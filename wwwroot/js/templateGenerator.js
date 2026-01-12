class TemplateGenerator {
    constructor() {
        this.constants = window.CONSTANTS;
        this.api = new ApiClient();
        this.validator = new Validator();
        this.ui = new UIManager();
        this.storage = this.createStorage();
        this.toastManager = window.toastManager;

        this.csvContent = null;
        this.currentTableColumns = []; // Store columns for validation

        this.init();
    }

    createStorage() {
        return {
            set: (key, value) => {
                try { sessionStorage.setItem(key, JSON.stringify(value)); } catch (e) { console.warn('Session storage not available'); }
            },
            get: (key) => {
                try { return JSON.parse(sessionStorage.getItem(key)); } catch (e) { return null; }
            },
            remove: (key) => {
                try { sessionStorage.removeItem(key); } catch (e) { }
            },
            clear: () => {
                try { sessionStorage.clear(); } catch (e) { }
            }
        };
    }

    init() {
        this.bindEvents();
        this.initializeSelections();
    }

    bindEvents() {
        $(this.constants.SELECTORS.DROPDOWNS.SERVER).on('change', () => this.onServerChange());
        $(this.constants.SELECTORS.DROPDOWNS.DATABASE).on('change', () => this.onDatabaseChange());
        $(this.constants.SELECTORS.DROPDOWNS.TEMPLATE_TYPE).on('change', () => this.onTemplateTypeChange());

        // Debounce table name input
        $(this.constants.SELECTORS.DROPDOWNS.TABLE_NAME).on('input', SiteUtilities.debounce(() => this.onTableNameChange(), 1000));

        $(this.constants.SELECTORS.INPUTS.CSV_FILE).on('change', (e) => this.onCsvFileChange(e));
        $(this.constants.SELECTORS.INPUTS.TICKET_NUMBER).on('input', () => this.checkGenerateButton());
        $(this.constants.SELECTORS.INPUTS.OBJECT_NAMES).on('input', () => this.checkGenerateButton());

        $(this.constants.SELECTORS.BUTTONS.GENERATE).on('click', () => this.generateTemplate());
        $(this.constants.SELECTORS.BUTTONS.DOWNLOAD).on('click', () => this.downloadTemplate());
        $(this.constants.SELECTORS.BUTTONS.CLEAR_SELECTIONS).on('click', () => this.clearSelections());

        // Event delegation for dynamic elements handled by UIManager
        $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).on('input change', this.constants.SELECTORS.CLASSES.COLUMN_INPUT, (e) => this.onColumnInputChange(e.target));
        $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).on('change', this.constants.SELECTORS.CLASSES.WHERE_CHECKBOX, () => this.onWhereCheckboxChange());

        // Bit Toggle Listener delegated
        $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).on('change', this.constants.SELECTORS.CLASSES.BIT_VALUE_TOGGLE, function () {
            const isChecked = $(this).is(':checked');
            const $label = $(this).closest('.toggle-wrapper').find('.bit-value-label');
            $label.text(isChecked ? 'True' : 'False');
            $label.css('color', isChecked ? 'var(--success-color)' : 'var(--text-muted)');
        });

        // FK Toggle Listener
        $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).on('change', '.fk-toggle-checkbox', function () {
            const column = $(this).data('column');
            const isChecked = $(this).is(':checked');
            const $panel = $(`#fk_config_${column}`);
            if (isChecked) $panel.slideDown(); else $panel.slideUp();
        });
    }

    async onServerChange() {
        const serverName = $(this.constants.SELECTORS.DROPDOWNS.SERVER).val();
        this.storage.set(this.constants.STORAGE_KEYS.SELECTED_SERVER, serverName);

        // Clear dependent dropdowns
        $(this.constants.SELECTORS.DROPDOWNS.DATABASE).html('<option value="">Loading...</option>').prop('disabled', true);
        $(this.constants.SELECTORS.DROPDOWNS.SERVER).prop('disabled', true); // Lock server during load

        if (!serverName) {
            this.ui.populateDatabases([]);
            $(this.constants.SELECTORS.DROPDOWNS.DATABASE).prop('disabled', false);
            $(this.constants.SELECTORS.DROPDOWNS.SERVER).prop('disabled', false);
            return;
        }

        try {
            const databases = await this.api.getDatabases(serverName);
            this.ui.populateDatabases(databases);
            if (this.toastManager) this.toastManager.showSuccess(this.constants.MESSAGES.DATABASES_LOADED);
        } catch (error) {
            console.error('Error loading databases:', error);
            if (this.toastManager) this.toastManager.showError(this.constants.MESSAGES.DATABASES_LOAD_ERROR);
            $(this.constants.SELECTORS.DROPDOWNS.DATABASE).html('<option value="">-- Error loading databases --</option>');
        } finally {
            $(this.constants.SELECTORS.DROPDOWNS.DATABASE).prop('disabled', false);
            $(this.constants.SELECTORS.DROPDOWNS.SERVER).prop('disabled', false);
            this.checkGenerateButton();
        }
    }

    async onDatabaseChange() {
        const databaseName = $(this.constants.SELECTORS.DROPDOWNS.DATABASE).val();
        this.storage.set(this.constants.STORAGE_KEYS.SELECTED_DATABASE, databaseName);
        this.checkGenerateButton();

        const tableName = $(this.constants.SELECTORS.DROPDOWNS.TABLE_NAME).val();
        if (tableName) {
            await this.loadTableColumns(tableName.trim());
        }
    }

    onTemplateTypeChange() {
        const templateType = $(this.constants.SELECTORS.DROPDOWNS.TEMPLATE_TYPE).val();
        this.storage.set(this.constants.STORAGE_KEYS.TEMPLATE_TYPE, templateType);

        this.ui.showTemplateSection(templateType);
        this.checkGenerateButton();
    }

    async onTableNameChange() {
        const tableName = $(this.constants.SELECTORS.DROPDOWNS.TABLE_NAME).val();
        console.log('[TemplateGenerator] onTableNameChange triggered. Table:', tableName);
        this.storage.set(this.constants.STORAGE_KEYS.TABLE_NAME, tableName);

        if (tableName) {
            await this.loadTableColumns(tableName.trim());
        } else {
            this.ui.renderTableColumns([]);
        }
        this.checkGenerateButton();
    }

    async loadTableColumns(tableName) {
        console.log('[TemplateGenerator] loadTableColumns called for:', tableName);
        const server = $(this.constants.SELECTORS.DROPDOWNS.SERVER).val();
        const database = $(this.constants.SELECTORS.DROPDOWNS.DATABASE).val();

        console.log('[TemplateGenerator] Server:', server, 'Database:', database);

        if (!server || !database || !tableName) {
            console.log('[TemplateGenerator] Missing dependencies for loadTableColumns');
            return;
        }

        // Show loading state (could add to UI manager)
        $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).html('<div class="loading-spinner">Loading columns...</div>');

        try {
            console.log('[TemplateGenerator] Calling API getTableColumns...');
            const columns = await this.api.getTableColumns(server, database, tableName);
            console.log('[TemplateGenerator] API returned columns:', columns);

            // Check concurrency: if selection changed while loading, ignore (simplistic check)
            const currentTable = $(this.constants.SELECTORS.DROPDOWNS.TABLE_NAME).val();
            if (currentTable !== tableName) {
                console.log('[TemplateGenerator] Table name changed during load. Ignoring result.');
                return;
            }

            this.currentTableColumns = columns;
            this.ui.renderTableColumns(columns);

            // Restore any saved data
            this.loadTableData();
            // Trigger update for UI consistency
            this.ui.updateWhereConditionSummary();

        } catch (error) {
            console.error('Error loading columns:', error);
            $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).html(`<div class="error-message">Error loading columns: ${error.message}</div>`);
        }
        this.checkGenerateButton();
    }

    onColumnInputChange(input) {
        this.validator.validateColumnInput(input);
        this.saveTableData();
        this.checkGenerateButton();
    }

    onWhereCheckboxChange() {
        this.saveTableData();
        this.ui.updateWhereConditionSummary();
        this.checkGenerateButton();
    }

    onCsvFileChange(e) {
        const file = e.target.files[0];
        if (file) {
            const reader = new FileReader();
            reader.onload = (e) => {
                this.csvContent = e.target.result;
                this.checkGenerateButton();

                // Validate bulk data immediately if possible, or just on generate
                if (this.currentTableColumns.length > 0) {
                    const validation = this.validator.validateBulkData(this.csvContent, this.currentTableColumns);
                    if (!validation.isValid && this.toastManager) {
                        this.toastManager.showError(validation.error);
                    }
                }

                if (this.csvContent) {
                    $(this.constants.SELECTORS.CLASSES.COLUMN_INPUT).prop('disabled', true);
                }
            };
            reader.readAsText(file);
        } else {
            this.csvContent = null;
            $(this.constants.SELECTORS.CLASSES.COLUMN_INPUT).prop('disabled', false);
            this.checkGenerateButton();
        }
    }

    getTableData() {
        const tableData = {};
        $(this.constants.SELECTORS.CLASSES.COLUMN_INPUT).each(function () {
            const $input = $(this);
            const columnName = $input.attr('name').replace('TableData.', '');
            const rawValue = $input.attr('type') === 'checkbox' ? $input.prop('checked') : $input.val();

            // Helper to check where checkbox state safely
            const whereChecked = $(`#where_${columnName}`).is(':checked');

            const fkChecked = $(this).closest('.column-card').find(`.fk-toggle-checkbox[data-column="${columnName}"]`).is(':checked');
            let fkDef = null;

            if (fkChecked) {
                const $card = $(this).closest('.column-card');
                fkDef = {
                    ReferencedTable: $card.find(`.fk-input-table[data-column="${columnName}"]`).val(),
                    ReferencedColumn: $card.find(`.fk-input-ref-col[data-column="${columnName}"]`).val() || 'ID',
                    WhereColumn: $card.find(`.fk-input-where-col[data-column="${columnName}"]`).val(),
                    WhereValue: $card.find(`.fk-input-where-val[data-column="${columnName}"]`).val()
                };
            }

            tableData[columnName] = {
                Value: String(rawValue),
                DataType: $input.data('type') || '',
                IsNullable: $input.data('is-nullable') === true,
                IsPrimaryKey: $input.data('is-primary-key') === true,
                UseInWhereClause: whereChecked,
                IsForeignKey: fkChecked,
                ForeignKey: fkDef
            };
        });
        return tableData;
    }

    getWhereClauseColumns() {
        const whereColumns = [];
        $(`${this.constants.SELECTORS.CLASSES.WHERE_CHECKBOX}:checked`).each(function () {
            whereColumns.push($(this).data('column'));
        });
        return whereColumns;
    }

    saveTableData() {
        const tableData = this.getTableData();
        const whereClauseColumns = this.getWhereClauseColumns();
        this.storage.set(this.constants.STORAGE_KEYS.TABLE_DATA, tableData);
        this.storage.set(this.constants.STORAGE_KEYS.WHERE_CLAUSE_COLUMNS, whereClauseColumns);
    }

    loadTableData() {
        const savedData = this.storage.get(this.constants.STORAGE_KEYS.TABLE_DATA) || {};
        const savedWhereColumns = this.storage.get(this.constants.STORAGE_KEYS.WHERE_CLAUSE_COLUMNS) || [];

        // Load values
        for (const [key, value] of Object.entries(savedData)) {
            const $input = $(`[name="TableData.${key}"]`);
            if ($input.length) {
                let val = value;
                if (value && typeof value === 'object' && value.hasOwnProperty('Value')) val = value.Value;

                if ($input.attr('type') === 'checkbox') {
                    $input.prop('checked', val === 'true' || val === true);
                    $input.trigger('change'); // trigger visual updates
                } else {
                    $input.val(val);
                }
            }
        }

        // Load Where checkboxes
        if (savedWhereColumns.length > 0) {
            let cols = Array.isArray(savedWhereColumns) ? savedWhereColumns : savedWhereColumns.split(',');
            $(this.constants.SELECTORS.CLASSES.WHERE_CHECKBOX).each(function () {
                const col = $(this).data('column');
                $(this).prop('checked', cols.includes(col));
            });
        }
    }

    checkGenerateButton() {
        const data = {
            ServerName: $(this.constants.SELECTORS.DROPDOWNS.SERVER).val(),
            DatabaseName: $(this.constants.SELECTORS.DROPDOWNS.DATABASE).val(),
            TemplateType: $(this.constants.SELECTORS.DROPDOWNS.TEMPLATE_TYPE).val(),
            TicketNumber: $(this.constants.SELECTORS.INPUTS.TICKET_NUMBER).val(),
            ObjectNames: $(this.constants.SELECTORS.INPUTS.OBJECT_NAMES).val(),
            TableName: $(this.constants.SELECTORS.DROPDOWNS.TABLE_NAME).val(),
            CsvContent: this.csvContent
        };

        const whereClauseCount = $(`${this.constants.SELECTORS.CLASSES.WHERE_CHECKBOX}:checked`).length;

        // Use Validation Module
        // Note: We need to adapt the validation logic slightly as Validator expects whereClauseColumnsCount
        let isValid = false;

        // Basic required check
        if (data.ServerName && data.DatabaseName && data.TemplateType && data.TicketNumber) {
            isValid = this.validator.validateFormData(data, whereClauseCount);
        }

        this.ui.setButtonState(this.constants.SELECTORS.BUTTONS.GENERATE, isValid);
    }

    async generateTemplate() {
        // Final Validation
        if (this.csvContent && this.currentTableColumns.length > 0) {
            const validation = this.validator.validateBulkData(this.csvContent, this.currentTableColumns);
            if (!validation.isValid) {
                if (this.toastManager) this.toastManager.showError(validation.error);
                return;
            }
        }

        const requestData = this.buildRequestData();

        try {
            const result = await this.api.generateTemplate(requestData);

            if (result.success) {
                if (this.toastManager) this.toastManager.showSuccess('Template generated successfully');
                this.ui.displayTemplateResult(result.template, requestData.TemplateType);
            } else {
                if (this.toastManager) this.toastManager.showError(result.error || 'Error generating template');
            }
        } catch (error) {
            if (this.toastManager) this.toastManager.showError('An error occurred during generation.');
            console.error(error);
        }
    }

    async downloadTemplate() {
        const requestData = this.buildRequestData();
        try {
            const blob = await this.api.downloadTemplate(requestData);
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `Template_${requestData.TicketNumber}.sql`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);
            if (this.toastManager) this.toastManager.showSuccess('Download started');
        } catch (error) {
            if (this.toastManager) this.toastManager.showError('Download failed');
            console.error(error);
        }
    }

    buildRequestData() {
        return {
            ServerName: $(this.constants.SELECTORS.DROPDOWNS.SERVER).val(),
            DatabaseName: $(this.constants.SELECTORS.DROPDOWNS.DATABASE).val(),
            TemplateType: $(this.constants.SELECTORS.DROPDOWNS.TEMPLATE_TYPE).val(),
            TicketNumber: $(this.constants.SELECTORS.INPUTS.TICKET_NUMBER).val(),
            ObjectNames: $(this.constants.SELECTORS.INPUTS.OBJECT_NAMES).val(),
            TableName: $(this.constants.SELECTORS.DROPDOWNS.TABLE_NAME).val(),
            ColumnValues: this.getTableData(),
            SelectedWhereColumns: this.getWhereClauseColumns(),
            CsvContent: this.csvContent,
            IncludeDrop: false // Defaults
        };
    }

    clearSelections() {
        this.storage.clear();

        // Reset Dropdowns
        $(this.constants.SELECTORS.DROPDOWNS.SERVER).val('').trigger('change'); // This handles dependent dropdowns
        $(this.constants.SELECTORS.DROPDOWNS.TEMPLATE_TYPE).val('');

        // Clear Inputs
        $(this.constants.SELECTORS.INPUTS.TICKET_NUMBER).val('');
        $(this.constants.SELECTORS.INPUTS.OBJECT_NAMES).val('');
        $(this.constants.SELECTORS.DROPDOWNS.TABLE_NAME).val('');
        $(this.constants.SELECTORS.INPUTS.CSV_FILE).val('');

        // Hide Sections
        $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).empty();
        $(this.constants.SELECTORS.CONTAINERS.SQL_OBJECTS_SECTION).hide();
        $('#resultsSection').slideUp();
        $('#whereConditionSummary').remove();

        // Reset internal state
        this.currentTableColumns = [];
        this.csvContent = null;

        // Reset UI Manager state where applicable
        this.ui.showTemplateSection(null);

        if (this.toastManager) this.toastManager.showInfo('All selections cleared');
    }

    async initializeSelections() {
        const savedServer = this.storage.get(this.constants.STORAGE_KEYS.SELECTED_SERVER);
        const savedDatabase = this.storage.get(this.constants.STORAGE_KEYS.SELECTED_DATABASE);
        const savedTemplateType = this.storage.get(this.constants.STORAGE_KEYS.TEMPLATE_TYPE);
        const savedTicketNumber = this.storage.get(this.constants.STORAGE_KEYS.TICKET_NUMBER);
        const savedTableName = this.storage.get(this.constants.STORAGE_KEYS.TABLE_NAME);

        if (savedTicketNumber) $(this.constants.SELECTORS.INPUTS.TICKET_NUMBER).val(savedTicketNumber);
        if (savedTableName) $(this.constants.SELECTORS.DROPDOWNS.TABLE_NAME).val(savedTableName);

        if (savedTemplateType) {
            $(this.constants.SELECTORS.DROPDOWNS.TEMPLATE_TYPE).val(savedTemplateType);
            this.onTemplateTypeChange();
        }

        if (savedServer) {
            $(this.constants.SELECTORS.DROPDOWNS.SERVER).val(savedServer);
            await this.onServerChange();

            if (savedDatabase) {
                $(this.constants.SELECTORS.DROPDOWNS.DATABASE).val(savedDatabase);
                await this.onDatabaseChange();
            }
        }
    }
}

// Initialize
$(document).ready(() => {
    window.templateGenerator = new TemplateGenerator();
});
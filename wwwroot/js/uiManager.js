class UIManager {
    constructor() {
        this.constants = window.CONSTANTS;
        this.templateConfig = {
            [this.constants.TEMPLATE_TYPES.PERMISSION]: {
                section: null,
                requiredFields: [],
                hideObjects: false
            },
            [this.constants.TEMPLATE_TYPES.DATA]: {
                section: null,
                requiredFields: [],
                hideObjects: false
            },
            [this.constants.TEMPLATE_TYPES.INSERT_UPDATE]: {
                section: '#insertUpdateTemplateSection',
                requiredFields: [this.constants.SELECTORS.DROPDOWNS.TABLE_NAME],
                hideObjects: true
            },
            [this.constants.TEMPLATE_TYPES.SP]: {
                section: null,
                requiredFields: [],
                hideObjects: false
            }
        };
    }

    renderTableColumns(columns) {
        if (!columns || columns.length === 0) {
            $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).html(`
                <div class="table-not-found">
                    <svg width="20" height="20" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                    </svg>
                    Table not found or no columns available. Please check the table name.
                </div>
            `);
            return;
        }

        let html = '<div class="table-columns-grid">';

        columns.forEach(column => {
            if (column.isIdentity) return;

            const isRequired = column.isNullable === 'NO' && !column.isIdentity && !column.isPrimaryKey;
            const isPk = column.isPrimaryKey;
            const inputType = this.getInputTypeForDataType(column.dataType);
            const placeholder = isPk ? 'Auto-generated if empty' : this.getPlaceholderForDataType(column.dataType);

            let cardClasses = 'column-card';
            if (isPk) cardClasses += ' primary-key';
            else if (isRequired) cardClasses += ' required-field';

            html += `
                <div class="${cardClasses}">
                    <div class="column-header">
                        <div class="column-info">
                            <span class="column-name">
                                ${column.columnName}
                                ${isPk ? '<span class="pk-badge">PK</span>' : ''}
                            </span>
                            <span class="column-meta">
                                ${column.dataType}${column.maxLength ? `(${column.maxLength})` : ''} 
                                ${column.isNullable === 'YES' ? '(null)' : '(not null)'}
                            </span>
                        </div>
                    </div>
                    
                    <div class="form-group">
                        ${inputType === 'checkbox' ? `
                            <div class="toggle-wrapper" style="background: transparent; padding: 0; margin-top: 0.5rem; justify-content: flex-start; gap: 1rem;">
                                <label class="toggle-switch toggle-success">
                                    <input type="checkbox"
                                        name="TableData.${column.columnName}"
                                        class="checkbox-input column-input bit-value-toggle"
                                        data-type="${column.dataType}"
                                        data-is-nullable="${column.isNullable === 'YES'}"
                                        data-is-primary-key="${column.isPrimaryKey}"
                                        ${isRequired ? 'required' : ''}>
                                    <span class="slider"></span>
                                </label>
                                <span class="bit-value-label" style="font-size: 0.9rem; font-weight: 500; min-width: 40px; color: var(--text-muted);">False</span>
                            </div>
                        ` : `
                            <input type="${inputType}"
                                name="TableData.${column.columnName}"
                                class="form-input column-input"
                                placeholder="${placeholder}"
                                data-type="${column.dataType}"
                                data-is-nullable="${column.isNullable === 'YES'}"
                                data-is-primary-key="${column.isPrimaryKey}"
                                ${isRequired ? 'required' : ''}>
                        `}
                    </div>

                    <div class="toggle-wrapper">
                        <span class="toggle-label">Use in WHERE clause</span>
                        <label class="toggle-switch">
                            <input type="checkbox" 
                                   id="where_${column.columnName}" 
                                   class="where-condition-checkbox"
                                   data-column="${column.columnName}"
                                   ${isPk ? 'checked' : ''}>
                            <span class="slider"></span>
                        </label>
                    </div>
                    <!-- Foreign Key Section -->
                    <div class="fk-section">
                        <div class="toggle-wrapper" style="margin-top: 0; background: transparent; padding: 0; justify-content: space-between;">
                            <span class="toggle-label">Foreign Key Lookup</span>
                            <label class="toggle-switch">
                                <input type="checkbox" 
                                       class="fk-toggle-checkbox"
                                       data-column="${column.columnName}">
                                <span class="slider"></span>
                            </label>
                        </div>

                        <div id="fk_config_${column.columnName}" class="fk-config-panel" style="display: none;">
                            <div class="fk-input-group">
                                <label class="fk-label">Ref Table</label>
                                <input type="text" class="fk-input fk-input-table" placeholder="e.g. UC_X_Table" data-column="${column.columnName}">
                            </div>
                            <div class="fk-input-group">
                                <label class="fk-label">Ref Column (ID)</label>
                                <input type="text" class="fk-input fk-input-ref-col" placeholder="ID" value="ID" data-column="${column.columnName}">
                            </div>
                            <div class="fk-grid-row">
                                <div class="fk-input-group">
                                    <label class="fk-label">Where Col</label>
                                    <input type="text" class="fk-input fk-input-where-col" placeholder="Name" data-column="${column.columnName}">
                                </div>
                                <div class="fk-input-group">
                                    <label class="fk-label">Where Value</label>
                                    <input type="text" class="fk-input fk-input-where-val" placeholder="Value" data-column="${column.columnName}">
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        });

        html += '</div>';
        $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).html(html);
    }

    updateWhereConditionSummary() {
        const selectedColumns = [];
        $(`${this.constants.SELECTORS.CLASSES.WHERE_CHECKBOX}:checked`).each(function () {
            selectedColumns.push($(this).data('column'));
        });

        let summaryElement = $(this.constants.SELECTORS.CONTAINERS.WHERE_CONDITION_SUMMARY);

        if (!summaryElement.length && $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).length) {
            $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).before(`
                <div id="whereConditionSummary" class="dynamic-section" style="margin-bottom:1rem; padding:1rem; background:rgba(30,41,59,0.5); border-radius:8px; border:1px solid rgba(255,255,255,0.05);">
                    <h4 style="margin-bottom: 8px; font-size:1rem; display: flex; align-items: center; gap: 8px;">
                        <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h18M3 14h18m-9-4v8m-7 0h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z"></path>
                        </svg>
                        Where Condition Columns
                    </h4>
                    <div class="form-hint">
                        <strong>Selected keys:</strong>
                        <span id="selectedWhereColumnsList" style="color: var(--primary-light); font-weight: 600; font-family:monospace;"></span>
                    </div>
                </div>
            `);
            summaryElement = $(this.constants.SELECTORS.CONTAINERS.WHERE_CONDITION_SUMMARY);
        }

        const selectedList = selectedColumns.length > 0
            ? selectedColumns.join(', ')
            : '<span style="color: var(--text-muted); font-style:italic;"></span>';

        $(this.constants.SELECTORS.CONTAINERS.SELECTED_WHERE_COLUMNS_LIST).html(selectedList);
        summaryElement.removeClass(this.constants.SELECTORS.CLASSES.INPUT_ERROR);
    }

    populateDatabases(databases) {
        let options = '<option value="">-- Select Database --</option>';
        if (databases && databases.length > 0) {
            databases.forEach(db => {
                options += `<option value="${db}">${db}</option>`;
            });
        } else {
            options = '<option value="">-- No databases found --</option>';
        }
        $(this.constants.SELECTORS.DROPDOWNS.DATABASE).html(options);
    }

    showTemplateSection(templateType) {
        $(this.constants.SELECTORS.CLASSES.TEMPLATE_SECTION).removeClass(this.constants.SELECTORS.CLASSES.ACTIVE);

        // Hide SQL Objects by default
        $(this.constants.SELECTORS.CONTAINERS.SQL_OBJECTS_SECTION).hide();

        if (templateType) {
            if (templateType === this.constants.TEMPLATE_TYPES.INSERT_UPDATE ||
                templateType === this.constants.TEMPLATE_TYPES.BULK_INSERT) {
                $(this.constants.SELECTORS.CONTAINERS.SQL_OBJECTS_SECTION).hide();
            } else {
                $(this.constants.SELECTORS.CONTAINERS.SQL_OBJECTS_SECTION).fadeIn();
            }

            const typeKey = templateType === this.constants.TEMPLATE_TYPES.BULK_INSERT
                ? this.constants.TEMPLATE_TYPES.INSERT_UPDATE
                : templateType;

            if (this.templateConfig[typeKey]) {
                const section = this.templateConfig[typeKey].section;
                if (section) {
                    $(section).addClass(this.constants.SELECTORS.CLASSES.ACTIVE);
                }
            }
        }

        // Handle specific UI elements for Bulk Insert vs Insert/Update
        if (templateType === this.constants.TEMPLATE_TYPES.BULK_INSERT) {
            $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).hide();
            $(this.constants.SELECTORS.CONTAINERS.WHERE_CONDITION_SUMMARY).hide();
            $(this.constants.SELECTORS.CONTAINERS.UPSERT_KEYS_CONTAINER).hide();
            $('.btn-add-row').hide();
            $(this.constants.SELECTORS.CONTAINERS.BULK_UPLOAD).show();
        } else if (templateType === this.constants.TEMPLATE_TYPES.INSERT_UPDATE) {
            $(this.constants.SELECTORS.CONTAINERS.TABLE_COLUMNS).show();
            $(this.constants.SELECTORS.CONTAINERS.WHERE_CONDITION_SUMMARY).show();
            $(this.constants.SELECTORS.CONTAINERS.UPSERT_KEYS_CONTAINER).show();
            $(this.constants.SELECTORS.CONTAINERS.BULK_UPLOAD).hide();
        }
    }

    getInputTypeForDataType(dataType) {
        const typeMap = {
            'bit': 'checkbox',
            'int': 'number',
            'bigint': 'number',
            'smallint': 'number',
            'tinyint': 'number',
            'decimal': 'number',
            'numeric': 'number',
            'float': 'number',
            'real': 'number',
            'money': 'number',
            'smallmoney': 'number',
            'date': 'date',
            'datetime': 'datetime-local',
            'datetime2': 'datetime-local',
            'smalldatetime': 'datetime-local',
            'time': 'time'
        };
        return typeMap[dataType.toLowerCase()] || 'text';
    }

    getPlaceholderForDataType(dataType) {
        const placeholderMap = {
            'varchar': 'Enter text...',
            'nvarchar': 'Enter text...',
            'char': 'Enter text...',
            'nchar': 'Enter text...',
            'text': 'Enter text...',
            'ntext': 'Enter text...',
            'int': 'Enter number...',
            'bigint': 'Enter number...',
            'decimal': 'Enter decimal...',
            'numeric': 'Enter number...',
            'float': 'Enter decimal...',
            'datetime': 'Select date and time...',
            'date': 'Select date...',
            'bit': 'True/False'
        };
        return placeholderMap[dataType.toLowerCase()] || 'Enter value...';
    }

    toggleSection(selector, show) {
        if (show) $(selector).slideDown();
        else $(selector).slideUp();
    }

    setButtonState(selector, enabled) {
        $(selector).prop('disabled', !enabled);
    }

    displayTemplateResult(content, type) {
        $('#finalCode').text(content);
        $('#resultsBadge').text(type ? type.toUpperCase() : '');
        $('#resultsSection').slideDown();

        // Scroll to results
        $('html, body').animate({
            scrollTop: $("#resultsSection").offset().top - 100
        }, 500);
    }
}

class Validator {
    constructor() {
        this.auditColumns = ['createddate', 'createdby', 'updateddate', 'updatedby'];
    }

    validateFormData(data, whereClauseColumnsCount) {
        if (!data.ServerName || !data.DatabaseName || !data.TemplateType || !data.TicketNumber) {
            return false;
        }

        if (data.TemplateType === 'insertupdate') {
            if (data.CsvContent && data.CsvContent.length > 0) return true;
            return data.TableName && whereClauseColumnsCount > 0;
        }

        if (data.TemplateType === 'bulkinsert') {
            return data.CsvContent && data.CsvContent.length > 0 && data.TableName;
        }

        return true;
    }

    validateBulkData(csvContent, tableColumns) {
        if (!csvContent || !tableColumns) return { isValid: true };

        const rows = csvContent.split(/\r?\n/).filter(row => row.trim());
        if (rows.length === 0) return { isValid: false, error: 'CSV file is empty' };

        // Filter table columns: exclude identity and audit columns
        const validTableColumns = tableColumns.filter(c =>
            !c.isIdentity &&
            !this.auditColumns.includes(c.columnName.toLowerCase())
        );

        // We can't strictly check column counts anymore because of the "Omitted ID" feature.
        // But we can check if it's within a reasonable range (e.g., matching or matching-1)
        const inputColumnCount = rows[0].split(',').length;
        if (inputColumnCount > validTableColumns.length) {
            return {
                isValid: false,
                error: `Column count mismatch. CSV has ${inputColumnCount} columns, which is more than the table requires (${validTableColumns.length}) (excluding audit fields).`
            };
        }

        let startRow = 1;
        if (rows.length === 1) startRow = 0; // If only 1 row, treat as data

        const maxRows = rows.length;

        for (let i = startRow; i < maxRows; i++) {
            // Regex to handle quoted CSV values properly, similar to backend
            // But for simple validation, split by comma is often used. 
            // If the user's data has commas, simple split fails. 
            // Let's assume simple split for now to match previous logic, or improve if needed.
            // keeping previous simple split for consistency unless requested otherwise.
            const cells = rows[i].split(',');

            // Logic: Iterate through Table Columns and map them to CSV Cells
            let cellIndex = 0;

            for (let j = 0; j < validTableColumns.length; j++) {
                const col = validTableColumns[j];
                let isOmitted = false;
                let rawVal = null;

                if (cellIndex < cells.length) {
                    rawVal = cells[cellIndex].trim();

                    // Smart Heuristic: Check for Type Mismatch
                    // If column is Numeric ID/PK but CSV value is Non-Numeric, assume Omitted
                    const isNumericCol = this.isNumericType(col.dataType);
                    const isNumericVal = this.isNumericValue(rawVal);

                    const isPkOrId = (col.isPrimaryKey || col.columnName.toLowerCase() === 'id') && !col.isIdentity;

                    if (isPkOrId && isNumericCol && !isNumericVal && rawVal !== '') {
                        isOmitted = true;
                    }
                } else {
                    // Out of cells, assume omitted or let validation fail if strictly required
                    // If it's PK, we might assume omitted. If not, it's missing data.
                    const isPkOrId = (col.isPrimaryKey || col.columnName.toLowerCase() === 'id');
                    if (isPkOrId) isOmitted = true;
                }

                if (isOmitted) {
                    // Column is omitted (Auto-Gen), so we skip validation for this COL
                    // and do NOT increment cellIndex (the current cell belongs to NEXT col)
                    continue;
                }

                // If not omitted, we MUST have a value
                if (cellIndex >= cells.length) {
                    return { isValid: false, error: `Row ${i + 1} is missing data for column '${col.columnName}'.` };
                }

                // Validate
                if (!this.isValidBulkValue(rawVal, col)) {
                    return { isValid: false, error: `Validation failed at Row ${i + 1}, Column '${col.columnName}': Value '${rawVal}' is invalid for type ${col.dataType}.` };
                }

                cellIndex++;
            }
        }

        return { isValid: true };
    }

    isNumericType(dataType) {
        if (!dataType) return false;
        const type = dataType.toLowerCase();
        return type.includes('int') || type.includes('decimal') || type.includes('numeric') || type.includes('float') || type.includes('money');
    }

    isNumericValue(val) {
        if (!val) return false;
        // Allow simple number check. 
        // Note: values like "123" are numeric. "abc" are not.
        return !isNaN(val) && !isNaN(parseFloat(val));
    }

    isValidBulkValue(val, col) {
        // Handle Nulls
        if (!val || val === '' || val.toLowerCase() === 'null') {
            return col.isNullable === 'YES';
        }

        const type = col.dataType.toLowerCase();

        if (type.includes('int') || type.includes('bigint') || type.includes('smallint') || type.includes('tinyint')) {
            return /^-?\d+$/.test(val);
        }

        if (type.includes('decimal') || type.includes('numeric') || type.includes('money') || type.includes('float')) {
            return /^-?\d*\.?\d+$/.test(val);
        }

        if (type.includes('bit')) {
            return /^(true|false|0|1)$/i.test(val);
        }

        if (type.includes('date') || type.includes('time')) {
            return !isNaN(Date.parse(val));
        }

        return true;
    }

    validateColumnInput(input) {
        const $input = $(input);
        const value = $input.val();
        const dataType = $input.data('type');
        const isNullable = $input.data('is-nullable');

        $input.removeClass('input-error input-success');

        if (!value && !isNullable) {
            $input.addClass('input-error');
            return false;
        }

        // Basic type validation
        if (value) {
            switch (dataType.toLowerCase()) {
                case 'int':
                case 'bigint':
                case 'smallint':
                case 'tinyint':
                    if (!/^-?\d+$/.test(value)) {
                        $input.addClass('input-error');
                        return false;
                    }
                    break;
                case 'decimal':
                case 'numeric':
                case 'float':
                case 'real':
                    if (!/^-?\d*\.?\d+$/.test(value)) {
                        $input.addClass('input-error');
                        return false;
                    }
                    break;
                case 'bit':
                    if (!/^(true|false|0|1)$/i.test(value)) {
                        $input.addClass('input-error');
                        return false;
                    }
                    break;
            }
        }

        $input.addClass('input-success');
        return true;
    }
}

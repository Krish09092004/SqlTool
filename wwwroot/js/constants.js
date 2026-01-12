/**
 * Constants for the Template Builder Application
 * Centralizes all magic strings, selectors, and API endpoints.
 */
const CONSTANTS = {
    SELECTORS: {
        DROPDOWNS: {
            SERVER: '#serverDropdown',
            DATABASE: '#databaseDropdown',
            TEMPLATE_TYPE: '#templateType',
            TABLE_NAME: '#tableName'
        },
        INPUTS: {
            TICKET_NUMBER: '#ticketNumber',
            OBJECT_NAMES: '#objectNames',
            CSV_FILE: '#csvFile',
            REQUEST_VERIFICATION_TOKEN: 'input[name="__RequestVerificationToken"]'
        },
        BUTTONS: {
            GENERATE: '#generateBtn',
            DOWNLOAD: '#downloadBtn',

            CLEAR_SELECTIONS: '#clearSelectionsBtn'
        },
        CONTAINERS: {
            TOAST: '#toastContainer',

            TABLE_COLUMNS: '#tableColumnsContainer',
            SQL_OBJECTS_SECTION: '#sqlObjectsSection',
            WHERE_CONDITION_SUMMARY: '#whereConditionSummary',
            SELECTED_WHERE_COLUMNS_LIST: '#selectedWhereColumnsList',
            UPSERT_KEYS_CONTAINER: '#upsertKeysContainer',
            BULK_UPLOAD: '#bulkUploadContainer'
        },
        CLASSES: {
            TEMPLATE_SECTION: '.template-section',

            COLUMN_INPUT: '.column-input',
            WHERE_CHECKBOX: '.where-condition-checkbox',
            ACTIVE: 'active',
            INPUT_ERROR: 'input-error',
            INPUT_SUCCESS: 'input-success',
            BIT_VALUE_TOGGLE: '.bit-value-toggle'
        }
    },
    API: {
        GET_DATABASES: '/Home/GetDatabases',
        GET_TABLE_COLUMNS: '/Home/GetTableColumns'
    },
    STORAGE_KEYS: {
        SELECTED_SERVER: 'SelectedServer',
        SELECTED_DATABASE: 'SelectedDatabase',
        TEMPLATE_TYPE: 'TemplateType',
        TICKET_NUMBER: 'TicketNumber',
        OBJECT_NAMES: 'ObjectNames',

        TABLE_NAME: 'TableName',
        TABLE_DATA: 'TableData',
        WHERE_CLAUSE_COLUMNS: 'WhereClauseColumns'
    },
    TEMPLATE_TYPES: {
        PERMISSION: 'permission',
        DATA: 'data',
        INSERT_UPDATE: 'insertupdate',
        SP: 'sp',
        BULK_INSERT: 'bulkinsert'
    },
    MESSAGES: {
        DATABASES_LOADED: 'Databases loaded successfully',
        DATABASES_LOAD_ERROR: 'Failed to load databases',
        API_CALL_FAILED: 'API call failed'
    }
};

// Make it globally available
window.CONSTANTS = CONSTANTS;

namespace Template_Builder.Constants
{
    /// <summary>
    /// Centralized constants for the application to improve maintainability and avoid magic strings.
    /// </summary>
    public static class AppConstants
    {
        public static class ErrorMessages
        {
            public const string LoadConfigError = "Error loading application configuration. Please contact administrator.";
            public const string GenericValidation = "Validation error occurred";
            public const string PreviewGenerationError = "Error generating preview";
            public const string PreviewGenerationWarning = "An error occurred while generating the preview";
            public const string TemplateGenerationError = "An error occurred: {0}";
            public const string DownloadError = "An error occurred while downloading the template.";
        }

        public static class LogMessages
        {
            public const string LoadConfigError = "Error loading application configuration";
            public const string FetchDatabasesError = "Error fetching databases for server: {ServerName}";
            public const string ValidateTemplateError = "Error validating template";
            public const string GeneratePreviewError = "Error generating live preview";
            public const string FetchColumnsError = "Error fetching table columns for table: {TableName}";
            public const string GenerateTemplateError = "Error generating template";
            public const string DownloadTemplateError = "Error downloading template";
        }

        public static class MimeTypes
        {
            public const string Sql = "application/sql";
        }

        public static class Headers
        {
            public const string CacheControl = "Cache-Control";
            public const string Connection = "Connection";
        }

        public static class HeaderValues
        {
            public const string NoCache = "no-cache";
            public const string KeepAlive = "keep-alive";
        }

        public static class SqlQueries
        {
            public const string GetDatabases = "SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0 ORDER BY name";
            public const string GetTableColumns = @"
            SELECT 
                c.name AS ColumnName,
                t.name AS DataType,
                c.max_length AS MaxLength,
                c.precision AS Precision,
                c.scale AS Scale,
                c.is_nullable AS IsNullable,
                c.is_identity AS IsIdentity,
                c.is_computed AS IsComputed,
                CASE WHEN pk.index_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            INNER JOIN sys.tables tab ON c.object_id = tab.object_id
            INNER JOIN sys.schemas s ON tab.schema_id = s.schema_id
            LEFT JOIN (
                SELECT i.object_id, ic.column_id, i.index_id
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                WHERE i.is_primary_key = 1
            ) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
            WHERE s.name = @Schema AND tab.name = @TableName
                AND c.is_computed = 0
            ORDER BY c.column_id";
            
            public const string TableExists = @"
            SELECT COUNT(*) FROM sys.tables t 
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
            WHERE s.name = @Schema AND t.name = @TableName";

            public const string GetObjectDefinition = "SELECT OBJECT_DEFINITION(@objectId)";
            public const string FindProcedure = @"SELECT p.name, SCHEMA_NAME(p.schema_id), p.object_id, CAST(NULL AS NVARCHAR(MAX)) FROM sys.procedures p WHERE p.name = @cleanName AND SCHEMA_NAME(p.schema_id) = @schema";
            public const string FindFunction = @"SELECT o.name, SCHEMA_NAME(o.schema_id), o.object_id, CAST(NULL AS NVARCHAR(MAX)) FROM sys.objects o WHERE o.type IN ('FN','FS','FT','IF','TF') AND (o.name = @cleanName OR SCHEMA_NAME(o.schema_id) = @schema)";
            public const string FindView = @"SELECT v.name, SCHEMA_NAME(v.schema_id), v.object_id, CAST(NULL AS NVARCHAR(MAX)) FROM sys.views v WHERE v.name = @name OR SCHEMA_NAME(v.schema_id) + '.' + v.name = @name";
            public const string FindSpView = @"SELECT ProcedureName, 'dbo', object_id, [PROCEDURE] FROM dbo.vw_ProcedureReleaseScript WHERE ProcedureName = @name";
        }

        public static class Templates
        {
            public class Header
            {
                public const string Common = @"USE [{0}]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
";
                public const string StartMarker = "PRINT '-------------------- {0} START -------------------------';";
                public const string EndMarker = "PRINT '-------------------- {0} END -------------------------';";
            }

            public class Transaction
            {
                public const string Begin = @"
BEGIN TRANSACTION {0};
BEGIN TRY";
                public const string Commit = @"    COMMIT TRANSACTION {0};
    PRINT 'Transaction {0} committed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION {0};
    
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    PRINT 'Error occurred. Transaction {0} rolled back.';
    RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";
            }

            public class Snippets
            {
                public const string DropProcedure = @"
IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'{0}') AND type IN ('P'))
BEGIN
    DROP PROCEDURE {0};
    PRINT 'PROCEDURE {0} Dropped';
END;";
                public const string DropFunction = @"
IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'{0}') AND type IN ('FN','FS','FT','IF','TF'))
BEGIN
    DROP FUNCTION {0};
    PRINT 'FUNCTION {0} Dropped';
END;";
                public const string DropView = @"
IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'{0}') AND type IN ('V'))
BEGIN
    DROP VIEW {0};
    PRINT 'VIEW {0} Dropped';
END;";

            }
        }
    }
}

using Microsoft.Data.SqlClient;
using System.Text;
using Template_Builder.Models.Entities;
using Template_Builder.Models.ViewModels;
using Template_Builder.Services.Interfaces;
using Template_Builder.Constants;

namespace Template_Builder.Services.Implementations
{
    public class SqlTemplateService : ISqlTemplateService
    {
        private readonly ISqlSchemaService _schemaService;
        private readonly ILogger<SqlTemplateService> _logger;

        public SqlTemplateService(ISqlSchemaService schemaService, ILogger<SqlTemplateService> logger)
        {
            _schemaService = schemaService;
            _logger = logger;
        }

        public async Task<TemplateGenerationResult> GenerateTemplateAsync(TemplateRequest request)
        {
            try
            {
                // Validate request first
                var validationResult = await ValidateTemplateAsync(request);
                if (!validationResult.IsValid)
                {
                    return new TemplateGenerationResult
                    {
                        Success = false,
                        ErrorMessage = string.Join("; ", validationResult.Errors),
                        Warnings = validationResult.Warnings
                    };
                }

                string template;
                var type = request.TemplateType?.ToLower() ?? "";

                switch (type)
                {

                    case "insertupdate":
                        if (!string.IsNullOrEmpty(request.CsvContent) && request.IsIdempotent)
                        {
                            template = await GenerateUnifiedUpsertScript(request, isCsv: true);
                        }
                        else
                        {
                             // Fallback to standard flow (unified now)
                            template = await GenerateUnifiedUpsertScript(request, isCsv: false);
                        }
                        break;
                    case "bulkinsert":
                        // Bulk Insert always uses the CSV flow
                        template = await GenerateUnifiedUpsertScript(request, isCsv: true);
                        break;
                    default:
                        // Covers "sp", "permission", "data" or any legacy types
                        template = await GenerateSqlTemplateAsync(request);
                        break;
                }

                if (string.IsNullOrEmpty(template))
                {
                    return new TemplateGenerationResult
                    {
                        Success = false,
                        ErrorMessage = "Template generation failed. No content was generated."
                    };
                }

                return new TemplateGenerationResult
                {
                    Success = true,
                    Template = template
                };
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "SQL error generating template for ticket {TicketNumber}", request.TicketNumber);
                return new TemplateGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"Database error: {sqlEx.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating template for ticket {TicketNumber}", request.TicketNumber);
                return new TemplateGenerationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }



        private async Task<string> GenerateUnifiedUpsertScript(TemplateRequest request, bool isCsv)
        {
            var sb = new StringBuilder();

            // 1. Header
            sb.AppendLine(string.Format(AppConstants.Templates.Header.Common, request.DatabaseName));
            sb.AppendLine(string.Format(AppConstants.Templates.Header.StartMarker, request.TicketNumber.ToUpper()));
            sb.AppendLine();

            // 2. Metadata
            var (schema, tableName) = ParseTableName(request.TableName);
            var columns = await _schemaService.GetTableColumnsAsync(request.ServerName, request.DatabaseName, request.TableName);
            if (columns == null || !columns.Any())
                throw new Exception($"Table {request.TableName} not found or has no columns.");

            // 3. Prepare Data Rows
            var allRowsData = new List<Dictionary<string, string>>();

            if (isCsv)
            {
                using (var reader = new StringReader(request.CsvContent))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var values = System.Text.RegularExpressions.Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
                                        .Select(v => v.Trim().Trim('"'))
                                        .ToList();
                        if (!values.Any()) continue;

                        var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        int valIndex = 0;
                        foreach (var col in columns)
                        {
                            if (col.IsIdentity) continue;
                            if (col.ColumnName.Equals("CreatedDate", StringComparison.OrdinalIgnoreCase) ||
                                col.ColumnName.Equals("CreatedBy", StringComparison.OrdinalIgnoreCase) ||
                                col.ColumnName.Equals("UpdatedDate", StringComparison.OrdinalIgnoreCase) ||
                                col.ColumnName.Equals("UpdatedBy", StringComparison.OrdinalIgnoreCase))
                            {
                                if (valIndex < values.Count) valIndex++;
                                rowDict[col.ColumnName] = "AUTO_GENERATE";
                            }
                            else if ((col.IsPrimaryKey && !col.IsIdentity) || (col.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase) && !col.IsIdentity))
                            {
                                bool isOmitted = false;
                                string currentValue = null;

                                if (valIndex < values.Count)
                                {
                                    currentValue = values[valIndex];
                                    
                                    // Smart Heuristic: Check for Type Mismatch
                                    // If column is Numeric but CSV value is NOT numeric (and not empty), assume column was omitted
                                    if (IsNumericType(col.DataType) && !string.IsNullOrEmpty(currentValue) && !IsNumericValue(currentValue))
                                    {
                                        isOmitted = true;
                                    }
                                }
                                else
                                {
                                    isOmitted = true;
                                }

                                if (isOmitted)
                                {
                                    rowDict[col.ColumnName] = "MANUAL_ID";
                                    // Do NOT increment valIndex; the current value 'Avtar...' belongs to the NEXT column
                                }
                                else
                                {
                                   rowDict[col.ColumnName] = string.IsNullOrEmpty(currentValue) ? "MANUAL_ID" : currentValue;
                                   valIndex++;
                                }
                            }
                            else
                            {
                                if (valIndex < values.Count)
                                {
                                    rowDict[col.ColumnName] = values[valIndex];
                                    valIndex++;
                                }
                            }
                        }
                        allRowsData.Add(rowDict);
                    }
                }
            }
            else
            {
                var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (request.ColumnValues != null)
                {
                    foreach (var kvp in request.ColumnValues)
                    {
                        rowDict[kvp.Key] = kvp.Value.Value;
                    }
                }

                // Ensure all PKs are handled for Single Row
                foreach (var col in columns) {
                    if ((col.IsPrimaryKey || col.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)) && !col.IsIdentity) 
                    {
                        if (!rowDict.ContainsKey(col.ColumnName) || string.IsNullOrEmpty(rowDict[col.ColumnName]))
                             rowDict[col.ColumnName] = "MANUAL_ID";
                    }
                }

                allRowsData.Add(rowDict);
            }

            // 4. Determine WHERE Columns
            var whereColNames = request.SelectedWhereColumns != null && request.SelectedWhereColumns.Any()
                ? request.SelectedWhereColumns
                : columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName).ToList();

            if (!whereColNames.Any())
                whereColNames = columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName).ToList();

            // 5. Generate Script
            bool isSingleRow = allRowsData.Count == 1;

            // Generate Variables Declarations
            // Use dictionary to map Column Name -> Variable Name
            var variableMap = new Dictionary<string, string>();
            foreach (var col in columns)
            {
                if (col.IsIdentity) continue;
                var varName = $"@{SanitizeVariableName(col.ColumnName)}";
                variableMap[col.ColumnName] = varName;
            }

            if (isSingleRow)
            {
                // -- Single Row Mode: DECLARE @Var Type = Value; --
                var rowData = allRowsData[0];
                var printValueVar = ""; // Hold reference to what we print (First Where Col)

                foreach (var col in columns)
                {
                    if (col.IsIdentity) continue;

                    var varName = variableMap[col.ColumnName];
                    var sqlType = GetSqlDataType(col);

                    // Check for Foreign Key Definition
                    bool isFk = !isCsv && request.ColumnValues != null 
                                && request.ColumnValues.ContainsKey(col.ColumnName) 
                                && request.ColumnValues[col.ColumnName].IsForeignKey
                                && request.ColumnValues[col.ColumnName].ForeignKey != null;

                    if (isFk)
                    {
                        var fk = request.ColumnValues[col.ColumnName].ForeignKey;
                        var whereValSlug = $"{col.ColumnName}_LookupVal";
                        var whereValName = $"@{SanitizeVariableName(whereValSlug)}";
                        
                        // Declare the lookup value 
                        sb.AppendLine($"DECLARE {whereValName} VARCHAR(MAX) = '{fk.WhereValue?.Replace("'", "''")}';");
                        
                        var refTable = fk.ReferencedTable ?? "UNKNOWN_TABLE";
                        if (!refTable.StartsWith("[")) refTable = $"[{refTable}]";
                        
                        var refCol = fk.ReferencedColumn;
                        if(string.IsNullOrEmpty(refCol)) refCol = "ID";

                        sb.AppendLine($"DECLARE {varName} {sqlType} = (SELECT TOP 1 [{refCol}] FROM {refTable} WITH (NOLOCK) WHERE [{fk.WhereColumn}] = {whereValName});");
                    }
                    else
                    {
                        var literalValue = GetValueLiteral(col, rowData, schema, tableName);
                        sb.AppendLine($"DECLARE {varName} {sqlType} = {literalValue};");
                    }
                }
                sb.AppendLine();

                // Logic Block
                GenerateUpsertLogic(sb, columns, whereColNames, variableMap, schema, tableName, out _);
            }
            else
            {
                // -- Multi Row Mode: DECLARE @Var Type; Loop SET --
                foreach (var col in columns)
                {
                    if (col.IsIdentity) continue;
                    var varName = variableMap[col.ColumnName];
                    var sqlType = GetSqlDataType(col);
                    sb.AppendLine($"DECLARE {varName} {sqlType};");
                }
                sb.AppendLine();

                foreach (var rowData in allRowsData)
                {
                    sb.AppendLine("-- Processing Row");
                    foreach (var col in columns)
                    {
                        if (col.IsIdentity) continue;
                        var literalValue = GetValueLiteral(col, rowData, schema, tableName);
                        var varName = variableMap[col.ColumnName];
                        sb.AppendLine($"SET {varName} = {literalValue};");
                    }

                    GenerateUpsertLogic(sb, columns, whereColNames, variableMap, schema, tableName, out _);
                    sb.AppendLine();
                }
            }

            sb.AppendLine(string.Format(AppConstants.Templates.Header.EndMarker, request.TicketNumber.ToUpper()));
            sb.AppendLine(GenerateTemplateFooter(request.TicketNumber, true));

            return sb.ToString();
        }

        private void GenerateUpsertLogic(
            StringBuilder sb,
            List<TableColumnInfo> columns,
            List<string> whereColNames,
            Dictionary<string, string> variableMap,
            string schema,
            string tableName,
            out string printValueExpression)
        {
            var whereConditions = new List<string>();
            var insertCols = new List<string>();
            var insertVals = new List<string>();
            var updateSet = new List<string>();
            printValueExpression = "'Record'";

            foreach (var col in columns)
            {
                if (col.IsIdentity) continue;

                var varName = variableMap[col.ColumnName];

                // Prepare Insert
                insertCols.Add($"[{col.ColumnName}]");
                insertVals.Add(varName);

                // Prepare Where
                if (whereColNames.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    whereConditions.Add($"[{col.ColumnName}] = {varName}");
                    // First Where col determines print value
                    if (printValueExpression == "'Record'") printValueExpression = $"CAST({varName} AS VARCHAR(MAX))";
                }
                bool isCreated = col.ColumnName.Equals("CreatedDate", StringComparison.OrdinalIgnoreCase) || col.ColumnName.Equals("CreatedBy", StringComparison.OrdinalIgnoreCase);
                bool isManualId = ((col.IsPrimaryKey || col.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)) && !col.IsIdentity);

                if (!whereColNames.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase) && !isCreated && !isManualId)
                {
                    updateSet.Add($"[{col.ColumnName}] = {varName}");
                }
            }

            var whereClause = whereConditions.Any() ? string.Join(" AND ", whereConditions) : "1=0";
            var updateStatement = updateSet.Any()
                    ? $"    UPDATE [{schema}].[{tableName}] \n\t   SET {string.Join(",\n\t       ", updateSet)}\n\t WHERE {whereClause};"
                    : "    -- No updateable columns found";

            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [{schema}].[{tableName}] WITH (NOLOCK) WHERE {whereClause})");
            sb.AppendLine("BEGIN");
            sb.AppendLine($"    INSERT INTO [{schema}].[{tableName}] (\n\t{string.Join(",\n\t ", insertCols)})");
            sb.AppendLine($"    VALUES (\n\t{string.Join(",\n\t ", insertVals)});");
            sb.AppendLine($"    PRINT 'Inserted: ' + {printValueExpression};");
            sb.AppendLine("END");
            sb.AppendLine("ELSE");
            sb.AppendLine("BEGIN");
            if (updateSet.Any())
            {
                sb.AppendLine(updateStatement);
            }
            else
            {
                sb.AppendLine(updateStatement);
            }
            sb.AppendLine($"    PRINT 'Updated: ' + {printValueExpression};");
            sb.AppendLine("END");
        }

        private string GetValueLiteral(TableColumnInfo col, Dictionary<string, string> rowData, string schema, string tableName)
        {
            rowData.TryGetValue(col.ColumnName, out string rawVal);

            bool isManualId = ((col.IsPrimaryKey || col.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)) && !col.IsIdentity) 
                              && (rawVal == "MANUAL_ID" || string.IsNullOrEmpty(rawVal));
            bool isCreatedDate = col.ColumnName.Equals("CreatedDate", StringComparison.OrdinalIgnoreCase);
            bool isCreatedBy = col.ColumnName.Equals("CreatedBy", StringComparison.OrdinalIgnoreCase);
            bool isUpdatedDate = col.ColumnName.Equals("UpdatedDate", StringComparison.OrdinalIgnoreCase);
            bool isUpdatedBy = col.ColumnName.Equals("UpdatedBy", StringComparison.OrdinalIgnoreCase);

            if (isManualId)
            {
                return $"(SELECT ISNULL(MAX([{col.ColumnName}]), 0) + 1 FROM [{schema}].[{tableName}] WITH (NOLOCK))";
            }
            else if (isCreatedDate || isUpdatedDate)
            {
                return "GETDATE()";
            }
            else if (isCreatedBy || isUpdatedBy)
            {
                return "SYSTEM_USER";
            }
            else
            {
                if (!string.IsNullOrEmpty(rawVal) && !rawVal.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                {
                    if (new[] { "GETDATE()", "SYSTEM_USER", "NEWID()" }.Contains(rawVal?.ToUpper()))
                        return rawVal;
                    else
                        return FormatValueForSql(rawVal, col.DataType);
                }
            }
            return "NULL";
        }

        private string SanitizeVariableName(string columnName)
        {
            // Replace spaces and special chars to make valid T-SQL variable
            return System.Text.RegularExpressions.Regex.Replace(columnName, @"[^a-zA-Z0-9_]", "_");
        }

        private string GetSqlDataType(TableColumnInfo col)
        {
            var type = col.DataType.ToUpper();
            switch (type)
            {
                case "VARCHAR":
                case "NVARCHAR":
                case "CHAR":
                case "NCHAR":
                case "VARBINARY":
                case "BINARY":
                    var len = col.MaxLength == -1 ? "MAX" : col.MaxLength.ToString();
                    // Some providers return -1 for MAX. Others might return large number? 
                    // Let's assume -1 or 4000/8000 logic handled by schema service, but here we trust MaxLength.
                    // Actually, for nvarchar, MaxLength often is bytes (so *2 chars). 
                    // But if it comes from our Schema service, let's assume it's correct attribute.
                    // If -1, assume MAX.
                    // If Null, default to MAX just in case? No, default to something safe like 255?
                    if (col.MaxLength.HasValue && col.MaxLength != -1) return $"{type}({col.MaxLength})";
                    return $"{type}(MAX)";

                case "DECIMAL":
                case "NUMERIC":
                    var p = col.Precision ?? 18;
                    var s = col.Scale ?? 0;
                    return $"{type}({p},{s})";

                default:
                    return type;
            }
        }

        public async Task<ValidationResult> ValidateTemplateAsync(TemplateRequest request)
        {
            var result = new ValidationResult();

            // Basic validation
            if (string.IsNullOrEmpty(request.ServerName))
                result.Errors.Add("Server is required");

            if (string.IsNullOrEmpty(request.DatabaseName))
                result.Errors.Add("Database is required");

            if (string.IsNullOrEmpty(request.TicketNumber))
                result.Errors.Add("Ticket number is required");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(request.TicketNumber, @"^[A-Z]+-\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                result.Warnings.Add("Ticket number format may be incorrect (expected format: ABC-12345)");

            // Template-specific validation
            // Template-specific validation
            var type = request.TemplateType?.ToLower() ?? "";
            
            switch (type)
            {


                case "insertupdate":
                    if (string.IsNullOrEmpty(request.TableName))
                        result.Errors.Add("Table name is required");
                    if ((request.ColumnValues == null || !request.ColumnValues.Any()) && string.IsNullOrEmpty(request.CsvContent))
                         result.Errors.Add("At least one column value or a CSV file is required");
                    
                    if (string.IsNullOrEmpty(request.CsvContent) && (request.SelectedWhereColumns == null || !request.SelectedWhereColumns.Any()))
                        result.Errors.Add("Please select at least one column for the WHERE condition");
                    break;

                case "bulkinsert":
                    if (string.IsNullOrEmpty(request.TableName))
                        result.Errors.Add("Table name is required");
                    if (string.IsNullOrEmpty(request.CsvContent))
                         result.Errors.Add("A CSV file is required for Bulk Insert");
                    if (request.SelectedWhereColumns == null || !request.SelectedWhereColumns.Any())
                        result.Warnings.Add("No WHERE columns selected. Using Primary Key or Identity if available.");
                    break;

                default:
                    // For SP, Permission, Data
                    if (string.IsNullOrEmpty(request.ObjectNames))
                        result.Errors.Add("SQL object names are required");
                    break;
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task<LivePreviewResponse> GeneratePreviewAsync(TemplateRequest request)
        {
            try
            {
                string preview = "";
                var warnings = new List<string>();

                // Generate a preview (first 50 lines)
                var type = request.TemplateType?.ToLower() ?? "";
                
                switch (type)
                {


                    case "insertupdate":
                        if (!string.IsNullOrEmpty(request.TableName) && (request.ColumnValues != null && request.ColumnValues.Any() || !string.IsNullOrEmpty(request.CsvContent)))
                        {
                             // Preview logic uses fallback or CSV
                             if (!string.IsNullOrEmpty(request.CsvContent))
                                 preview = await GenerateUnifiedUpsertScript(request, isCsv: true);
                             else
                                 preview = await GenerateUnifiedUpsertScript(request, isCsv: false);
                        }
                        else
                        {
                            warnings.Add("Enter table name and column values (or CSV) to see preview");
                        }
                        break;

                    case "bulkinsert":
                        if (!string.IsNullOrEmpty(request.TableName) && !string.IsNullOrEmpty(request.CsvContent))
                        {
                             preview = await GenerateUnifiedUpsertScript(request, isCsv: true);
                        }
                        else
                        {
                            warnings.Add("Enter table name and upload CSV to see preview");
                        }
                        break;

                    default:
                        if (!string.IsNullOrEmpty(request.ObjectNames))
                        {
                            preview = await GenerateSqlTemplateAsync(request);
                        }
                        break;
                }

                // Limit preview to first 50 lines
                var lines = preview.Split('\n');
                var previewLines = lines.Take(50).ToArray();
                var limitedPreview = string.Join('\n', previewLines);

                if (lines.Length > 50)
                {
                    limitedPreview += "\n\n-- ... (preview truncated, showing first 50 lines) ...";
                    warnings.Add($"Full template has {lines.Length} lines. Preview shows first 50 lines.");
                }

                return new LivePreviewResponse
                {
                    Success = true,
                    Preview = limitedPreview,
                    LineCount = lines.Length,
                    Warnings = warnings
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating live preview");
                return new LivePreviewResponse
                {
                    Success = false,
                    Preview = "Error generating preview",
                    Warnings = new List<string> { "An error occurred while generating the preview" }
                };
            }
        }



        private async Task<string> GenerateSqlTemplateAsync(TemplateRequest request)
        {
            _logger.LogInformation("DEBUG CHECK: Running updated GenerateSqlTemplateAsync logic");
            var sb = new StringBuilder();
            var objectNames = ParseObjectNames(request.ObjectNames);

            sb.AppendLine(string.Format(AppConstants.Templates.Header.Common, request.DatabaseName));
            sb.AppendLine(string.Format(AppConstants.Templates.Header.StartMarker, request.TicketNumber.ToUpper()));

            bool hasValidObjects = false;

            foreach (var name in objectNames)
            {
                var cleanName = name.Trim();
                sb.AppendLine("GO");

                try
                {
                    var objectInfo = await _schemaService.GetObjectInfoAsync(request.ServerName, request.DatabaseName, cleanName);
                    if (!(objectInfo != null && objectInfo.Type == "SPVIEW"))
                    {
                        if (objectInfo != null)
                        {
                            hasValidObjects = true;
                            sb.AppendLine(GenerateDropStatement(objectInfo));
                            sb.AppendLine(objectInfo.Definition);
                        }
                        else
                        {
                            sb.AppendLine($"-- WARNING: Object {cleanName} not found on {request.DatabaseName}");
                        }
                    }
                    else
                    {
                        hasValidObjects = true;
                        sb.AppendLine(objectInfo.Definition);
                    }
                }
                catch (Exception objEx)
                {
                     sb.AppendLine($"-- ERROR retrieving object {cleanName}: {objEx.Message}");
                     _logger.LogError(objEx, $"Error retrieving object {cleanName}");
                }
                sb.AppendLine("GO\n");
            }

            sb.AppendLine(string.Format(AppConstants.Templates.Header.EndMarker, request.TicketNumber.ToUpper()));
            sb.AppendLine(GenerateTemplateFooter(request.TicketNumber, hasValidObjects));

            if (!hasValidObjects)
            {
                var cleanNames = string.Join(", ", objectNames);
                throw new Exception($"No stored procedure or object found with name(s): {cleanNames}. Please check the name and database.");
            }

            return sb.ToString();
        }

        private List<string> ParseObjectNames(string objectNames)
        {
            if (string.IsNullOrWhiteSpace(objectNames)) return new List<string>();
            return objectNames.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(n => n.Trim())
                              .Where(n => !string.IsNullOrWhiteSpace(n))
                              .ToList();
        }

        private (string schema, string tableName) ParseTableName(string fullTableName)
        {
            if (string.IsNullOrEmpty(fullTableName))
                return ("dbo", "");

            fullTableName = fullTableName.Replace("[", "").Replace("]", "");

            if (fullTableName.Contains('.'))
            {
                var parts = fullTableName.Split('.');
                return (parts[0], parts[1]);
            }

            return ("dbo", fullTableName);
        }

        private string FormatValueForSql(string value, string dataType)
        {
            if (string.IsNullOrEmpty(value))
                return "NULL";

            dataType = dataType.ToLower();

            if (dataType.Contains("char") || dataType.Contains("text") || dataType.Contains("xml"))
            {
                return $"N'{value.Replace("'", "''")}'";
            }
            else if (dataType.Contains("date") || dataType.Contains("time"))
            {
                if (DateTime.TryParse(value, out DateTime dateValue))
                {
                    return $"'{dateValue:yyyy-MM-dd HH:mm:ss}'";
                }
                return $"'{value}'";
            }
            else if (dataType == "bit")
            {
                return value == "1" || value.ToLower() == "true" ? "1" : "0";
            }
            else if (dataType.Contains("int") || dataType.Contains("decimal") || dataType.Contains("numeric") || dataType.Contains("float") || dataType.Contains("real"))
            {
                return value;
            }
            else
            {
                return $"'{value.Replace("'", "''")}'";
            }
        }

        private string GenerateDropStatement(SqlObjectInfo obj)
        {
            return obj.Type switch
            {
                "PROCEDURE" => string.Format(AppConstants.Templates.Snippets.DropProcedure, obj.Name),
                "FUNCTION" => string.Format(AppConstants.Templates.Snippets.DropFunction, obj.Name),
                "VIEW" => string.Format(AppConstants.Templates.Snippets.DropView, obj.Name),
                _ => $"-- No drop statement generated for {obj.Type} {obj.Name}"
            };
        }

        private string GenerateCommonSqlHeader(string databaseName)
        {
            return string.Format(AppConstants.Templates.Header.Common, databaseName);
        }

        private string GenerateTemplateFooter(string ticketNumber, bool hasActions)
        {
            return hasActions ? "" : $"-- No actions performed for ticket {ticketNumber}";
        }

        private string GenerateTransactionStart(string ticketNumber)
        {
            var tranName = $"TRAN_{ticketNumber.Replace("-", "_")}";
            return string.Format(AppConstants.Templates.Transaction.Begin, tranName);
        }

        private string GenerateTransactionEnd(string ticketNumber)
        {
            var tranName = $"TRAN_{ticketNumber.Replace("-", "_")}";
            return string.Format(AppConstants.Templates.Transaction.Commit, tranName);
        }

        private bool IsNumericType(string dataType)
        {
            if (string.IsNullOrEmpty(dataType)) return false;
            var dt = dataType.ToLower();
            return dt.Contains("int") || 
                   dt.Contains("decimal") || 
                   dt.Contains("numeric") || 
                   dt.Contains("float") || 
                   dt.Contains("real") || 
                   dt.Contains("money");
        }

        private bool IsNumericValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return double.TryParse(value, out _);
        }
    }
}

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Template_Builder.Models.Entities;
using Template_Builder.Services.Interfaces;
using Template_Builder.Constants;

namespace Template_Builder.Services.Implementations
{
    public class SqlSchemaService : ISqlSchemaService
    {
        private readonly ISqlConnectionService _connectionService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SqlSchemaService> _logger;

        public SqlSchemaService(ISqlConnectionService connectionService, IMemoryCache cache, ILogger<SqlSchemaService> logger)
        {
            _connectionService = connectionService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<string>> GetDatabasesAsync(string serverName)
        {
            var databases = new List<string>();

            try
            {
                var connectionString = _connectionService.GetConnectionString(serverName);
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(AppConstants.SqlQueries.GetDatabases, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching databases for server: {ServerName}", serverName);
            }

            return databases;
        }

        public async Task<List<TableColumnInfo>> GetTableColumnsAsync(string serverName, string databaseName, string tableName)
        {
            var cacheKey = $"Columns_{serverName}_{databaseName}_{tableName}";
            if (_cache.TryGetValue(cacheKey, out List<TableColumnInfo> cachedColumns))
            {
                _logger.LogInformation("Returning table columns from cache for: {TableName}", tableName);
                return cachedColumns;
            }

            var columns = new List<TableColumnInfo>();

            try
            {
                var connectionString = _connectionService.GetConnectionString(serverName, databaseName);
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var (schema, pureTableName) = ParseTableName(tableName);

                using var command = new SqlCommand(AppConstants.SqlQueries.GetTableColumns, connection);
                command.Parameters.AddWithValue("@Schema", schema);
                command.Parameters.AddWithValue("@TableName", pureTableName);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var isIdentity = reader.GetBoolean(6);
                    var isComputed = reader.GetBoolean(7);

                    // Skip identity and computed columns
                    if (!isIdentity && !isComputed)
                    {
                        columns.Add(new TableColumnInfo
                        {
                            ColumnName = reader.GetString(0),
                            DataType = reader.GetString(1),
                            MaxLength = reader.IsDBNull(2) ? (int?)null : reader.GetInt16(2),
                            Precision = reader.IsDBNull(3) ? (byte?)null : reader.GetByte(3),
                            Scale = reader.IsDBNull(4) ? (byte?)null : reader.GetByte(4),
                            IsNullable = reader.GetBoolean(5) ? "YES" : "NO",
                            IsIdentity = isIdentity,
                            IsComputed = isComputed,
                            IsPrimaryKey = reader.GetInt32(8) == 1
                        });
                    }
                }

                if (columns.Any())
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(2));
                    
                    _cache.Set(cacheKey, columns, cacheEntryOptions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching table columns for table: {TableName}", tableName);
            }

            return columns;
        }

        public async Task<SqlObjectInfo> GetObjectInfoAsync(string serverName, string databaseName, string objectName)
        {
            try
            {
                var connectionString = _connectionService.GetConnectionString(serverName, databaseName);
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Try different naming formats
                var attempts = new[]
                {
                    objectName,
                    AddBrackets(objectName),
                    StripBrackets(objectName),
                    EnsureFullQualifiedName(objectName)
                }.Distinct();

                foreach (var name in attempts)
                {
                    var obj = await FindObjectAsync(connection, name);
                    if (obj != null)
                        return obj;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting object info for {ObjectName}", objectName);
                return null;
            }
        }

        public async Task<bool> TableExistsAsync(string serverName, string databaseName, string tableName)
        {
            try
            {
                var connectionString = _connectionService.GetConnectionString(serverName, databaseName);
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var (schema, pureTableName) = ParseTableName(tableName);

                using var command = new SqlCommand(AppConstants.SqlQueries.TableExists, connection);
                command.Parameters.AddWithValue("@Schema", schema);
                command.Parameters.AddWithValue("@TableName", pureTableName);

                var result = (int)await command.ExecuteScalarAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if table exists: {TableName}", tableName);
                return false;
            }
        }

        private async Task<SqlObjectInfo> FindObjectAsync(SqlConnection connection, string name)
        {
            try
            {
                var (schema, tableName) = ParseTableName(name);
                var cleanName = tableName.Replace("[", "").Replace("]", "");
                var cleanSchema = schema.Replace("[", "").Replace("]", "");

                var objectTypes = new[]
                {
                    new { Type = "PROCEDURE", Query = AppConstants.SqlQueries.FindProcedure },
                    new { Type = "FUNCTION", Query = AppConstants.SqlQueries.FindFunction },
                    new { Type = "VIEW", Query = AppConstants.SqlQueries.FindView },
                    new { Type = "SPVIEW", Query = AppConstants.SqlQueries.FindSpView }
                };

                foreach (var objType in objectTypes)
                {
                    using var command = new SqlCommand(objType.Query, connection);
                    command.Parameters.AddWithValue("@name", name);
                    if (objType.Type == "PROCEDURE" || objType.Type == "FUNCTION")
                    {
                        command.Parameters.AddWithValue("@cleanName", cleanName);
                        command.Parameters.AddWithValue("@schema", cleanSchema);
                    }
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var objName = reader.GetString(0);
                        var objSchema = reader.GetString(1);
                        var objectId = reader.GetValue(2);
                        
                        string definition;
                        if (!reader.IsDBNull(3))
                        {
                            definition = reader.GetString(3);
                        }
                        else
                        {
                            definition = await GetDefinitionFromObjectAsync(connection, objectId);
                        }
                        reader.Close();

                        if (string.IsNullOrEmpty(definition))
                        {
                            continue;
                        }

                        return new SqlObjectInfo
                        {
                            Name = $"[{objSchema}].[{objName}]",
                            Type = objType.Type,
                            Definition = definition + "\nGO",
                            Schema = objSchema
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding object: {ObjectName}", name);
                return null;
            }
        }

        private async Task<string> GetDefinitionFromObjectAsync(SqlConnection connection, object objectId)
        {
            try
            {
                using var command = new SqlCommand(AppConstants.SqlQueries.GetObjectDefinition, connection);
                command.Parameters.AddWithValue("@objectId", objectId);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting object definition for ID: {ObjectId}", objectId);
                return null;
            }
        }

        private string AddBrackets(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            name = name.Trim();
            name = name.Replace("[", "").Replace("]", "");
            if (!name.StartsWith("[") && !name.EndsWith("]"))
            {
                if (name.Contains('.'))
                {
                    var parts = name.Split('.');
                    return $"[{parts[0]}].[{parts[1]}]";
                }
                return $"[{name}]";
            }
            return name;
        }

        private string StripBrackets(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Replace("[", "").Replace("]", "");
        }

        private string EnsureFullQualifiedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            if (!name.Contains('.') && !name.StartsWith("["))
            {
                return $"[dbo].[{name}]";
            }

            return name;
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
    }
}

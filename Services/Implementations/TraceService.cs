using System.Data;
using Microsoft.Data.SqlClient;
using Template_Builder.Services.Interfaces;

namespace Template_Builder.Services.Implementations
{
    public class TraceService : ITraceService
    {
        private readonly ISqlConnectionService _connectionService;
        private readonly ILogger<TraceService> _logger;

        public TraceService(ISqlConnectionService connectionService, ILogger<TraceService> logger)
        {
            _connectionService = connectionService;
            _logger = logger;
        }

        public async Task<List<TraceResult>> GetTraceDataAsync(string serverName, string? databaseName, string? spFilter, string traceType = "query")
        {
            var results = new List<TraceResult>();

            try
            {
                var connectionString = _connectionService.GetConnectionString(serverName, "master");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    string sql;

                    if (traceType == "procedures")
                    {
                        // SP Query: focusing on SP execution stats
                         sql = @"
SELECT TOP 50
    'SP Execution' AS EventType,
    OBJECT_NAME(ps.object_id, ps.database_id) AS textData_Name,
    DB_NAME(ps.database_id) AS DatabaseName,
    ps.last_elapsed_time / 1000 AS Duration,
    ps.last_execution_time AS StartTime,
    ps.last_logical_reads AS Reads,
    ps.last_logical_writes AS Writes,
    ps.last_worker_time / 1000 AS CPU,
    'Pars: ' + COALESCE(t.text, '') AS FullText
FROM sys.dm_exec_procedure_stats ps
CROSS APPLY sys.dm_exec_sql_text(ps.sql_handle) t
WHERE ps.last_execution_time > DATEADD(SECOND, -30, GETDATE())
";
                    }
                    else if (traceType == "long_running")
                    {
                        // Long Running Analysis (Last 1 hour, Top Duration)
                        sql = @"
SELECT TOP 50
    'Long Query' AS EventType,
    COALESCE(t.text, 'Encrypted') AS textData_Name,
    DB_NAME(t.dbid) AS DatabaseName,
    qs.last_elapsed_time / 1000 AS Duration,
    qs.last_execution_time AS StartTime,
    qs.last_logical_reads AS Reads,
    qs.last_logical_writes AS Writes,
    qs.last_worker_time / 1000 AS CPU,
    '' AS FullText
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) t
WHERE qs.last_execution_time > DATEADD(HOUR, -1, GETDATE())
AND qs.last_elapsed_time > 1000000 -- > 1s
ORDER BY qs.last_elapsed_time DESC
";
                    }
                    else if (traceType == "high_cpu")
                    {
                        // High CPU Analysis (Last 1 hour)
                        sql = @"
SELECT TOP 50
    'High CPU' AS EventType,
    COALESCE(t.text, 'Encrypted') AS textData_Name,
    DB_NAME(t.dbid) AS DatabaseName,
    qs.last_elapsed_time / 1000 AS Duration,
    qs.last_execution_time AS StartTime,
    qs.last_logical_reads AS Reads,
    qs.last_logical_writes AS Writes,
    qs.last_worker_time / 1000 AS CPU,
    '' AS FullText
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) t
WHERE qs.last_execution_time > DATEADD(HOUR, -1, GETDATE())
ORDER BY qs.last_worker_time DESC
";
                    }
                    else if (traceType == "high_reads")
                    {
                        // High IO Analysis (Last 1 hour)
                        sql = @"
SELECT TOP 50
    'High reads' AS EventType,
    COALESCE(t.text, 'Encrypted') AS textData_Name,
    DB_NAME(t.dbid) AS DatabaseName,
    qs.last_elapsed_time / 1000 AS Duration,
    qs.last_execution_time AS StartTime,
    qs.last_logical_reads AS Reads,
    qs.last_logical_writes AS Writes,
    qs.last_worker_time / 1000 AS CPU,
    '' AS FullText
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) t
WHERE qs.last_execution_time > DATEADD(HOUR, -1, GETDATE())
ORDER BY qs.last_logical_reads DESC
";
                    }
                    else
                    {
                        // Default Query Query (Live)
                        sql = @"
SELECT TOP 50
    'Batch Completed' AS EventType,
    COALESCE(t.text, 'Encrypted') AS textData_Name,
    DB_NAME(t.dbid) AS DatabaseName,
    qs.last_elapsed_time / 1000 AS Duration,
    qs.last_execution_time AS StartTime,
    qs.last_logical_reads AS Reads,
    qs.last_logical_writes AS Writes,
    qs.last_worker_time / 1000 AS CPU,
    '' AS FullText
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) t
WHERE qs.last_execution_time > DATEADD(SECOND, -10, GETDATE())
";
                    }

                    // Apply Filters
                    if (!string.IsNullOrEmpty(databaseName))
                    {
                         sql += traceType == "procedures" 
                             ? " AND DB_NAME(ps.database_id) = @DatabaseName"
                             : " AND DB_NAME(t.dbid) = @DatabaseName";
                    }

                    if (!string.IsNullOrEmpty(spFilter))
                    {
                         sql += traceType == "procedures"
                             ? " AND OBJECT_NAME(ps.object_id, ps.database_id) LIKE '%' + @SpFilter + '%'"
                             : " AND t.text LIKE '%' + @SpFilter + '%'";
                    }
                    
                    sql += traceType == "procedures" 
                        ? " ORDER BY ps.last_execution_time DESC"
                        : " ORDER BY qs.last_execution_time DESC";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        if (!string.IsNullOrEmpty(databaseName))
                        {
                            command.Parameters.AddWithValue("DatabaseName", databaseName);
                        }
                        if (!string.IsNullOrEmpty(spFilter))
                        {
                            command.Parameters.AddWithValue("SpFilter", spFilter);
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var textData = reader["textData_Name"]?.ToString() ?? "";
                                var fullText = reader["FullText"]?.ToString() ?? "";
                                
                                if (traceType == "procedures" && !string.IsNullOrEmpty(textData))
                                {
                                   // Keep SP Name as primary text
                                }

                                results.Add(new TraceResult
                                {
                                    EventType = reader["EventType"]?.ToString() ?? "Unknown",
                                    TextData = textData, // SP Name or SQL Text
                                    ApplicationName = "SQL App",
                                    Database = reader["DatabaseName"]?.ToString() ?? "Unknown",
                                    Duration = reader["Duration"]?.ToString() ?? "0",
                                    StartTime = Convert.ToDateTime(reader["StartTime"]),
                                    LoginName = "N/A",
                                    SPID = "0",
                                    Reads = reader["Reads"]?.ToString() ?? "0",
                                    Writes = reader["Writes"]?.ToString() ?? "0"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching trace data from {Server}", serverName);
            }

            return results;
        }
    }
}

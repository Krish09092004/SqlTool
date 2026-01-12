using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using Template_Builder.Models.Entities;
using Template_Builder.Services.Interfaces;

namespace Template_Builder.Services.Implementations
{
    public class SqlConnectionService : ISqlConnectionService
    {
        private readonly List<ServerInfo> _servers;
        private readonly ILogger<SqlConnectionService> _logger;

        public SqlConnectionService(IOptions<List<ServerInfo>> servers, ILogger<SqlConnectionService> logger)
        {
            _servers = servers.Value ?? new List<ServerInfo>();
            _logger = logger;
        }

        public List<ServerInfo> GetServers()
        {
            return _servers.Where(s => s.IsActive).ToList();
        }

        public async Task<bool> TestConnectionAsync(string serverName, string databaseName = "master")
        {
            try
            {
                var connectionString = GetConnectionString(serverName, databaseName);
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test failed for server: {ServerName}", serverName);
                return false;
            }
        }

        public string GetConnectionString(string serverName, string databaseName = "")
        {
            var server = _servers.FirstOrDefault(s => s.Name == serverName);
            if (server == null)
            {
                throw new ArgumentException($"Server '{serverName}' not found in configuration");
            }

            var builder = new SqlConnectionStringBuilder(server.ConnectionString);
            
            if (!string.IsNullOrEmpty(databaseName))
            {
                builder.InitialCatalog = databaseName;
            }

            return builder.ConnectionString;
        }
    }
}
using Template_Builder.Models.Entities;

namespace Template_Builder.Services.Interfaces
{
    public interface ISqlConnectionService
    {
        List<ServerInfo> GetServers();
        Task<bool> TestConnectionAsync(string serverName, string databaseName = "master");
        string GetConnectionString(string serverName, string databaseName = "");
    }
}
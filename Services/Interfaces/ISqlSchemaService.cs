using Template_Builder.Models.Entities;

namespace Template_Builder.Services.Interfaces
{
    public interface ISqlSchemaService
    {
        Task<List<string>> GetDatabasesAsync(string serverName);
        Task<List<TableColumnInfo>> GetTableColumnsAsync(string serverName, string databaseName, string tableName);
        Task<SqlObjectInfo> GetObjectInfoAsync(string serverName, string databaseName, string objectName);
        Task<bool> TableExistsAsync(string serverName, string databaseName, string tableName);
    }

    public class SqlObjectInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Definition { get; set; }
        public string Schema { get; set; }
    }
}
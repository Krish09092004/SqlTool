using Template_Builder.Models.Entities;

namespace Template_Builder.Services.Interfaces
{
    public interface ITraceService
    {
        Task<List<TraceResult>> GetTraceDataAsync(string serverName, string? databaseName, string? spFilter, string traceType = "query");
    }

    public class TraceResult
    {
        public string EventType { get; set; }
        public string TextData { get; set; }
        public string ApplicationName { get; set; }
        public string Database { get; set; }
        public string Duration { get; set; }
        public DateTime StartTime { get; set; }
        public string LoginName { get; set; }
        public string SPID { get; set; }
        public string Reads { get; set; }
        public string Writes { get; set; }
    }
}

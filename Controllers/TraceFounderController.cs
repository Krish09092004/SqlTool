using Microsoft.AspNetCore.Mvc;
using Template_Builder.Models.Entities;
using Template_Builder.Models.ViewModels;
using Template_Builder.Services.Interfaces;

namespace Template_Builder.Controllers
{
    public class TraceFounderController : Controller
    {
        private readonly ISqlConnectionService _connectionService;
        private readonly ISqlSchemaService _schemaService;
        private readonly ITraceService _traceService;
        private readonly ILogger<TraceFounderController> _logger;

        public TraceFounderController(
            ISqlConnectionService connectionService,
            ISqlSchemaService schemaService,
            ITraceService traceService,
            ILogger<TraceFounderController> logger)
        {
            _connectionService = connectionService;
            _schemaService = schemaService;
            _traceService = traceService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var servers = _connectionService.GetServers();
            // Reusing TemplateViewModel as it already has Servers list. 
            // In a larger app, we'd make a dedicated TraceViewModel.
            return View(new TemplateViewModel
            {
                Servers = servers
            });
        }

        [HttpGet]
        public async Task<JsonResult> GetDatabases(string serverName)
        {
            try
            {
                if (string.IsNullOrEmpty(serverName)) return Json(new List<string>());
                var databases = await _schemaService.GetDatabasesAsync(serverName);
                return Json(databases.OrderBy(db => db).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching databases for {Server}", serverName);
                return Json(new List<string>());
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetTraceData(string serverName, string? databaseName, string? spFilter, string traceType = "query")
        {
            try
            {
                if (string.IsNullOrEmpty(serverName)) return Json(new List<object>());
                
                var data = await _traceService.GetTraceDataAsync(serverName, databaseName, spFilter, traceType);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching trace data");
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}

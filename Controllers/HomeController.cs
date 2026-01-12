using Microsoft.AspNetCore.Mvc;
using Template_Builder.Models.Entities;
using Template_Builder.Models.ViewModels;
using Template_Builder.Services.Interfaces;
using Template_Builder.Constants;

namespace Template_Builder.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISqlConnectionService _connectionService;
        private readonly ISqlSchemaService _schemaService;
        private readonly ITemplateTypeService _templateTypeService;
        private readonly ISqlTemplateService _templateService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ISqlConnectionService connectionService,
            ISqlSchemaService schemaService,
            ITemplateTypeService templateTypeService,
            ISqlTemplateService templateService,
            ILogger<HomeController> logger)
        {
            _connectionService = connectionService;
            _schemaService = schemaService;
            _templateTypeService = templateTypeService;
            _templateService = templateService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                var servers = _connectionService.GetServers();
                var templateTypes = _templateTypeService.GetTemplateTypes();

                return View(new TemplateViewModel
                {
                    Servers = servers,
                    AvailableTemplateTypes = templateTypes,
                    ShowLivePreview = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, AppConstants.LogMessages.LoadConfigError);
                TempData["Error"] = AppConstants.ErrorMessages.LoadConfigError;
                return View(new TemplateViewModel { Servers = new List<ServerInfo>() });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public JsonResult GetTemplateTypes()
        {
            var templateTypes = _templateTypeService.GetTemplateTypes();
            return Json(templateTypes);
        }

        [HttpGet]
        public JsonResult GetTemplateType(string id)
        {
            var templateType = _templateTypeService.GetTemplateType(id);
            return Json(templateType);
        }

        [HttpGet]
        public async Task<JsonResult> GetDatabases(string serverName)
        {
            try
            {
                if (string.IsNullOrEmpty(serverName))
                {
                    return Json(new List<string>());
                }

                var databases = await _schemaService.GetDatabasesAsync(serverName);
                return Json(databases.OrderBy(db => db).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, AppConstants.LogMessages.FetchDatabasesError, serverName);
                return Json(new List<string>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> ValidateTemplate([FromBody] TemplateRequest request)
        {
            try
            {
                var result = await _templateService.ValidateTemplateAsync(request);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, AppConstants.LogMessages.ValidateTemplateError);
                return Json(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { AppConstants.ErrorMessages.GenericValidation }
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> GetLivePreview([FromBody] TemplateRequest request)
        {
            try
            {
                var result = await _templateService.GeneratePreviewAsync(request);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, AppConstants.LogMessages.GeneratePreviewError);
                return Json(new LivePreviewResponse
                {
                    Success = false,
                    Preview = AppConstants.ErrorMessages.PreviewGenerationError,
                    Warnings = new List<string> { AppConstants.ErrorMessages.PreviewGenerationWarning }
                });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetTableColumns(string serverName, string databaseName, string tableName)
        {
            try
            {
                if (string.IsNullOrEmpty(serverName) || string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(tableName))
                {
                    return Json(new List<TableColumnInfo>());
                }

                var columns = await _schemaService.GetTableColumnsAsync(serverName, databaseName, tableName);
                return Json(columns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, AppConstants.LogMessages.FetchColumnsError, tableName);
                return Json(new List<TableColumnInfo>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateTemplate([FromBody] TemplateRequest request)
        {
            try
            {
                var result = await _templateService.GenerateTemplateAsync(request);

                if (!result.Success)
                {
                    return Json(new { success = false, error = result.ErrorMessage });
                }

                return Json(new { success = true, template = result.Template });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, AppConstants.LogMessages.GenerateTemplateError);
                return Json(new { success = false, error = string.Format(AppConstants.ErrorMessages.TemplateGenerationError, ex.Message) });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadTemplate([FromBody] TemplateRequest request)
        {
            try
            {
                var result = await _templateService.GenerateTemplateAsync(request);

                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    return RedirectToAction("Index");
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(result.Template);
                var fileName = $"#{request.TicketNumber.ToUpper()}_{request.TemplateType}.sql";

                return File(bytes, AppConstants.MimeTypes.Sql, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, AppConstants.LogMessages.DownloadTemplateError);
                TempData["Error"] = AppConstants.ErrorMessages.DownloadError;
                return RedirectToAction("Index");
            }
        }
    }
}
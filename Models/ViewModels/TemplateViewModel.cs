using Template_Builder.Models.Entities;
using Template_Builder.Models.Enums;

namespace Template_Builder.Models.ViewModels
{
    public class TemplateViewModel
    {
        public List<ServerInfo> Servers { get; set; } = new();
        public List<TemplateTypeDefinition> AvailableTemplateTypes { get; set; } = new();
        public bool ShowLivePreview { get; set; } = true;

        // Form fields
        public string SelectedServer { get; set; }
        public string SelectedDatabase { get; set; }
        public TemplateType TemplateType { get; set; }
        public string TicketNumber { get; set; }
        public string ObjectNamesText { get; set; }



        // Table data fields
        public string TableName { get; set; }

        // Results
        public string GeneratedTemplate { get; set; }
        public bool HasGeneratedTemplate => !string.IsNullOrEmpty(GeneratedTemplate);

        // UI State
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, string> FieldErrors { get; set; } = new();
    }

    public class LivePreviewResponse
    {
        public bool Success { get; set; }
        public string Preview { get; set; }
        public int LineCount { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class TemplateGenerationResult
    {
        public bool Success { get; set; }
        public string Template { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
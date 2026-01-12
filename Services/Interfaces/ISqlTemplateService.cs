using Template_Builder.Models.Entities;
using Template_Builder.Models.ViewModels;

namespace Template_Builder.Services.Interfaces
{
    public interface ISqlTemplateService
    {
        Task<TemplateGenerationResult> GenerateTemplateAsync(TemplateRequest request);
        Task<ValidationResult> ValidateTemplateAsync(TemplateRequest request);
        Task<LivePreviewResponse> GeneratePreviewAsync(TemplateRequest request);



    }
}
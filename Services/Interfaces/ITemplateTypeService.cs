using Template_Builder.Models.Entities;

namespace Template_Builder.Services.Interfaces
{
    public interface ITemplateTypeService
    {
        List<TemplateTypeDefinition> GetTemplateTypes();
        TemplateTypeDefinition GetTemplateType(string id);
    }
}
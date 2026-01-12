using Template_Builder.Models.Entities;
using Template_Builder.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Template_Builder.Services.Implementations
{
    public class TemplateTypeService : ITemplateTypeService
    {
        private static List<TemplateTypeDefinition> _templateTypes;
        private readonly IConfiguration _configuration;

        public TemplateTypeService(IConfiguration configuration)
        {
            _configuration = configuration;
            InitializeTemplateTypes();
        }

        public List<TemplateTypeDefinition> GetTemplateTypes()
        {
            return _templateTypes;
        }

        public TemplateTypeDefinition GetTemplateType(string id)
        {
            return _templateTypes.FirstOrDefault(t => t.Id == id);
        }

        private void InitializeTemplateTypes()
        {
            if (_templateTypes != null) return;

            // Try to load from configuration first
            var typesFromConfig = _configuration.GetSection("TemplateTypes").Get<List<TemplateTypeDefinition>>();
            if (typesFromConfig != null && typesFromConfig.Any())
            {
                _templateTypes = typesFromConfig;
                return;
            }

            // Fallback to hardcoded defaults if config is missing
            _templateTypes = new List<TemplateTypeDefinition>
            {
                new TemplateTypeDefinition
                {
                    Id = "config",
                    Name = "Configuration Template",
                    Description = "Manage application configuration settings",
                    Icon = "settings",
                    BadgeColor = "badge-config",
                    RequiresObjectNames = false,
                    RequiresTableInfo = false,
                    Fields = new List<TemplateField>
                    {
                        new TemplateField { Name = "ConfigCode", Label = "Config Code", Type = "text", Required = true, Placeholder = "e.g. SITE_NAME, MAX_USERS" },
                        new TemplateField { Name = "ConfigValue1", Label = "Config Value 1", Type = "text", Required = true, Placeholder = "e.g. MySite, 100" },
                        new TemplateField { Name = "ConfigValue2", Label = "Config Value 2", Type = "text", Required = false, Placeholder = "Optional second value" },
                        new TemplateField { Name = "Description", Label = "Description", Type = "text", Required = false, Placeholder = "Configuration description" }
                    }
                },
                new TemplateTypeDefinition
                {
                    Id = "permission",
                    Name = "Permission Template",
                    Description = "Grant or revoke database permissions",
                    Icon = "lock",
                    BadgeColor = "badge-permission",
                    RequiresObjectNames = true,
                    RequiresTableInfo = false
                },
                new TemplateTypeDefinition
                {
                    Id = "data",
                    Name = "Data Template",
                    Description = "Manage database objects and definitions",
                    Icon = "database",
                    BadgeColor = "badge-data",
                    RequiresObjectNames = true,
                    RequiresTableInfo = false
                },
                new TemplateTypeDefinition
                {
                    Id = "insertupdate",
                    Name = "Insert/Update Data Template",
                    Description = "Insert or update table records dynamically",
                    Icon = "table",
                    BadgeColor = "badge-insertupdate",
                    RequiresObjectNames = false,
                    RequiresTableInfo = true,
                    Fields = new List<TemplateField>
                    {
                        new TemplateField { Name = "TableName", Label = "Table Name", Type = "text", Required = true, Placeholder = "e.g. dbo.Users, [dbo].[Products]", HelpText = "Enter the table name with schema" }
                    }
                },
                new TemplateTypeDefinition
                {
                    Id = "sp",
                    Name = "Stored Procedure Template",
                    Description = "Deploy or modify stored procedures",
                    Icon = "code",
                    BadgeColor = "badge-sp",
                    RequiresObjectNames = true,
                    RequiresTableInfo = false
                }
            };
        }
    }
}
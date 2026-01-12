namespace Template_Builder.Models.Entities
{
    public class TemplateTypeDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string BadgeColor { get; set; }
        public bool RequiresObjectNames { get; set; }
        public bool RequiresTableInfo { get; set; }
        public List<TemplateField> Fields { get; set; } = new List<TemplateField>();
    }
}
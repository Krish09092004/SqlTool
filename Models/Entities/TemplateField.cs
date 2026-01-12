namespace Template_Builder.Models.Entities
{
    public class TemplateField
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Type { get; set; } = "text";
        public bool Required { get; set; }
        public string Placeholder { get; set; }
        public string HelpText { get; set; }
    }
}
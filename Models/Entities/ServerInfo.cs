namespace Template_Builder.Models.Entities
{
    public class ServerInfo
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string Environment { get; set; } = "Development";
        public bool IsActive { get; set; } = true;
    }
}
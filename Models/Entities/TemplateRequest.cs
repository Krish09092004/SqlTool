using System.ComponentModel.DataAnnotations;
using Template_Builder.Models.Enums;

namespace Template_Builder.Models.Entities
{
    public class TemplateRequest
    {
        [Required(ErrorMessage = "Server selection is required")]
        public string ServerName { get; set; }

        [Required(ErrorMessage = "Database selection is required")]
        public string DatabaseName { get; set; }

        [Required(ErrorMessage = "Template type is required")]
        public string TemplateType { get; set; }

        [Required(ErrorMessage = "Ticket number is required")]
        [RegularExpression(@"^[A-Z]+-\d+$", ErrorMessage = "Ticket number format must be ABC-12345")]
        public string TicketNumber { get; set; }
        public string ObjectNames { get; set; }

        public string TableName { get; set; }
        public Dictionary<string, ColumnValue> ColumnValues { get; set; } = new();
        public List<string> SelectedWhereColumns { get; set; } = new();
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
        public string SessionId { get; set; }
        
        // CSV Import Support
        public string CsvContent { get; set; }
        public bool IsIdempotent { get; set; }
    }

    public class ColumnValue
    {
        public string Value { get; set; }
        public string DataType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool UseInWhereClause { get; set; }
        public bool IsForeignKey { get; set; }
        public ForeignKeyDefinition ForeignKey { get; set; }
    }

    public class ForeignKeyDefinition
    {
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
        public string WhereColumn { get; set; }
        public string WhereValue { get; set; }
    }

    public class ForeignKeyInfo
    {
        public string ReferencedTable { get; set; }
        public string ReferencedWhereClause { get; set; }
    }
}
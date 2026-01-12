namespace Template_Builder.Models.Entities
{
    public class TableColumnInfo
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public int? MaxLength { get; set; }
        public byte? Precision { get; set; }
        public byte? Scale { get; set; }
        public string IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsComputed { get; set; }
        public bool IsPrimaryKey { get; set; }
    }
}
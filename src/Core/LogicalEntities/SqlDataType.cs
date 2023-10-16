namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDataType
{
    public SqlDataType(string name) => Name = !string.IsNullOrEmpty(name) ? name : throw new ArgumentNullException(nameof(name));

    public string Name { get; }
    public int? Length { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
}

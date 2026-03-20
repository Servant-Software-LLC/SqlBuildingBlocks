namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDataType
{
    public SqlDataType(string name) => Name = !string.IsNullOrEmpty(name) ? name : throw new ArgumentNullException(nameof(name));

    public string Name { get; }
    public int? Length { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }

    /// <summary>
    /// Number of array dimensions. 0 means not an array type.
    /// e.g. <c>integer[]</c> has 1 dimension, <c>text[][]</c> has 2 dimensions.
    /// </summary>
    public int ArrayDimensions { get; set; }
}

using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlLimitValue
{
    public SqlLimitValue(int value = 0)
    {
        Value = value;
    }

    public SqlLimitValue(SqlParameter sqlParameter)
    {
        Parameter = sqlParameter;
    }

    public int Value { get; private set; }
    public SqlParameter? Parameter { get; private set; }

    public void Accept(ISqlValueVisitor visitor)
    {
        if (Parameter != null)
        {
            var potentiallyNewValue = Parameter.Accept(visitor);

            // If the returned expression is the same, then don't morph our likeness.
            if (potentiallyNewValue != null && !ReferenceEquals(this, potentiallyNewValue))
            {
                Value = potentiallyNewValue.Value;
                Parameter = potentiallyNewValue.Parameter;
            }
        }
    }
}

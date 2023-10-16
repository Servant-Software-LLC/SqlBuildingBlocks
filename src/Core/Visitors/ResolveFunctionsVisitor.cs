using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Visitors;

public class ResolveFunctionsVisitor : ResolveFunctionsVisitorBase
{
    private readonly IFunctionProvider functionProvider;

    public ResolveFunctionsVisitor(IFunctionProvider functionProvider)
    {
        this.functionProvider = functionProvider ?? throw new ArgumentNullException(nameof(functionProvider));
    }

    protected override SqlExpression? VisitReturnExpression(SqlFunction sqlFunction)
    {
        var sqlLiteralValue = VisitReturnValue(sqlFunction);
        if (sqlLiteralValue == null)
            return null;

        return new(sqlLiteralValue);
    }

    protected override SqlLiteralValue? VisitReturnValue(SqlFunction sqlFunction) =>
        new (functionProvider.GetDataValue(sqlFunction)());

}

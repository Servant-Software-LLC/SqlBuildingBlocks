using SqlBuildingBlocks.Interfaces;
using System.Data.Common;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDefinition
{
    public SqlDefinition(SqlSelectDefinition select) => Select = select ?? throw new ArgumentNullException(nameof(select));
    public SqlDefinition(SqlInsertDefinition insert) => Insert = insert ?? throw new ArgumentNullException(nameof(insert));

    public SqlDefinition(SqlUpdateDefinition update) => Update = update?? throw new ArgumentNullException(nameof(update));
    public SqlDefinition(SqlDeleteDefinition delete) => Delete = delete ?? throw new ArgumentNullException(nameof(delete));
    public SqlDefinition(SqlCreateTableDefinition create) => Create = create ?? throw new ArgumentNullException(nameof(create));

    //Only one of the following properties will ever be set.  Bounded by the ctors.
    public SqlSelectDefinition? Select { get; }
    public SqlInsertDefinition? Insert { get; }
    public SqlUpdateDefinition? Update { get; }
    public SqlDeleteDefinition? Delete { get;}
    public SqlCreateTableDefinition? Create { get; }

    public void ResolveParameters(DbParameterCollection parameters)
    {
        Select?.ResolveParameters(parameters);
        Insert?.ResolveParameters(parameters);
        Update?.ResolveParameters(parameters);
        Delete?.ResolveParameters(parameters);

        //NOTE: CREATE TABLE does not use parameters
    }

    /// <summary>
    /// Resolve functions which don't depend on the state of individual rows (for instance, LAST_INSERT_ID() depends on the previous SQL INSERT statement to determine its value whereas UPPER() would depend on the current row that is being evaluated.)
    /// </summary>
    /// <param name="functionProvider"></param>
    public void ResolveFunction(IFunctionProvider functionProvider)
    {
        Select?.ResolveFunctions(functionProvider);
        Insert?.ResolveFunctions(functionProvider);
        Update?.ResolveFunctions(functionProvider);
        Delete?.ResolveFunctions(functionProvider);

        //NOTE: CREATE TABLE does not use functions
    }

    public override string ToString()
    {
        if (Select != null) return Select.ToString(); 
        if (Insert != null) return Insert.ToString(); 
        if (Update != null) return Update.ToString();
        if (Delete != null) return Delete.ToString();
        if (Create != null) return Create.ToString();

        return "SQL definition type not set";
    }
}

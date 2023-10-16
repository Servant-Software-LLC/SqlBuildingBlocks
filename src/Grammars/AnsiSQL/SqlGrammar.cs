using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.AnsiSQL;

[Language("SQL", "89", "SQL 89 grammar")]
public class SqlGrammar : Grammar
{
    public SqlGrammar() : base(false)  //SQL is case insensitive
    {
        Comment.Register(this);
    }
}

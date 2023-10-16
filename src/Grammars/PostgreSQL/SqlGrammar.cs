using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

public class SqlGrammar : Grammar
{
    public SqlGrammar() : base(false)  //SQL is case insensitive
    {
        Comment.Register(this);
    }

}

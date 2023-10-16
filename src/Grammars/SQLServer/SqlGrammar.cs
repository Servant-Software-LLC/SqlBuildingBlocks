using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class SqlGrammar : Grammar
{
    public SqlGrammar() : base(false)  //SQL is case insensitive
    {
        Comment.Register(this);
    }

}

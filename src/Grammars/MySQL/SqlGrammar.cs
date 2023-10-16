using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.MySQL;


//TODO:  Implement this grammar.  Currently had no need for full MySQL parsing.
public class SqlGrammar : Grammar
{
    public SqlGrammar() : base(false)  //SQL is case insensitive
    {
        Comment.Register(this);

        //MySQL has special naming rules for identifiers.  (Note: the backtick)
        //REF: https://dev.mysql.com/doc/refman/8.0/en/identifiers.html
        MySQL.SimpleId simpleId = new(this);

    }

}

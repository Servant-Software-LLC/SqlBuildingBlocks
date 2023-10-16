using Irony.Parsing;

namespace SqlBuildingBlocks.Core.Tests;

public class IdListTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            var simpleId = new SimpleId(this);
            var id = new Id(this, simpleId);
            var idList = new IdList(this, id);

            Root = idList;
        }
    }

    //[Fact]

}

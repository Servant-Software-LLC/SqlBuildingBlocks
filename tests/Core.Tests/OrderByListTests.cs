using Irony.Parsing;

namespace SqlBuildingBlocks.Core.Tests;

public class OrderByListTests
{
    private class TestGrammar : Grammar
    {

        public TestGrammar()
        {
            var simpleId = new SimpleId(this);
            var id = new Id(this, simpleId);
            var orderByList = new OrderByList(this, id);

            Root = orderByList;
        }
    }

    //[Fact]
}

using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using System.Data;

namespace SqlBuildingBlocks.Core.Tests.Utils;

public class TableSchemaProvider : ITableSchemaProvider
{
    public IEnumerable<DataColumn> GetColumns(SqlTable table)
    {
        if (table.TableName == "Customers")
        {
            yield return new("ID", typeof(int));
            yield return new("CustomerName", typeof(string));
            yield break;
        }

        if (table.TableName == "Orders")
        {
            yield return new("ID", typeof(int));
            yield return new("CustomerID", typeof(int));
            yield return new("OrderDate", typeof(DateTime));
            yield break;
        }

        if (table.TableName == "OrderItems")
        {
            yield return new("OrderID", typeof(int));
            yield return new("ProductID", typeof(int));
            yield return new("Quantity", typeof(int));
            yield break;
        }

        if (table.TableName == "Products")
        {
            yield return new("ID", typeof(int));
            yield return new("Name", typeof(string));
            yield break;
        }

        if (table.TableName == "Blogs")
        {
            yield return new("BlogId", typeof(int));
            yield break;
        }

        throw new KeyNotFoundException($"The fake TableSchemaProvider doesn't know about a table by the name of {table.TableName}");
    }
}

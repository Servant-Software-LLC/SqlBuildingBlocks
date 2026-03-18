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

        if (table.TableName == "employees")
        {
            yield return new("id", typeof(int));
            yield return new("name", typeof(string));
            // DataColumn does not support Nullable<T>; use object to represent a nullable column.
            yield return new DataColumn("manager_id", typeof(int)) { AllowDBNull = true };
            yield return new("age", typeof(int));
            yield return new("status", typeof(string));
            yield break;
        }

        if (table.TableName == "orders")
        {
            yield return new("id", typeof(int));
            yield return new("customer_id", typeof(int));
            yield return new DataColumn("shipped_date", typeof(DateTime)) { AllowDBNull = true };
            yield return new("amount", typeof(decimal));
            yield return new("status", typeof(string));
            yield return new("age", typeof(int));
            yield break;
        }

        if (table.TableName == "tasks")
        {
            yield return new("id", typeof(int));
            yield return new DataColumn("completed_at", typeof(DateTime)) { AllowDBNull = true };
            yield return new("assigned_to", typeof(string));
            yield break;
        }

        throw new KeyNotFoundException($"The fake TableSchemaProvider doesn't know about a table by the name of {table.TableName}");
    }
}

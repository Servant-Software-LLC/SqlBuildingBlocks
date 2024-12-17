using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using System.Data;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.QueryProcessing;

public class QueryEngineTests
{
    [Fact]
    public void Ctor_WithDataSets_QueryAsDataTable_Results()
    {
        const string databaseName = "MyDB";

        // SELECT c.CustomerName, [o].[OrderDate] FROM [Customers] c INNER JOIN [Orders] o ON [c].[ID] = [o].[CustomerID]
        SqlSelectDefinition sqlSelect = new();
        SqlColumn customerNameCol = new(null, "c", "CustomerName");
        sqlSelect.Columns.Add(customerNameCol);

        SqlColumn orderDateColumn = new(null, "o", "OrderDate");
        sqlSelect.Columns.Add(orderDateColumn);

        //FROM [Customers] c
        SqlTable customersTable = new(databaseName, "Customers") { TableAlias = "c" };
        customerNameCol.TableRef = customersTable;
        sqlSelect.Table = customersTable;

        //INNER JOIN [Orders]
        SqlTable ordersTable = new(databaseName, "Orders") { TableAlias= "o" };
        orderDateColumn.TableRef = ordersTable;

        //ON [c].[ID] = [o].[CustomerID]

        //Hidden column [c].ID (for left-side of JOIN expression)
        SqlColumn idColumn = new(databaseName, customersTable.TableName, "ID")
        {
            ColumnType = typeof(string),
            TableRef = customersTable
        };
        SqlColumnRef idColumnRef = new(null, "c", idColumn.ColumnName) { Column = idColumn };

        //Hidden column [o].[CustomerID] (for right-side of JOIN expression)
        SqlColumn customerIdColumn = new(databaseName, ordersTable.TableName, "CustomerID")
        {
            ColumnType = typeof(string),
            TableRef = ordersTable
        };
        SqlColumnRef customerIdColumnRef = new(null, "o", customerIdColumn.ColumnName) { Column = customerIdColumn };

        //INNER JOIN [Orders] o ON [c].[ID] = [o].[CustomerID]
        SqlJoin sqlJoin = new(ordersTable, new SqlBinaryExpression(new(idColumnRef), SqlBinaryOperator.Equal, new(customerIdColumnRef)));
        sqlSelect.Joins.Add(sqlJoin);


        //Create the DataSet with the schema and data.
        DataSet dataSet = new(databaseName);

        //Customers table (schema)
        DataTable customers = new("Customers");
        customers.Columns.Add("ID", typeof(string));
        customers.Columns.Add("CustomerName", typeof(string));
        dataSet.Tables.Add(customers);

        //Customers table (data)
        customers.Rows.Add("1", "John Doe");
        customers.Rows.Add("2", "Jane Smith");
        customers.Rows.Add("3", "Bob Johnson");

        //Orders table (schema)
        DataTable orders = new("Orders");
        orders.Columns.Add("ID", typeof(string));
        orders.Columns.Add("CustomerID", typeof(string));
        orders.Columns.Add("OrderDate", typeof(string));
        dataSet.Tables.Add(orders);

        //Orders table (data)
        orders.Rows.Add("1", "1", "2022-03-20");
        orders.Rows.Add("2", "1", "2022-03-21");
        orders.Rows.Add("3", "1", "2022-03-22");
        orders.Rows.Add("4", "1", "2022-03-23");
        orders.Rows.Add("5", "1", "2022-03-24");
        orders.Rows.Add("6", "2", "2022-03-20");
        orders.Rows.Add("7", "2", "2022-03-21");
        orders.Rows.Add("8", "2", "2022-03-22");
        orders.Rows.Add("9", "2", "2022-03-23");
        orders.Rows.Add("10", "2", "2022-03-24");
        orders.Rows.Add("11", "3", "2022-03-20");
        orders.Rows.Add("12", "3", "2022-03-21");
        orders.Rows.Add("13", "3", "2022-03-22");
        orders.Rows.Add("14", "3", "2022-03-23");
        orders.Rows.Add("15", "3", "2022-03-24");

        //Create the QueryEngine
        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);

        //Act
        var resultset = queryEngine.QueryAsDataTable();

        Assert.NotNull(resultset);
        Assert.Equal(2, resultset.Columns.Count);
        Assert.Equal("CustomerName", resultset.Columns[0].ColumnName);
        Assert.Equal("OrderDate", resultset.Columns[1].ColumnName);

        Assert.Equal(15, resultset.Rows.Count);

        //Sample one of the rows
        var sixthRow = resultset.Rows[5];
        Assert.Equal("Jane Smith", sixthRow[0]);
        Assert.Equal("2022-03-20", sixthRow[1]);
    }

    [Fact]
    public void Select_Count_WithFilter()
    {
        const string databaseName = "MyDB";

        // SELECT COUNT(*) FROM locations where id=1 or id=2
        SqlSelectDefinition sqlSelect = new();
        SqlAggregate countColumn = new("COUNT");
        sqlSelect.Columns.Add(countColumn);

        // FROM locations
        SqlTable locationsTable = new(databaseName, "locations");
        sqlSelect.Table = locationsTable;

        //Hidden column [locations].[id] (for left-side of WHERE expressions)
        SqlColumn locationsIdColumn = new(databaseName, locationsTable.TableName, "id")
        {
            ColumnType = typeof(int),
            TableRef = locationsTable
        };
        SqlColumnRef locationsIdColumnRef = new(null, null, "id") { Column = locationsIdColumn };

        // WHERE id=1 or id=2
        SqlExpression leftExpression = new(new SqlBinaryExpression(new(locationsIdColumnRef), SqlBinaryOperator.Equal, new(new SqlLiteralValue(1))));
        SqlExpression rightExpression = new(new SqlBinaryExpression(new(locationsIdColumnRef), SqlBinaryOperator.Equal, new(new SqlLiteralValue(2))));
        SqlBinaryExpression whereClause = new(leftExpression, SqlBinaryOperator.Or, rightExpression);
        sqlSelect.WhereClause = whereClause;

        //Create the DataSet with the schema and data.
        DataSet dataSet = new(databaseName);

        //locations table (schema)
        DataTable locations = new("locations");
        locations.Columns.Add("id", typeof(int));
        locations.Columns.Add("city", typeof(string));
        locations.Columns.Add("state", typeof(string));
        locations.Columns.Add("zip", typeof(int));
        dataSet.Tables.Add(locations);

        //locations table (data)
        locations.Rows.Add(1, "Houston", "Texas", 77846);
        locations.Rows.Add(2, "New Braunfels", "Texas", 78132);
        locations.Rows.Add(3, "San Antonio  ", "Texas", 78245);

        //Create the QueryEngine
        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);

        //Act
        var resultset = queryEngine.QueryAsDataTable();

        Assert.NotNull(resultset);
        Assert.Equal(1, resultset.Rows.Count);
        Assert.Single(resultset.Columns);
        var countValue = resultset.Rows[0][0];
        Assert.IsType<int>(countValue);
        Assert.Equal(2, (int)countValue);
    }

    [Fact]
    public void QueryAsDataTable_Select_AliasedColumn()
    {
        const string databaseName = "MyDB";

        //SELECT ID, Name, Time AS LoginTime FROM Users JOIN Logins ON Users.ID = Logins.UserID
        SqlSelectDefinition sqlSelect = new();
        SqlColumn idCol = new(null, null, "ID") { ColumnType = typeof(int) };
        sqlSelect.Columns.Add(idCol);
        SqlColumn nameCol = new(null, null, "Name") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(nameCol);
        SqlColumn timeCol = new(null, null, "Time") { ColumnAlias = "LoginTime", ColumnType = typeof(DateTime) };
        sqlSelect.Columns.Add(timeCol);

        //FROM Users
        SqlTable usersTable = new(databaseName, "Users");
        idCol.TableRef = usersTable;
        nameCol.TableRef = usersTable;
        sqlSelect.Table = usersTable;

        //JOIN Logins
        SqlTable loginsTable = new(databaseName, "Logins") { TableAlias = "o" };
        timeCol.TableRef = loginsTable;

        //ON Users.ID = Logins.UserID

        //ColumnRef (for left-side of JOIN expression)
        SqlColumnRef idColumnRef = new(null, usersTable.TableName, idCol.ColumnName) { Column = idCol };

        //Hidden column Logins.UserID (for right-side of JOIN expression)
        SqlColumn userIdColumn = new(null, loginsTable.TableName, "UserID")
        {
            ColumnType = typeof(int),
            TableRef = loginsTable
        };
        SqlColumnRef userIdColumnRef = new(null, loginsTable.TableName, userIdColumn.ColumnName) { Column = userIdColumn };

        SqlJoin sqlJoin = new(loginsTable, new SqlBinaryExpression(new(idColumnRef), SqlBinaryOperator.Equal, new(userIdColumnRef)));
        sqlSelect.Joins.Add(sqlJoin);

        //Create the DataSet with the schema and data.
        DataSet dataSet = new(databaseName);

        //Users table (schema)
        DataTable customers = new(usersTable.TableName);
        customers.Columns.Add(idCol.ColumnName, typeof(int));
        customers.Columns.Add(nameCol.ColumnName, typeof(string));
        dataSet.Tables.Add(customers);

        //Customers table (data)
        customers.Rows.Add(1, "Mike");
        customers.Rows.Add(2, "Jon");
        customers.Rows.Add(3, "Dave");

        //Logins table (schema)
        DataTable logins = new(loginsTable.TableName);
        logins.Columns.Add(userIdColumn.ColumnName, typeof(int));
        logins.Columns.Add(timeCol.ColumnName, typeof(DateTime));
        dataSet.Tables.Add(logins);

        //Logins table (data)
        Random random = new Random();
        DateTime now = DateTime.Now;
        for (int i = 0; i < 10; i++)
        {
            int randomUserId = random.Next(1, 4); // Random value between 1 and 3 inclusive

            DateTime randomDate = now.AddDays(-random.Next(0, 365)); // Random date within the last year

            logins.Rows.Add(randomUserId, randomDate);
        }

        //Create the QueryEngine
        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);

        //Act
        var resultset = queryEngine.QueryAsDataTable();

        Assert.NotNull(resultset);
        Assert.Equal(3, resultset.Columns.Count);
        Assert.Equal(idCol.ColumnName, resultset.Columns[0].ColumnName);
        Assert.Equal(nameCol.ColumnName, resultset.Columns[1].ColumnName);
        Assert.Equal(timeCol.ColumnAlias, resultset.Columns[2].ColumnName);

        Assert.Equal(10, resultset.Rows.Count);

    }

    [Fact]
    public void QueryAsDataTable_Select_ColumnsSameName_DifferingCase()
    {
        // Arrange

        const string databaseName = "MyDB";
        const string tableName = "Table";

        //Create the DataSet with the schema and data.
        DataSet dataSet = new(databaseName);

        //Define a table's schema
        DataTable table = new(tableName);
        table.Columns.Add("Id", typeof(double));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("nAMe", typeof(string));

        //Add data to table
        table.Rows.Add(new object[] { 1.0, "Bogart", "Bob"});
        dataSet.Tables.Add(table);

        // SELECT * FROM Table
        SqlSelectDefinition sqlSelect = new SqlSelectDefinition();
        SqlColumn idColumn = new(databaseName, tableName, "Id") { ColumnType = typeof(double) };
        sqlSelect.Columns.Add(idColumn);
        SqlColumn nameColumn = new(databaseName, tableName, "Name");
        sqlSelect.Columns.Add(nameColumn);
        SqlColumn nAMeColumn = new(databaseName, tableName, "nAMe");
        sqlSelect.Columns.Add(nAMeColumn);

        SqlTable sqlTable = new(databaseName, tableName);
        idColumn.TableRef = sqlTable;
        nameColumn.TableRef = sqlTable;
        nAMeColumn.TableRef = sqlTable;
        sqlSelect.Table = sqlTable;


        // Act 

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        DataTable dataTable = queryEngine.QueryAsDataTable();

        // Assert

        Assert.Equal(3, dataTable.Columns.Count);
        Assert.Equal("Id", dataTable.Columns[0].ColumnName);
        Assert.Equal("Name", dataTable.Columns[1].ColumnName);
        Assert.Equal("nAMe", dataTable.Columns[2].ColumnName);

        Assert.Equal(1, dataTable.Rows.Count);
        var firstRow = dataTable.Rows[0];

        Assert.Equal(1.0, firstRow[0]);
        Assert.Equal("Bogart", firstRow[1]);
        Assert.Equal("Bob", firstRow[2]);

    }
}

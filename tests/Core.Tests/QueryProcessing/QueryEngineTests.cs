using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
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
        SqlBinaryExpression whereClauseBinary = new(leftExpression, SqlBinaryOperator.Or, rightExpression);
        sqlSelect.WhereClause = new SqlExpression(whereClauseBinary);

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

    [Fact]
    public void QueryAsDataTable_OrderBy_SingleColumn_Asc()
    {
        const string databaseName = "MyDB";

        // SELECT id, city FROM locations ORDER BY city ASC
        SqlSelectDefinition sqlSelect = new();
        SqlColumn idCol = new(databaseName, "locations", "id") { ColumnType = typeof(int) };
        SqlColumn cityCol = new(databaseName, "locations", "city") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(cityCol);

        SqlTable locationsTable = new(databaseName, "locations");
        idCol.TableRef = locationsTable;
        cityCol.TableRef = locationsTable;
        sqlSelect.Table = locationsTable;

        sqlSelect.OrderBy.Add(new SqlOrderByColumn("city", descending: false));

        DataSet dataSet = new(databaseName);
        DataTable locations = new("locations");
        locations.Columns.Add("id", typeof(int));
        locations.Columns.Add("city", typeof(string));
        locations.Rows.Add(1, "Houston");
        locations.Rows.Add(2, "New Braunfels");
        locations.Rows.Add(3, "Austin");
        dataSet.Tables.Add(locations);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(3, resultset.Rows.Count);
        Assert.Equal("Austin", resultset.Rows[0]["city"]);
        Assert.Equal("Houston", resultset.Rows[1]["city"]);
        Assert.Equal("New Braunfels", resultset.Rows[2]["city"]);
    }

    [Fact]
    public void QueryAsDataTable_OrderBy_SingleColumn_Desc()
    {
        const string databaseName = "MyDB";

        // SELECT id, city FROM locations ORDER BY city DESC
        SqlSelectDefinition sqlSelect = new();
        SqlColumn idCol = new(databaseName, "locations", "id") { ColumnType = typeof(int) };
        SqlColumn cityCol = new(databaseName, "locations", "city") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(cityCol);

        SqlTable locationsTable = new(databaseName, "locations");
        idCol.TableRef = locationsTable;
        cityCol.TableRef = locationsTable;
        sqlSelect.Table = locationsTable;

        sqlSelect.OrderBy.Add(new SqlOrderByColumn("city", descending: true));

        DataSet dataSet = new(databaseName);
        DataTable locations = new("locations");
        locations.Columns.Add("id", typeof(int));
        locations.Columns.Add("city", typeof(string));
        locations.Rows.Add(1, "Houston");
        locations.Rows.Add(2, "New Braunfels");
        locations.Rows.Add(3, "Austin");
        dataSet.Tables.Add(locations);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(3, resultset.Rows.Count);
        Assert.Equal("New Braunfels", resultset.Rows[0]["city"]);
        Assert.Equal("Houston", resultset.Rows[1]["city"]);
        Assert.Equal("Austin", resultset.Rows[2]["city"]);
    }

    [Fact]
    public void QueryAsDataTable_OrderBy_MultiColumn()
    {
        const string databaseName = "MyDB";

        // SELECT id, state, city FROM locations ORDER BY state ASC, city DESC
        SqlSelectDefinition sqlSelect = new();
        SqlColumn idCol = new(databaseName, "locations", "id") { ColumnType = typeof(int) };
        SqlColumn stateCol = new(databaseName, "locations", "state") { ColumnType = typeof(string) };
        SqlColumn cityCol = new(databaseName, "locations", "city") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(stateCol);
        sqlSelect.Columns.Add(cityCol);

        SqlTable locationsTable = new(databaseName, "locations");
        idCol.TableRef = locationsTable;
        stateCol.TableRef = locationsTable;
        cityCol.TableRef = locationsTable;
        sqlSelect.Table = locationsTable;

        sqlSelect.OrderBy.Add(new SqlOrderByColumn("state", descending: false));
        sqlSelect.OrderBy.Add(new SqlOrderByColumn("city", descending: true));

        DataSet dataSet = new(databaseName);
        DataTable locations = new("locations");
        locations.Columns.Add("id", typeof(int));
        locations.Columns.Add("state", typeof(string));
        locations.Columns.Add("city", typeof(string));
        locations.Rows.Add(1, "Texas", "Houston");
        locations.Rows.Add(2, "Texas", "Austin");
        locations.Rows.Add(3, "California", "Los Angeles");
        locations.Rows.Add(4, "California", "San Francisco");
        dataSet.Tables.Add(locations);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(4, resultset.Rows.Count);
        // California first (ASC), cities within California in DESC order
        Assert.Equal("San Francisco", resultset.Rows[0]["city"]);
        Assert.Equal("Los Angeles", resultset.Rows[1]["city"]);
        // Texas second, cities within Texas in DESC order
        Assert.Equal("Houston", resultset.Rows[2]["city"]);
        Assert.Equal("Austin", resultset.Rows[3]["city"]);
    }

    [Fact]
    public void QueryAsDataTable_OrderBy_Numeric_Asc()
    {
        const string databaseName = "MyDB";

        // SELECT id FROM locations ORDER BY id ASC — verifies numeric (not lexicographic) sort
        SqlSelectDefinition sqlSelect = new();
        SqlColumn idCol = new(databaseName, "locations", "id") { ColumnType = typeof(int) };
        sqlSelect.Columns.Add(idCol);

        SqlTable locationsTable = new(databaseName, "locations");
        idCol.TableRef = locationsTable;
        sqlSelect.Table = locationsTable;

        sqlSelect.OrderBy.Add(new SqlOrderByColumn("id", descending: false));

        DataSet dataSet = new(databaseName);
        DataTable locations = new("locations");
        locations.Columns.Add("id", typeof(int));
        locations.Rows.Add(10);
        locations.Rows.Add(2);
        locations.Rows.Add(1);
        dataSet.Tables.Add(locations);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(3, resultset.Rows.Count);
        Assert.Equal(1, resultset.Rows[0]["id"]);
        Assert.Equal(2, resultset.Rows[1]["id"]);
        Assert.Equal(10, resultset.Rows[2]["id"]);
    }

    [Fact]
    public void QueryAsDataTable_OrderBy_Then_Limit()
    {
        const string databaseName = "MyDB";

        // ORDER BY id ASC LIMIT 2 — verifies ordering applied before limiting
        SqlSelectDefinition sqlSelect = new();
        SqlColumn idCol = new(databaseName, "locations", "id") { ColumnType = typeof(int) };
        SqlColumn cityCol = new(databaseName, "locations", "city") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(cityCol);

        SqlTable locationsTable = new(databaseName, "locations");
        idCol.TableRef = locationsTable;
        cityCol.TableRef = locationsTable;
        sqlSelect.Table = locationsTable;

        sqlSelect.OrderBy.Add(new SqlOrderByColumn("id", descending: false));
        sqlSelect.Limit = new SqlLimitOffset { RowCount = new SqlLimitValue(2) };

        DataSet dataSet = new(databaseName);
        DataTable locations = new("locations");
        locations.Columns.Add("id", typeof(int));
        locations.Columns.Add("city", typeof(string));
        locations.Rows.Add(3, "Austin");
        locations.Rows.Add(1, "Houston");
        locations.Rows.Add(2, "New Braunfels");
        dataSet.Tables.Add(locations);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        // After sort: 1-Houston, 2-New Braunfels, 3-Austin.  LIMIT 2 → first two.
        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(1, resultset.Rows[0]["id"]);
        Assert.Equal(2, resultset.Rows[1]["id"]);
    }

    [Fact]
    public void QueryWithUnendingDataSource_Select()
    {
        SelectStmtTests.TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, $"SELECT * FROM {UnendingTableDataProvider.tableName}");

        FakeDatabaseConnectionProvider databaseConnectionProvider = new() { DefaultDatabase = UnendingTableDataProvider.databaseName };
        UnendingTableDataProvider unendingTableDataProvider = new();
        SqlSelectDefinition selectDefinition = grammar.Create(node, databaseConnectionProvider, unendingTableDataProvider);

        AllTableDataProvider allTableDataProvider = new(new ITableDataProvider[] { unendingTableDataProvider });
        var queryEngine = new QueryEngine(allTableDataProvider, selectDefinition);

        var virtualDataTable = queryEngine.Query();

        int counter = 0;
        foreach (var row in virtualDataTable.Rows)
        {
            Assert.Equal(counter, row["id"]);
            counter++;

            //Make sure that our data source only has provided the number of rows requested.
            Assert.Equal(counter, unendingTableDataProvider.DataRowsProvided);

            if (counter > 5)
                break;
        }
    }

    #region IS NOT NULL

    [Fact]
    public void QueryAsDataTable_Where_IsNotNull()
    {
        const string databaseName = "MyDB";

        // SELECT id, name FROM employees WHERE name IS NOT NULL
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn idCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(nameCol);
        sqlSelect.Table = employeesTable;

        // WHERE name IS NOT NULL
        SqlColumnRef nameRef = new(null, null, "name") { Column = nameCol };
        SqlBinaryExpression whereClause = new(new SqlExpression(nameRef), SqlBinaryOperator.IsNotNull, null);
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, DBNull.Value);
        employees.Rows.Add(3, "Charlie");
        employees.Rows.Add(4, DBNull.Value);
        employees.Rows.Add(5, "Eve");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(3, resultset.Rows.Count);
        Assert.Equal("Alice", resultset.Rows[0]["name"]);
        Assert.Equal("Charlie", resultset.Rows[1]["name"]);
        Assert.Equal("Eve", resultset.Rows[2]["name"]);
    }

    [Fact]
    public void QueryAsDataTable_Where_IsNull()
    {
        const string databaseName = "MyDB";

        // SELECT id FROM employees WHERE name IS NULL
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn idCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Table = employeesTable;

        // WHERE name IS NULL
        SqlColumnRef nameRef = new(null, null, "name") { Column = nameCol };
        SqlBinaryExpression whereClause = new(new SqlExpression(nameRef), SqlBinaryOperator.IsNull, null);
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, DBNull.Value);
        employees.Rows.Add(3, "Charlie");
        employees.Rows.Add(4, DBNull.Value);
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(2, resultset.Rows[0]["id"]);
        Assert.Equal(4, resultset.Rows[1]["id"]);
    }

    #endregion

    #region Built-in Functions

    [Fact]
    public void QueryAsDataTable_Upper_Function()
    {
        const string databaseName = "MyDB";

        // SELECT UPPER(name) FROM employees
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlFunction upperFunc = new("UPPER");
        upperFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = nameCol }));
        SqlFunctionColumn funcCol = new(upperFunc) { ColumnAlias = "upper_name" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add("Alice");
        employees.Rows.Add("bob");
        employees.Rows.Add("Charlie");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(3, resultset.Rows.Count);
        Assert.Equal("ALICE", resultset.Rows[0]["upper_name"]);
        Assert.Equal("BOB", resultset.Rows[1]["upper_name"]);
        Assert.Equal("CHARLIE", resultset.Rows[2]["upper_name"]);
    }

    [Fact]
    public void QueryAsDataTable_Lower_Function()
    {
        const string databaseName = "MyDB";

        // SELECT LOWER(name) FROM employees
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlFunction lowerFunc = new("LOWER");
        lowerFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = nameCol }));
        SqlFunctionColumn funcCol = new(lowerFunc) { ColumnAlias = "lower_name" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add("ALICE");
        employees.Rows.Add("Bob");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal("alice", resultset.Rows[0]["lower_name"]);
        Assert.Equal("bob", resultset.Rows[1]["lower_name"]);
    }

    [Fact]
    public void QueryAsDataTable_Length_Function()
    {
        const string databaseName = "MyDB";

        // SELECT LENGTH(name) AS name_len FROM employees
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlFunction lengthFunc = new("LENGTH") { ValueType = typeof(int) };
        lengthFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = nameCol }));
        SqlFunctionColumn funcCol = new(lengthFunc) { ColumnAlias = "name_len" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add("Alice");
        employees.Rows.Add("Bo");
        employees.Rows.Add("Charlotte");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(3, resultset.Rows.Count);
        Assert.Equal(5, resultset.Rows[0]["name_len"]);
        Assert.Equal(2, resultset.Rows[1]["name_len"]);
        Assert.Equal(9, resultset.Rows[2]["name_len"]);
    }

    [Fact]
    public void QueryAsDataTable_Abs_Function()
    {
        const string databaseName = "MyDB";

        // SELECT ABS(balance) AS abs_balance FROM accounts
        SqlSelectDefinition sqlSelect = new();
        SqlTable accountsTable = new(databaseName, "accounts");

        SqlColumn balanceCol = new(databaseName, "accounts", "balance") { ColumnType = typeof(double), TableRef = accountsTable };
        SqlFunction absFunc = new("ABS") { ValueType = typeof(double) };
        absFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "balance") { Column = balanceCol }));
        SqlFunctionColumn funcCol = new(absFunc) { ColumnAlias = "abs_balance" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = accountsTable;

        DataSet dataSet = new(databaseName);
        DataTable accounts = new("accounts");
        accounts.Columns.Add("balance", typeof(double));
        accounts.Rows.Add(-100.5);
        accounts.Rows.Add(200.0);
        accounts.Rows.Add(-50.25);
        dataSet.Tables.Add(accounts);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(3, resultset.Rows.Count);
        Assert.Equal(100.5, resultset.Rows[0]["abs_balance"]);
        Assert.Equal(200.0, resultset.Rows[1]["abs_balance"]);
        Assert.Equal(50.25, resultset.Rows[2]["abs_balance"]);
    }

    [Fact]
    public void QueryAsDataTable_Round_Function()
    {
        const string databaseName = "MyDB";

        // SELECT ROUND(price, 1) AS rounded FROM products
        SqlSelectDefinition sqlSelect = new();
        SqlTable productsTable = new(databaseName, "products");

        SqlColumn priceCol = new(databaseName, "products", "price") { ColumnType = typeof(double), TableRef = productsTable };
        SqlFunction roundFunc = new("ROUND") { ValueType = typeof(double) };
        roundFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "price") { Column = priceCol }));
        roundFunc.Arguments.Add(new SqlExpression(new SqlLiteralValue(1)));
        SqlFunctionColumn funcCol = new(roundFunc) { ColumnAlias = "rounded" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = productsTable;

        DataSet dataSet = new(databaseName);
        DataTable products = new("products");
        products.Columns.Add("price", typeof(double));
        products.Rows.Add(10.456);
        products.Rows.Add(20.789);
        products.Rows.Add(30.123);
        dataSet.Tables.Add(products);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(3, resultset.Rows.Count);
        Assert.Equal(10.5, resultset.Rows[0]["rounded"]);
        Assert.Equal(20.8, resultset.Rows[1]["rounded"]);
        Assert.Equal(30.1, resultset.Rows[2]["rounded"]);
    }

    [Fact]
    public void QueryAsDataTable_Len_Function_Alias()
    {
        const string databaseName = "MyDB";

        // SELECT LEN(name) AS name_len FROM employees  (LEN as alias for LENGTH)
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlFunction lenFunc = new("LEN") { ValueType = typeof(int) };
        lenFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = nameCol }));
        SqlFunctionColumn funcCol = new(lenFunc) { ColumnAlias = "name_len" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add("Hi");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal(2, resultset.Rows[0]["name_len"]);
    }

    #endregion

    #region MAX/MIN Aggregates

    [Fact]
    public void QueryAsDataTable_Select_Max()
    {
        const string databaseName = "MyDB";

        // SELECT MAX(price) FROM products
        SqlSelectDefinition sqlSelect = new();
        SqlTable productsTable = new(databaseName, "products");

        SqlColumn priceCol = new(databaseName, "products", "price") { ColumnType = typeof(double), TableRef = productsTable };
        SqlColumnRef priceRef = new(null, null, "price") { Column = priceCol };
        SqlAggregate maxAgg = new("MAX", new SqlExpression(priceRef));
        sqlSelect.Columns.Add(maxAgg);
        sqlSelect.Table = productsTable;

        DataSet dataSet = new(databaseName);
        DataTable products = new("products");
        products.Columns.Add("price", typeof(double));
        products.Rows.Add(10.0);
        products.Rows.Add(50.0);
        products.Rows.Add(30.0);
        dataSet.Tables.Add(products);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal(50.0, resultset.Rows[0][0]);
    }

    [Fact]
    public void QueryAsDataTable_Select_Min()
    {
        const string databaseName = "MyDB";

        // SELECT MIN(price) FROM products
        SqlSelectDefinition sqlSelect = new();
        SqlTable productsTable = new(databaseName, "products");

        SqlColumn priceCol = new(databaseName, "products", "price") { ColumnType = typeof(double), TableRef = productsTable };
        SqlColumnRef priceRef = new(null, null, "price") { Column = priceCol };
        SqlAggregate minAgg = new("MIN", new SqlExpression(priceRef));
        sqlSelect.Columns.Add(minAgg);
        sqlSelect.Table = productsTable;

        DataSet dataSet = new(databaseName);
        DataTable products = new("products");
        products.Columns.Add("price", typeof(double));
        products.Rows.Add(10.0);
        products.Rows.Add(50.0);
        products.Rows.Add(30.0);
        dataSet.Tables.Add(products);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal(10.0, resultset.Rows[0][0]);
    }

    [Fact]
    public void QueryAsDataTable_Select_MaxAndMin()
    {
        const string databaseName = "MyDB";

        // SELECT MAX(price), MIN(price) FROM products
        SqlSelectDefinition sqlSelect = new();
        SqlTable productsTable = new(databaseName, "products");

        SqlColumn priceCol = new(databaseName, "products", "price") { ColumnType = typeof(double), TableRef = productsTable };

        SqlColumnRef maxRef = new(null, null, "price") { Column = priceCol };
        SqlAggregate maxAgg = new("MAX", new SqlExpression(maxRef)) { ColumnAlias = "max_price" };
        sqlSelect.Columns.Add(maxAgg);

        SqlColumnRef minRef = new(null, null, "price") { Column = priceCol };
        SqlAggregate minAgg = new("MIN", new SqlExpression(minRef)) { ColumnAlias = "min_price" };
        sqlSelect.Columns.Add(minAgg);

        sqlSelect.Table = productsTable;

        DataSet dataSet = new(databaseName);
        DataTable products = new("products");
        products.Columns.Add("price", typeof(double));
        products.Rows.Add(10.0);
        products.Rows.Add(50.0);
        products.Rows.Add(30.0);
        dataSet.Tables.Add(products);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal(2, resultset.Columns.Count);
        Assert.Equal(50.0, resultset.Rows[0]["max_price"]);
        Assert.Equal(10.0, resultset.Rows[0]["min_price"]);
    }

    [Fact]
    public void QueryAsDataTable_Select_Max_WithFilter()
    {
        const string databaseName = "MyDB";

        // SELECT MAX(price) FROM products WHERE category = 'A'
        SqlSelectDefinition sqlSelect = new();
        SqlTable productsTable = new(databaseName, "products");

        SqlColumn priceCol = new(databaseName, "products", "price") { ColumnType = typeof(double), TableRef = productsTable };
        SqlColumn categoryCol = new(databaseName, "products", "category") { ColumnType = typeof(string), TableRef = productsTable };

        SqlColumnRef priceRef = new(null, null, "price") { Column = priceCol };
        SqlAggregate maxAgg = new("MAX", new SqlExpression(priceRef));
        sqlSelect.Columns.Add(maxAgg);
        sqlSelect.Table = productsTable;

        // WHERE category = 'A'
        SqlColumnRef categoryRef = new(null, null, "category") { Column = categoryCol };
        SqlBinaryExpression whereClause = new(new SqlExpression(categoryRef), SqlBinaryOperator.Equal, new SqlExpression(new SqlLiteralValue("A")));
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable products = new("products");
        products.Columns.Add("price", typeof(double));
        products.Columns.Add("category", typeof(string));
        products.Rows.Add(10.0, "A");
        products.Rows.Add(50.0, "B");
        products.Rows.Add(30.0, "A");
        products.Rows.Add(20.0, "A");
        dataSet.Tables.Add(products);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal(30.0, resultset.Rows[0][0]);
    }

    [Fact]
    public void QueryAsDataTable_Select_Min_StringColumn()
    {
        const string databaseName = "MyDB";

        // SELECT MIN(name) FROM employees
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlColumnRef nameRef = new(null, null, "name") { Column = nameCol };
        SqlAggregate minAgg = new("MIN", new SqlExpression(nameRef));
        sqlSelect.Columns.Add(minAgg);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add("Charlie");
        employees.Rows.Add("Alice");
        employees.Rows.Add("Bob");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal("Alice", resultset.Rows[0][0]);
    }

    #endregion

    #region Bug Fix: UPPER/LOWER projection with missing TableRef

    [Fact]
    public void QueryAsDataTable_Upper_Function_NoTableRef()
    {
        const string databaseName = "MyDB";

        // SELECT UPPER(name) FROM employees — function arg column has no TableRef
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        // Column without TableRef set (simulates parser not resolving table reference)
        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string) };
        SqlFunction upperFunc = new("UPPER");
        upperFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = nameCol }));
        SqlFunctionColumn funcCol = new(upperFunc) { ColumnAlias = "upper_name" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add("Alice");
        employees.Rows.Add("bob");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal("ALICE", resultset.Rows[0]["upper_name"]);
        Assert.Equal("BOB", resultset.Rows[1]["upper_name"]);
    }

    [Fact]
    public void QueryAsDataTable_Lower_Function_NoTableRef()
    {
        const string databaseName = "MyDB";

        // SELECT LOWER(name) FROM employees — function arg column has no TableRef
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string) };
        SqlFunction lowerFunc = new("LOWER");
        lowerFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = nameCol }));
        SqlFunctionColumn funcCol = new(lowerFunc) { ColumnAlias = "lower_name" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add("ALICE");
        employees.Rows.Add("Bob");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal("alice", resultset.Rows[0]["lower_name"]);
        Assert.Equal("bob", resultset.Rows[1]["lower_name"]);
    }

    [Fact]
    public void QueryAsDataTable_Upper_WithOtherColumn_NoTableRef()
    {
        const string databaseName = "MyDB";

        // SELECT id, UPPER(name) FROM employees — ensures projection includes function arg column
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn idCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        sqlSelect.Columns.Add(idCol);

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string) };
        SqlFunction upperFunc = new("UPPER");
        upperFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = nameCol }));
        SqlFunctionColumn funcCol = new(upperFunc) { ColumnAlias = "upper_name" };
        sqlSelect.Columns.Add(funcCol);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, "bob");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(1, resultset.Rows[0]["id"]);
        Assert.Equal("ALICE", resultset.Rows[0]["upper_name"]);
        Assert.Equal(2, resultset.Rows[1]["id"]);
        Assert.Equal("BOB", resultset.Rows[1]["upper_name"]);
    }

    #endregion

    #region Bug Fix: LENGTH/LEN in WHERE clause

    [Fact]
    public void QueryAsDataTable_Length_InWhere()
    {
        const string databaseName = "MyDB";

        // SELECT id, name FROM employees WHERE LENGTH(name) > 3
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn idCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        sqlSelect.Columns.Add(idCol);

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        sqlSelect.Columns.Add(nameCol);

        sqlSelect.Table = employeesTable;

        // WHERE LENGTH(name) > 3
        SqlColumn whereNameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlFunction lengthFunc = new("LENGTH") { ValueType = typeof(int) };
        lengthFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = whereNameCol }));
        SqlBinaryExpression whereClause = new(
            new SqlExpression(lengthFunc),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(3)));
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, "Bo");
        employees.Rows.Add(3, "Charlotte");
        employees.Rows.Add(4, "Ed");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(1, resultset.Rows[0]["id"]);
        Assert.Equal("Alice", resultset.Rows[0]["name"]);
        Assert.Equal(3, resultset.Rows[1]["id"]);
        Assert.Equal("Charlotte", resultset.Rows[1]["name"]);
    }

    [Fact]
    public void QueryAsDataTable_Upper_InWhere()
    {
        const string databaseName = "MyDB";

        // SELECT id, name FROM employees WHERE UPPER(name) = 'ALICE'
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn idCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        sqlSelect.Columns.Add(idCol);

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        sqlSelect.Columns.Add(nameCol);

        sqlSelect.Table = employeesTable;

        // WHERE UPPER(name) = 'ALICE'
        SqlColumn whereNameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlFunction upperFunc = new("UPPER") { ValueType = typeof(string) };
        upperFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = whereNameCol }));
        SqlBinaryExpression whereClause = new(
            new SqlExpression(upperFunc),
            SqlBinaryOperator.Equal,
            new SqlExpression(new SqlLiteralValue("ALICE")));
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, "Bob");
        employees.Rows.Add(3, "alice");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(1, resultset.Rows[0]["id"]);
        Assert.Equal(3, resultset.Rows[1]["id"]);
    }

    #endregion

    #region Bug Fix: MAX/MIN aggregate DataTable format

    [Fact]
    public void QueryAsDataTable_Max_ColumnName_MatchesSourceColumn()
    {
        const string databaseName = "MyDB";

        // SELECT MAX(price) FROM products — no alias, column name should include column arg
        SqlSelectDefinition sqlSelect = new();
        SqlTable productsTable = new(databaseName, "products");

        SqlColumn priceCol = new(databaseName, "products", "price") { ColumnType = typeof(double), TableRef = productsTable };
        SqlColumnRef priceRef = new(null, null, "price") { Column = priceCol };
        SqlAggregate maxAgg = new("MAX", new SqlExpression(priceRef));
        sqlSelect.Columns.Add(maxAgg);
        sqlSelect.Table = productsTable;

        DataSet dataSet = new(databaseName);
        DataTable products = new("products");
        products.Columns.Add("price", typeof(double));
        products.Rows.Add(10.0);
        products.Rows.Add(50.0);
        products.Rows.Add(30.0);
        dataSet.Tables.Add(products);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal("MAX(price)", resultset.Columns[0].ColumnName);
        Assert.Equal(typeof(double), resultset.Columns[0].DataType);
        Assert.Equal(50.0, resultset.Rows[0][0]);
    }

    [Fact]
    public void QueryAsDataTable_Max_WithAlias_UsesAlias()
    {
        const string databaseName = "MyDB";

        // SELECT MAX(price) AS max_price FROM products
        SqlSelectDefinition sqlSelect = new();
        SqlTable productsTable = new(databaseName, "products");

        SqlColumn priceCol = new(databaseName, "products", "price") { ColumnType = typeof(double), TableRef = productsTable };
        SqlColumnRef priceRef = new(null, null, "price") { Column = priceCol };
        SqlAggregate maxAgg = new("MAX", new SqlExpression(priceRef)) { ColumnAlias = "max_price" };
        sqlSelect.Columns.Add(maxAgg);
        sqlSelect.Table = productsTable;

        DataSet dataSet = new(databaseName);
        DataTable products = new("products");
        products.Columns.Add("price", typeof(double));
        products.Rows.Add(10.0);
        products.Rows.Add(50.0);
        dataSet.Tables.Add(products);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal("max_price", resultset.Columns[0].ColumnName);
        Assert.Equal(typeof(double), resultset.Columns[0].DataType);
        Assert.Equal(50.0, resultset.Rows[0]["max_price"]);
    }

    [Fact]
    public void QueryAsDataTable_Max_IntColumn_CorrectType()
    {
        const string databaseName = "MyDB";

        // SELECT MAX(age) AS max_age FROM employees — int column should produce int type
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn ageCol = new(databaseName, "employees", "age") { ColumnType = typeof(int), TableRef = employeesTable };
        SqlColumnRef ageRef = new(null, null, "age") { Column = ageCol };
        SqlAggregate maxAgg = new("MAX", new SqlExpression(ageRef)) { ColumnAlias = "max_age" };
        sqlSelect.Columns.Add(maxAgg);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("age", typeof(int));
        employees.Rows.Add(25);
        employees.Rows.Add(42);
        employees.Rows.Add(31);
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal(typeof(int), resultset.Columns[0].DataType);
        Assert.Equal(42, resultset.Rows[0]["max_age"]);
    }

    [Fact]
    public void QueryAsDataTable_Min_IntColumn_CorrectType()
    {
        const string databaseName = "MyDB";

        // SELECT MIN(age) AS min_age FROM employees
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn ageCol = new(databaseName, "employees", "age") { ColumnType = typeof(int), TableRef = employeesTable };
        SqlColumnRef ageRef = new(null, null, "age") { Column = ageCol };
        SqlAggregate minAgg = new("MIN", new SqlExpression(ageRef)) { ColumnAlias = "min_age" };
        sqlSelect.Columns.Add(minAgg);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("age", typeof(int));
        employees.Rows.Add(25);
        employees.Rows.Add(42);
        employees.Rows.Add(31);
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal(typeof(int), resultset.Columns[0].DataType);
        Assert.Equal(25, resultset.Rows[0]["min_age"]);
    }

    #endregion

    #region Bug Fix: Aggregate with null TableRef on column argument (#116)

    [Fact]
    public void QueryAsDataTable_Max_NoTableRef_ReturnsCorrectValue()
    {
        const string databaseName = "MyDB";

        // SELECT MAX(price) FROM products — aggregate column arg has no TableRef
        SqlSelectDefinition sqlSelect = new();
        SqlTable productsTable = new(databaseName, "products");

        // Column without TableRef (simulates parser not setting table reference)
        SqlColumn priceCol = new(databaseName, "products", "price") { ColumnType = typeof(double) };
        SqlColumnRef priceRef = new(null, null, "price") { Column = priceCol };
        SqlAggregate maxAgg = new("MAX", new SqlExpression(priceRef));
        sqlSelect.Columns.Add(maxAgg);
        sqlSelect.Table = productsTable;

        DataSet dataSet = new(databaseName);
        DataTable products = new("products");
        products.Columns.Add("price", typeof(double));
        products.Rows.Add(10.0);
        products.Rows.Add(50.0);
        products.Rows.Add(30.0);
        dataSet.Tables.Add(products);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal("MAX(price)", resultset.Columns[0].ColumnName);
        Assert.Equal(typeof(double), resultset.Columns[0].DataType);
        Assert.Equal(50.0, resultset.Rows[0][0]);
    }

    [Fact]
    public void QueryAsDataTable_Min_NoTableRef_ReturnsCorrectValue()
    {
        const string databaseName = "MyDB";

        // SELECT MIN(age) AS min_age FROM employees — no TableRef
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn ageCol = new(databaseName, "employees", "age") { ColumnType = typeof(int) };
        SqlColumnRef ageRef = new(null, null, "age") { Column = ageCol };
        SqlAggregate minAgg = new("MIN", new SqlExpression(ageRef)) { ColumnAlias = "min_age" };
        sqlSelect.Columns.Add(minAgg);
        sqlSelect.Table = employeesTable;

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("age", typeof(int));
        employees.Rows.Add(25);
        employees.Rows.Add(42);
        employees.Rows.Add(31);
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        Assert.Equal(typeof(int), resultset.Columns[0].DataType);
        Assert.Equal(25, resultset.Rows[0]["min_age"]);
    }

    [Fact]
    public void QueryAsDataTable_Max_NullableColumn_CorrectType()
    {
        const string databaseName = "MyDB";

        // SELECT MAX(score) FROM results — nullable column type
        SqlSelectDefinition sqlSelect = new();
        SqlTable resultsTable = new(databaseName, "results");

        SqlColumn scoreCol = new(databaseName, "results", "score") { ColumnType = typeof(int?), TableRef = resultsTable };
        SqlColumnRef scoreRef = new(null, null, "score") { Column = scoreCol };
        SqlAggregate maxAgg = new("MAX", new SqlExpression(scoreRef)) { ColumnAlias = "max_score" };
        sqlSelect.Columns.Add(maxAgg);
        sqlSelect.Table = resultsTable;

        DataSet dataSet = new(databaseName);
        DataTable results = new("results");
        results.Columns.Add("score", typeof(int));
        results.Rows.Add(85);
        results.Rows.Add(92);
        results.Rows.Add(78);
        dataSet.Tables.Add(results);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Single(resultset.Rows);
        // Nullable<int> should be unwrapped to int for the DataColumn type
        Assert.Equal(typeof(int), resultset.Columns[0].DataType);
        Assert.Equal(92, resultset.Rows[0]["max_score"]);
    }

    #endregion

    #region Bug Fix: Nested functions and combined predicates in WHERE (#115)

    [Fact]
    public void QueryAsDataTable_NestedFunction_InWhere()
    {
        const string databaseName = "MyDB";

        // SELECT id, name FROM employees WHERE UPPER(LOWER(name)) = 'ALICE'
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn idCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        sqlSelect.Columns.Add(idCol);

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        sqlSelect.Columns.Add(nameCol);

        sqlSelect.Table = employeesTable;

        // WHERE UPPER(LOWER(name)) = 'ALICE'
        SqlColumn whereNameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlFunction lowerFunc = new("LOWER") { ValueType = typeof(string) };
        lowerFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = whereNameCol }));

        SqlFunction upperFunc = new("UPPER") { ValueType = typeof(string) };
        upperFunc.Arguments.Add(new SqlExpression(lowerFunc));

        SqlBinaryExpression whereClause = new(
            new SqlExpression(upperFunc),
            SqlBinaryOperator.Equal,
            new SqlExpression(new SqlLiteralValue("ALICE")));
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, "Bob");
        employees.Rows.Add(3, "ALICE");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(1, resultset.Rows[0]["id"]);
        Assert.Equal(3, resultset.Rows[1]["id"]);
    }

    [Fact]
    public void QueryAsDataTable_FunctionWithAnd_InWhere()
    {
        const string databaseName = "MyDB";

        // SELECT id, name FROM employees WHERE UPPER(name) = 'ALICE' AND id > 1
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn idCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        sqlSelect.Columns.Add(idCol);

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        sqlSelect.Columns.Add(nameCol);

        sqlSelect.Table = employeesTable;

        // WHERE UPPER(name) = 'ALICE' AND id > 1
        SqlColumn whereNameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        SqlFunction upperFunc = new("UPPER") { ValueType = typeof(string) };
        upperFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = whereNameCol }));

        SqlBinaryExpression upperEqualsAlice = new(
            new SqlExpression(upperFunc),
            SqlBinaryOperator.Equal,
            new SqlExpression(new SqlLiteralValue("ALICE")));

        SqlColumn whereIdCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        SqlBinaryExpression idGreaterThan1 = new(
            new SqlExpression(new SqlColumnRef(null, null, "id") { Column = whereIdCol }),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(1)));

        SqlBinaryExpression combinedWhere = new(
            new SqlExpression(upperEqualsAlice),
            SqlBinaryOperator.And,
            new SqlExpression(idGreaterThan1));
        sqlSelect.WhereClause = new SqlExpression(combinedWhere);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, "Bob");
        employees.Rows.Add(3, "alice");
        employees.Rows.Add(4, "ALICE");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        // Only rows where UPPER(name) = 'ALICE' AND id > 1: rows 3 and 4
        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(3, resultset.Rows[0]["id"]);
        Assert.Equal(4, resultset.Rows[1]["id"]);
    }

    [Fact]
    public void QueryAsDataTable_Function_InWhere_NoTableRef()
    {
        const string databaseName = "MyDB";

        // SELECT id, name FROM employees WHERE LENGTH(name) > 3
        // Function arg column has no TableRef
        SqlSelectDefinition sqlSelect = new();
        SqlTable employeesTable = new(databaseName, "employees");

        SqlColumn idCol = new(databaseName, "employees", "id") { ColumnType = typeof(int), TableRef = employeesTable };
        sqlSelect.Columns.Add(idCol);

        SqlColumn nameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string), TableRef = employeesTable };
        sqlSelect.Columns.Add(nameCol);

        sqlSelect.Table = employeesTable;

        // WHERE LENGTH(name) > 3 — function arg column has NO TableRef
        SqlColumn whereNameCol = new(databaseName, "employees", "name") { ColumnType = typeof(string) };
        SqlFunction lengthFunc = new("LENGTH") { ValueType = typeof(int) };
        lengthFunc.Arguments.Add(new SqlExpression(new SqlColumnRef(null, null, "name") { Column = whereNameCol }));
        SqlBinaryExpression whereClause = new(
            new SqlExpression(lengthFunc),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(3)));
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, "Bo");
        employees.Rows.Add(3, "Charlotte");
        employees.Rows.Add(4, "Ed");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(1, resultset.Rows[0]["id"]);
        Assert.Equal("Alice", resultset.Rows[0]["name"]);
        Assert.Equal(3, resultset.Rows[1]["id"]);
        Assert.Equal("Charlotte", resultset.Rows[1]["name"]);
    }

    #endregion

    #region IN / NOT IN Tests

    [Fact]
    public void QueryAsDataTable_WhereIn_FiltersCorrectly()
    {
        const string databaseName = "MyDB";

        // SELECT id, city FROM locations WHERE id IN (1, 3)
        SqlSelectDefinition sqlSelect = new();
        SqlColumn idCol = new(databaseName, "locations", "id") { ColumnType = typeof(int) };
        SqlColumn cityCol = new(databaseName, "locations", "city") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(cityCol);

        SqlTable locationsTable = new(databaseName, "locations");
        idCol.TableRef = locationsTable;
        cityCol.TableRef = locationsTable;
        sqlSelect.Table = locationsTable;

        // WHERE id IN (1, 3)
        SqlColumnRef idColumnRef = new(null, null, "id") { Column = idCol };
        var inList = new SqlInList(new List<SqlExpression>
        {
            new SqlExpression(new SqlLiteralValue(1)),
            new SqlExpression(new SqlLiteralValue(3))
        });
        var whereClause = new SqlBinaryExpression(
            new SqlExpression(idColumnRef),
            SqlBinaryOperator.In,
            new SqlExpression(inList));
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable locations = new("locations");
        locations.Columns.Add("id", typeof(int));
        locations.Columns.Add("city", typeof(string));
        locations.Rows.Add(1, "Houston");
        locations.Rows.Add(2, "Dallas");
        locations.Rows.Add(3, "Austin");
        locations.Rows.Add(4, "San Antonio");
        dataSet.Tables.Add(locations);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(1, resultset.Rows[0]["id"]);
        Assert.Equal("Houston", resultset.Rows[0]["city"]);
        Assert.Equal(3, resultset.Rows[1]["id"]);
        Assert.Equal("Austin", resultset.Rows[1]["city"]);
    }

    [Fact]
    public void QueryAsDataTable_WhereNotIn_FiltersCorrectly()
    {
        const string databaseName = "MyDB";

        // SELECT id, city FROM locations WHERE id NOT IN (1, 3)
        SqlSelectDefinition sqlSelect = new();
        SqlColumn idCol = new(databaseName, "locations", "id") { ColumnType = typeof(int) };
        SqlColumn cityCol = new(databaseName, "locations", "city") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(cityCol);

        SqlTable locationsTable = new(databaseName, "locations");
        idCol.TableRef = locationsTable;
        cityCol.TableRef = locationsTable;
        sqlSelect.Table = locationsTable;

        // WHERE id NOT IN (1, 3)
        SqlColumnRef idColumnRef = new(null, null, "id") { Column = idCol };
        var inList = new SqlInList(new List<SqlExpression>
        {
            new SqlExpression(new SqlLiteralValue(1)),
            new SqlExpression(new SqlLiteralValue(3))
        });
        var whereClause = new SqlBinaryExpression(
            new SqlExpression(idColumnRef),
            SqlBinaryOperator.NotIn,
            new SqlExpression(inList));
        sqlSelect.WhereClause = new SqlExpression(whereClause);

        DataSet dataSet = new(databaseName);
        DataTable locations = new("locations");
        locations.Columns.Add("id", typeof(int));
        locations.Columns.Add("city", typeof(string));
        locations.Rows.Add(1, "Houston");
        locations.Rows.Add(2, "Dallas");
        locations.Rows.Add(3, "Austin");
        locations.Rows.Add(4, "San Antonio");
        dataSet.Tables.Add(locations);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);
        Assert.Equal(2, resultset.Rows[0]["id"]);
        Assert.Equal("Dallas", resultset.Rows[0]["city"]);
        Assert.Equal(4, resultset.Rows[1]["id"]);
        Assert.Equal("San Antonio", resultset.Rows[1]["city"]);
    }

    #endregion

    #region GROUP BY Tests

    [Fact]
    public void QueryAsDataTable_GroupBy_WithCount()
    {
        const string databaseName = "MyDB";

        // SELECT department, COUNT(*) as cnt FROM employees GROUP BY department
        SqlSelectDefinition sqlSelect = new();

        SqlColumn deptCol = new(databaseName, "employees", "department") { ColumnType = typeof(string) };
        SqlTable employeesTable = new(databaseName, "employees");
        deptCol.TableRef = employeesTable;
        sqlSelect.Columns.Add(deptCol);

        SqlAggregate countAgg = new("COUNT") { ColumnAlias = "cnt" };
        sqlSelect.Columns.Add(countAgg);

        sqlSelect.Table = employeesTable;
        sqlSelect.GroupBy = new SqlGroupByClause();
        sqlSelect.GroupBy.Columns.Add("department");

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Columns.Add("department", typeof(string));
        employees.Rows.Add(1, "Alice", "Engineering");
        employees.Rows.Add(2, "Bob", "Engineering");
        employees.Rows.Add(3, "Carol", "Sales");
        employees.Rows.Add(4, "Dave", "Engineering");
        employees.Rows.Add(5, "Eve", "Sales");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);

        // Find Engineering and Sales groups
        var engRow = resultset.Rows.Cast<DataRow>().First(r => r["department"].ToString() == "Engineering");
        var salesRow = resultset.Rows.Cast<DataRow>().First(r => r["department"].ToString() == "Sales");

        Assert.Equal(3, engRow["cnt"]);
        Assert.Equal(2, salesRow["cnt"]);
    }

    [Fact]
    public void QueryAsDataTable_GroupBy_WithSum()
    {
        const string databaseName = "MyDB";

        // SELECT department, SUM(salary) as total FROM employees GROUP BY department
        SqlSelectDefinition sqlSelect = new();

        SqlColumn deptCol = new(databaseName, "employees", "department") { ColumnType = typeof(string) };
        SqlTable employeesTable = new(databaseName, "employees");
        deptCol.TableRef = employeesTable;
        sqlSelect.Columns.Add(deptCol);

        SqlColumn salaryCol = new(databaseName, "employees", "salary") { ColumnType = typeof(decimal) };
        salaryCol.TableRef = employeesTable;
        SqlColumnRef salaryRef = new(null, null, "salary") { Column = salaryCol };
        SqlAggregate sumAgg = new("SUM", new SqlExpression(salaryRef)) { ColumnAlias = "total" };
        sqlSelect.Columns.Add(sumAgg);

        sqlSelect.Table = employeesTable;
        sqlSelect.GroupBy = new SqlGroupByClause();
        sqlSelect.GroupBy.Columns.Add("department");

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Columns.Add("department", typeof(string));
        employees.Columns.Add("salary", typeof(decimal));
        employees.Rows.Add(1, "Alice", "Engineering", 100000m);
        employees.Rows.Add(2, "Bob", "Engineering", 90000m);
        employees.Rows.Add(3, "Carol", "Sales", 80000m);
        employees.Rows.Add(4, "Dave", "Engineering", 110000m);
        employees.Rows.Add(5, "Eve", "Sales", 75000m);
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        Assert.Equal(2, resultset.Rows.Count);

        var engRow = resultset.Rows.Cast<DataRow>().First(r => r["department"].ToString() == "Engineering");
        var salesRow = resultset.Rows.Cast<DataRow>().First(r => r["department"].ToString() == "Sales");

        Assert.Equal(300000m, Convert.ToDecimal(engRow["total"]));
        Assert.Equal(155000m, Convert.ToDecimal(salesRow["total"]));
    }

    #endregion

    #region HAVING Tests

    [Fact]
    public void QueryAsDataTable_GroupBy_WithHaving()
    {
        const string databaseName = "MyDB";

        // SELECT department, COUNT(*) as cnt FROM employees GROUP BY department HAVING COUNT(*) > 2
        SqlSelectDefinition sqlSelect = new();

        SqlColumn deptCol = new(databaseName, "employees", "department") { ColumnType = typeof(string) };
        SqlTable employeesTable = new(databaseName, "employees");
        deptCol.TableRef = employeesTable;
        sqlSelect.Columns.Add(deptCol);

        SqlAggregate countAgg = new("COUNT") { ColumnAlias = "cnt" };
        sqlSelect.Columns.Add(countAgg);

        sqlSelect.Table = employeesTable;
        sqlSelect.GroupBy = new SqlGroupByClause();
        sqlSelect.GroupBy.Columns.Add("department");

        // HAVING COUNT(*) > 2 — reference the "cnt" column in the result
        SqlColumnRef cntRef = new(null, null, "cnt") { Column = new SqlColumn(null, null, "cnt") { ColumnType = typeof(int) } };
        var havingClause = new SqlBinaryExpression(
            new SqlExpression(cntRef),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(2)));
        sqlSelect.HavingClause = new SqlExpression(havingClause);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Columns.Add("department", typeof(string));
        employees.Rows.Add(1, "Alice", "Engineering");
        employees.Rows.Add(2, "Bob", "Engineering");
        employees.Rows.Add(3, "Carol", "Sales");
        employees.Rows.Add(4, "Dave", "Engineering");
        employees.Rows.Add(5, "Eve", "Sales");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        // Only Engineering has > 2 employees
        Assert.Equal(1, resultset.Rows.Count);
        Assert.Equal("Engineering", resultset.Rows[0]["department"]);
        Assert.Equal(3, resultset.Rows[0]["cnt"]);
    }

    [Fact]
    public void QueryAsDataTable_GroupBy_WithHaving_AggregateFunctionRef()
    {
        const string databaseName = "MyDB";

        // SELECT department, COUNT(*) as cnt FROM employees GROUP BY department HAVING COUNT(*) > 2
        // This time the HAVING clause references the aggregate as an SqlFunction (as the parser produces)
        // rather than by column alias.
        SqlSelectDefinition sqlSelect = new();

        SqlColumn deptCol = new(databaseName, "employees", "department") { ColumnType = typeof(string) };
        SqlTable employeesTable = new(databaseName, "employees");
        deptCol.TableRef = employeesTable;
        sqlSelect.Columns.Add(deptCol);

        SqlAggregate countAgg = new("COUNT") { ColumnAlias = "cnt" };
        sqlSelect.Columns.Add(countAgg);

        sqlSelect.Table = employeesTable;
        sqlSelect.GroupBy = new SqlGroupByClause();
        sqlSelect.GroupBy.Columns.Add("department");

        // HAVING COUNT(*) > 2 — reference as SqlFunction (how the parser creates it)
        var countFunc = new SqlFunction("COUNT");
        var havingClause = new SqlBinaryExpression(
            new SqlExpression(countFunc),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(2)));
        sqlSelect.HavingClause = new SqlExpression(havingClause);

        DataSet dataSet = new(databaseName);
        DataTable employees = new("employees");
        employees.Columns.Add("id", typeof(int));
        employees.Columns.Add("name", typeof(string));
        employees.Columns.Add("department", typeof(string));
        employees.Rows.Add(1, "Alice", "Engineering");
        employees.Rows.Add(2, "Bob", "Engineering");
        employees.Rows.Add(3, "Carol", "Sales");
        employees.Rows.Add(4, "Dave", "Engineering");
        employees.Rows.Add(5, "Eve", "Sales");
        dataSet.Tables.Add(employees);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        var resultset = queryEngine.QueryAsDataTable();

        // Only Engineering has > 2 employees
        Assert.Equal(1, resultset.Rows.Count);
        Assert.Equal("Engineering", resultset.Rows[0]["department"]);
        Assert.Equal(3, resultset.Rows[0]["cnt"]);
    }

    #endregion

    #region DISTINCT (#123)

    [Fact]
    public void QueryAsDataTable_Distinct_RemovesDuplicateRows()
    {
        const string databaseName = "MyDB";

        // SELECT DISTINCT city FROM customers
        SqlSelectDefinition sqlSelect = new() { IsDistinct = true };
        SqlColumn cityCol = new(databaseName, "customers", "city") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(cityCol);

        SqlTable customersTable = new(databaseName, "customers");
        cityCol.TableRef = customersTable;
        sqlSelect.Table = customersTable;

        DataSet dataSet = new(databaseName);
        DataTable customers = new("customers");
        customers.Columns.Add("city", typeof(string));
        customers.Rows.Add("New York");
        customers.Rows.Add("Chicago");
        customers.Rows.Add("New York");
        customers.Rows.Add("Chicago");
        customers.Rows.Add("Boston");
        dataSet.Tables.Add(customers);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        DataTable result = queryEngine.QueryAsDataTable();

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal("New York", result.Rows[0]["city"]);
        Assert.Equal("Chicago", result.Rows[1]["city"]);
        Assert.Equal("Boston", result.Rows[2]["city"]);
    }

    [Fact]
    public void QueryAsDataTable_Distinct_MultipleColumns()
    {
        const string databaseName = "MyDB";

        // SELECT DISTINCT city, state FROM locations
        SqlSelectDefinition sqlSelect = new() { IsDistinct = true };
        SqlColumn cityCol = new(databaseName, "locations", "city") { ColumnType = typeof(string) };
        SqlColumn stateCol = new(databaseName, "locations", "state") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(cityCol);
        sqlSelect.Columns.Add(stateCol);

        SqlTable locationsTable = new(databaseName, "locations");
        cityCol.TableRef = locationsTable;
        stateCol.TableRef = locationsTable;
        sqlSelect.Table = locationsTable;

        DataSet dataSet = new(databaseName);
        DataTable locations = new("locations");
        locations.Columns.Add("city", typeof(string));
        locations.Columns.Add("state", typeof(string));
        locations.Rows.Add("Portland", "OR");
        locations.Rows.Add("Portland", "ME");
        locations.Rows.Add("Portland", "OR");
        locations.Rows.Add("Springfield", "IL");
        locations.Rows.Add("Springfield", "IL");
        dataSet.Tables.Add(locations);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        DataTable result = queryEngine.QueryAsDataTable();

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal("Portland", result.Rows[0]["city"]);
        Assert.Equal("OR", result.Rows[0]["state"]);
        Assert.Equal("Portland", result.Rows[1]["city"]);
        Assert.Equal("ME", result.Rows[1]["state"]);
        Assert.Equal("Springfield", result.Rows[2]["city"]);
        Assert.Equal("IL", result.Rows[2]["state"]);
    }

    [Fact]
    public void QueryAsDataTable_WithoutDistinct_KeepsDuplicates()
    {
        const string databaseName = "MyDB";

        // SELECT city FROM customers (no DISTINCT)
        SqlSelectDefinition sqlSelect = new();
        SqlColumn cityCol = new(databaseName, "customers", "city") { ColumnType = typeof(string) };
        sqlSelect.Columns.Add(cityCol);

        SqlTable customersTable = new(databaseName, "customers");
        cityCol.TableRef = customersTable;
        sqlSelect.Table = customersTable;

        DataSet dataSet = new(databaseName);
        DataTable customers = new("customers");
        customers.Columns.Add("city", typeof(string));
        customers.Rows.Add("New York");
        customers.Rows.Add("New York");
        customers.Rows.Add("Chicago");
        dataSet.Tables.Add(customers);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        DataTable result = queryEngine.QueryAsDataTable();

        Assert.Equal(3, result.Rows.Count);
    }

    #endregion

    #region OUTER JOIN Tests

    /// <summary>
    /// Helper: builds a DataSet with Customers and Orders tables for outer join tests.
    /// Customers: (1, Alice), (2, Bob), (3, Charlie)
    /// Orders: (101, 1, "Widget"), (102, 1, "Gadget"), (103, 2, "Doohickey"), (104, 99, "Orphan")
    /// Customer 3 (Charlie) has no orders.  Order 104 has no matching customer (CustomerID=99).
    /// </summary>
    private static (DataSet dataSet, SqlTable customersTable, SqlTable ordersTable,
                     SqlColumn nameCol, SqlColumn productCol,
                     SqlColumn custIdCol, SqlColumn orderCustIdCol) BuildOuterJoinDataSet()
    {
        const string db = "TestDB";
        DataSet dataSet = new(db);

        DataTable customers = new("Customers");
        customers.Columns.Add("ID", typeof(int));
        customers.Columns.Add("Name", typeof(string));
        dataSet.Tables.Add(customers);
        customers.Rows.Add(1, "Alice");
        customers.Rows.Add(2, "Bob");
        customers.Rows.Add(3, "Charlie");

        DataTable orders = new("Orders");
        orders.Columns.Add("OrderID", typeof(int));
        orders.Columns.Add("CustomerID", typeof(int));
        orders.Columns.Add("Product", typeof(string));
        dataSet.Tables.Add(orders);
        orders.Rows.Add(101, 1, "Widget");
        orders.Rows.Add(102, 1, "Gadget");
        orders.Rows.Add(103, 2, "Doohickey");
        orders.Rows.Add(104, 99, "Orphan");

        SqlTable customersTable = new(db, "Customers") { TableAlias = "c" };
        SqlTable ordersTable = new(db, "Orders") { TableAlias = "o" };

        SqlColumn nameCol = new(null, "c", "Name") { ColumnType = typeof(string), TableRef = customersTable };
        SqlColumn productCol = new(null, "o", "Product") { ColumnType = typeof(string), TableRef = ordersTable };

        SqlColumn custIdCol = new(db, "Customers", "ID") { ColumnType = typeof(int), TableRef = customersTable };
        SqlColumn orderCustIdCol = new(db, "Orders", "CustomerID") { ColumnType = typeof(int), TableRef = ordersTable };

        return (dataSet, customersTable, ordersTable, nameCol, productCol, custIdCol, orderCustIdCol);
    }

    private static SqlSelectDefinition BuildOuterJoinSelect(SqlJoinKind joinKind)
    {
        var (dataSet, customersTable, ordersTable, nameCol, productCol, custIdCol, orderCustIdCol) = BuildOuterJoinDataSet();

        SqlSelectDefinition sqlSelect = new();
        sqlSelect.Columns.Add(nameCol);
        sqlSelect.Columns.Add(productCol);
        sqlSelect.Table = customersTable;

        SqlColumnRef custIdRef = new(null, "c", "ID") { Column = custIdCol };
        SqlColumnRef orderCustIdRef = new(null, "o", "CustomerID") { Column = orderCustIdCol };

        SqlJoin join = new(ordersTable, new SqlBinaryExpression(new(custIdRef), SqlBinaryOperator.Equal, new(orderCustIdRef)));
        join.JoinKind = joinKind;
        sqlSelect.Joins.Add(join);

        return sqlSelect;
    }

    [Fact]
    public void LeftJoin_UnmatchedLeftRows_ReturnNullForRightColumns()
    {
        // SELECT c.Name, o.Product FROM Customers c LEFT JOIN Orders o ON c.ID = o.CustomerID
        // Alice has 2 orders, Bob has 1, Charlie has 0 → 4 rows, Charlie row has NULL product
        var (dataSet, _, _, _, _, _, _) = BuildOuterJoinDataSet();
        var sqlSelect = BuildOuterJoinSelect(SqlJoinKind.Left);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        DataTable result = queryEngine.QueryAsDataTable();

        Assert.Equal(4, result.Rows.Count);

        // Verify Charlie appears with NULL product
        var charlieRows = result.AsEnumerable().Where(r => "Charlie".Equals(r["Name"])).ToList();
        Assert.Single(charlieRows);
        Assert.Equal(DBNull.Value, charlieRows[0]["Product"]);

        // Verify matched rows are correct
        var aliceRows = result.AsEnumerable().Where(r => "Alice".Equals(r["Name"])).ToList();
        Assert.Equal(2, aliceRows.Count);
    }

    [Fact]
    public void RightJoin_UnmatchedRightRows_ReturnNullForLeftColumns()
    {
        // SELECT c.Name, o.Product FROM Customers c RIGHT JOIN Orders o ON c.ID = o.CustomerID
        // Orders 101-103 match customers. Order 104 (Orphan) has no customer → NULL name.
        var (dataSet, _, _, _, _, _, _) = BuildOuterJoinDataSet();
        var sqlSelect = BuildOuterJoinSelect(SqlJoinKind.Right);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        DataTable result = queryEngine.QueryAsDataTable();

        Assert.Equal(4, result.Rows.Count);

        // Verify the orphan order appears with NULL name
        var orphanRows = result.AsEnumerable().Where(r => "Orphan".Equals(r["Product"])).ToList();
        Assert.Single(orphanRows);
        Assert.Equal(DBNull.Value, orphanRows[0]["Name"]);

        // Verify matched rows exist
        var widgetRows = result.AsEnumerable().Where(r => "Widget".Equals(r["Product"])).ToList();
        Assert.Single(widgetRows);
        Assert.Equal("Alice", widgetRows[0]["Name"]);
    }

    [Fact]
    public void FullJoin_UnmatchedBothSides_ReturnNullForMissingColumns()
    {
        // SELECT c.Name, o.Product FROM Customers c FULL JOIN Orders o ON c.ID = o.CustomerID
        // 3 matched rows + Charlie (no orders, NULL product) + Orphan (no customer, NULL name) = 5 rows
        var (dataSet, _, _, _, _, _, _) = BuildOuterJoinDataSet();
        var sqlSelect = BuildOuterJoinSelect(SqlJoinKind.Full);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        DataTable result = queryEngine.QueryAsDataTable();

        Assert.Equal(5, result.Rows.Count);

        // Charlie has NULL product (LEFT side unmatched)
        var charlieRows = result.AsEnumerable().Where(r => "Charlie".Equals(r["Name"])).ToList();
        Assert.Single(charlieRows);
        Assert.Equal(DBNull.Value, charlieRows[0]["Product"]);

        // Orphan has NULL name (RIGHT side unmatched)
        var orphanRows = result.AsEnumerable().Where(r => "Orphan".Equals(r["Product"])).ToList();
        Assert.Single(orphanRows);
        Assert.Equal(DBNull.Value, orphanRows[0]["Name"]);
    }

    [Fact]
    public void InnerJoin_Unchanged_NoNullPaddedRows()
    {
        // Verify INNER JOIN still works correctly (no regression) — only matched rows
        var (dataSet, _, _, _, _, _, _) = BuildOuterJoinDataSet();
        var sqlSelect = BuildOuterJoinSelect(SqlJoinKind.Inner);

        QueryEngine queryEngine = new(new DataSet[] { dataSet }, sqlSelect);
        DataTable result = queryEngine.QueryAsDataTable();

        // Only 3 matched rows (Alice×2, Bob×1). No Charlie, no Orphan.
        Assert.Equal(3, result.Rows.Count);
    }

    #endregion
}

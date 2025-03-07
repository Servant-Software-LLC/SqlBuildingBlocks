using SqlBuildingBlocks.POCOs;
using System.Data;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.POCOs;

public class VirtualDataTableCollectionTests
{
    private class DisposableVirtualDataTable(string name, Action disposeFunc) : VirtualDataTable(name), IDisposable
    {
        public void Dispose() => disposeFunc();
    }

    [Fact]
    public void Remove_DisposesVirtualDataTable()
    {
        const string tableName = "FakeTable";

        //Setup
        bool disposed = false;
        VirtualDataSet dataSet = new();
        dataSet.Tables.Add(new DisposableVirtualDataTable(tableName, () => disposed = true));

        //Act
        dataSet.Tables.Remove(tableName);

        Assert.True(disposed);
    }

    [Fact]
    public void Indexer_CaseInsensitive()
    {
        /* Copy this behavior in our unit test

        DataSet dataSet = new DataSet();
        dataSet.Tables.Add(new DataTable("blogs"));

        var table = dataSet.Tables["Blogs"];
        Assert.NotNull(table);
        */

        VirtualDataSet dataSet = new();
        dataSet.Tables.Add(new VirtualDataTable("blogs"));

        var table = dataSet.Tables["Blogs"];
        Assert.NotNull(table);

    }

    [Fact]
    public void AppendRow_AddsValidRow()
    {
        // Arrange: Create a VirtualDataTable and a valid foreign row.
        VirtualDataTable vdt = CreateVirtualDataTable();
        DataRow foreignRow = CreateForeignRowMatchingSchema(1, "Alice");

        // Act: Append the foreign row.
        vdt.AppendRow(foreignRow);

        // Assert: Ensure that Rows is not null and contains exactly one row.
        Assert.NotNull(vdt.Rows);
        var allRows = vdt.Rows.ToList();
        Assert.Single(allRows);

        // Verify the values.
        DataRow newRow = allRows.First();
        Assert.Equal(1, newRow["Id"]);
        Assert.Equal("Alice", newRow["Name"]);

        // Verify that the new row is using the same DataColumn instances as in VirtualDataTable.Columns.
        foreach (DataColumn col in vdt.Columns)
        {
            // The DataColumn in the new row's table should be the same instance.
            Assert.Same(col, newRow.Table.Columns[col.ColumnName]);
        }
    }

    [Fact]
    public void AppendRow_AppendsMultipleRows()
    {
        // Arrange: Create a VirtualDataTable and two valid foreign rows.
        VirtualDataTable vdt = CreateVirtualDataTable();
        DataRow foreignRow1 = CreateForeignRowMatchingSchema(1, "Alice");
        DataRow foreignRow2 = CreateForeignRowMatchingSchema(2, "Bob");

        // Act: Append both rows.
        vdt.AppendRow(foreignRow1);
        vdt.AppendRow(foreignRow2);

        // Assert: Check that both rows are present.
        var allRows = vdt.Rows.ToList();
        Assert.Equal(2, allRows.Count);
        Assert.Equal(1, allRows[0]["Id"]);
        Assert.Equal("Alice", allRows[0]["Name"]);
        Assert.Equal(2, allRows[1]["Id"]);
        Assert.Equal("Bob", allRows[1]["Name"]);
    }

    [Fact]
    public void AppendRow_ThrowsOnDifferentColumnCount()
    {
        // Arrange: Create a VirtualDataTable with a known schema.
        VirtualDataTable vdt = CreateVirtualDataTable();

        // Create a foreign DataRow with fewer columns (only "Id").
        DataTable foreignDt = new DataTable("ForeignTable");
        foreignDt.Columns.Add("Id", typeof(int));
        DataRow foreignRow = foreignDt.NewRow();
        foreignRow["Id"] = 1;

        // Act & Assert: Expect an exception due to different number of columns.
        var ex = Assert.Throws<InvalidOperationException>(() => vdt.AppendRow(foreignRow));
        Assert.Contains("does not have the same number of columns", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendRow_ThrowsOnMismatchedColumnName()
    {
        // Arrange: Create a VirtualDataTable with a known schema.
        VirtualDataTable vdt = CreateVirtualDataTable();

        // Create a foreign DataRow with the same number of columns but with one mismatched name.
        // VirtualDataTable expects columns "Id" and "Name", so we'll use "Id" and "FirstName" instead.
        DataTable foreignDt = new DataTable("ForeignTable");
        foreignDt.Columns.Add("Id", typeof(int));
        foreignDt.Columns.Add("FirstName", typeof(string));
        DataRow foreignRow = foreignDt.NewRow();
        foreignRow["Id"] = 1;
        foreignRow["FirstName"] = "Alice";

        // Act & Assert: Expect an exception due to the missing required column "Name".
        var ex = Assert.Throws<InvalidOperationException>(() => vdt.AppendRow(foreignRow));
        Assert.Contains("missing required column", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendRow_ThrowsOnTypeMismatch()
    {
        // Arrange: Create a VirtualDataTable with a specific schema.
        VirtualDataTable vdt = CreateVirtualDataTable();

        // Create a foreign DataRow with a type mismatch on the "Id" column (string instead of int).
        DataTable foreignDt = new DataTable("ForeignTable");
        foreignDt.Columns.Add("Id", typeof(string));  // Incorrect type.
        foreignDt.Columns.Add("Name", typeof(string));
        DataRow foreignRow = foreignDt.NewRow();
        foreignRow["Id"] = "NotAnInt";
        foreignRow["Name"] = "Alice";

        // Act & Assert: Expect an exception due to data type mismatch.
        var ex = Assert.Throws<InvalidOperationException>(() => vdt.AppendRow(foreignRow));
        Assert.Contains("Data type mismatch for column", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendRow_KV_AddsValidRow()
    {
        // Arrange: Create a VirtualDataTable and a valid key/value collection.
        VirtualDataTable vdt = CreateVirtualDataTable();
        var rowData = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("Id", 1),
                new KeyValuePair<string, object>("Name", "Alice")
            };

        // Act: Append the key/value row.
        vdt.AppendRow(rowData);

        // Assert: Verify that one row was added with the correct values.
        Assert.NotNull(vdt.Rows);
        var allRows = vdt.Rows.ToList();
        Assert.Single(allRows);

        DataRow newRow = allRows.First();
        Assert.Equal(1, newRow["Id"]);
        Assert.Equal("Alice", newRow["Name"]);

        // Confirm the new row's DataColumns are the same instances as those in VirtualDataTable.Columns.
        foreach (DataColumn col in vdt.Columns)
        {
            Assert.Same(col, newRow.Table.Columns[col.ColumnName]);
        }
    }

    [Fact]
    public void AppendRow_KV_ThrowsOnDifferentColumnCount()
    {
        // Arrange: Create a VirtualDataTable and a key/value collection with only one entry.
        VirtualDataTable vdt = CreateVirtualDataTable();
        var rowData = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("Id", 1)  // Missing "Name"
            };

        // Act & Assert: Expect an exception due to a different number of columns.
        var ex = Assert.Throws<InvalidOperationException>(() => vdt.AppendRow(rowData));
        Assert.Contains("does not have the same number of columns", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendRow_KV_ThrowsOnMismatchedColumnName()
    {
        // Arrange: Create a VirtualDataTable and a key/value collection with a mismatched column name.
        VirtualDataTable vdt = CreateVirtualDataTable();
        var rowData = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("Id", 1),
                // "FirstName" is used instead of the expected "Name"
                new KeyValuePair<string, object>("FirstName", "Alice")
            };

        // Act & Assert: Expect an exception due to the missing required column "Name".
        var ex = Assert.Throws<InvalidOperationException>(() => vdt.AppendRow(rowData));
        Assert.Contains("missing required column", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendRow_KV_ThrowsOnTypeMismatch()
    {
        // Arrange: Create a VirtualDataTable and a key/value collection with a type mismatch.
        VirtualDataTable vdt = CreateVirtualDataTable();
        var rowData = new List<KeyValuePair<string, object>>
            {
                // "Id" expects an int but a string is provided.
                new KeyValuePair<string, object>("Id", "NotAnInt"),
                new KeyValuePair<string, object>("Name", "Alice")
            };

        // Act & Assert: Expect an exception due to data type mismatch.
        var ex = Assert.Throws<InvalidOperationException>(() => vdt.AppendRow(rowData));
        Assert.Contains("Data type mismatch for column", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a DataTable with two columns ("Id" and "Name") and then uses it
    /// to initialize a VirtualDataTable. The VirtualDataTable will have its Columns
    /// property set to the DataColumnCollection of the underlying DataTable.
    /// </summary>
    private VirtualDataTable CreateVirtualDataTable()
    {
        DataTable dt = new DataTable("TestTable");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        return new VirtualDataTable(dt);
    }

    /// <summary>
    /// Creates a foreign DataRow from a separate DataTable that has an identical schema
    /// to the one used in VirtualDataTable. This row is used to simulate a DataRow coming
    /// from a different source but with the same column definitions.
    /// </summary>
    private DataRow CreateForeignRowMatchingSchema(int id, string name)
    {
        DataTable foreignDt = new DataTable("ForeignTable");
        foreignDt.Columns.Add("Id", typeof(int));
        foreignDt.Columns.Add("Name", typeof(string));
        DataRow row = foreignDt.NewRow();
        row["Id"] = id;
        row["Name"] = name;
        return row;
    }
}


using System.Data;

namespace SqlBuildingBlocks.POCOs;

/// <summary>
/// In order for us to support large data sources, the results coming from the QueryEngine must be in an enumerable iterator.  This iterator
/// will only pull rows from the data sources on demand.  Previously, we stored a whole DataTable of results which would require loading
/// all data sources and the whole result set into memory.
/// </summary>
public class VirtualDataTable
{
    public VirtualDataTable(string tableName) => 
        TableName = !string.IsNullOrEmpty(tableName) ? tableName : throw new ArgumentNullException(nameof(tableName));

    public VirtualDataTable(DataTable dataTable)
    {
        TableName = dataTable.TableName;
        Columns = dataTable.Columns;
        Rows = dataTable.Rows.Cast<DataRow>();
    }

    public string TableName { get; }
    public DataColumnCollection? Columns { get; set; }
    public IEnumerable<DataRow>? Rows { get; set; }

    public DataTable CreateEmptyDataTable()
    {
        DataTable table = new DataTable();
        SetSchema(table);

        return table;
    }

    /// <summary>
    /// Make a full copy of the schema and data contained in this virtual data table.  
    /// </summary>
    /// <remarks>Be careful, this call will copy of the data into memory.  The enumerable of <see cref="Rows"/> isn't necessarily in-memory.</remarks>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
        DataTable table = new DataTable(TableName);
        SetSchema(table);

        if (Rows != null)
        {
            //Copy the data.
            foreach (DataRow dataRow in Rows)
            {
                table.Rows.Add(dataRow.ItemArray);
            }
        }

        return table;
    }

    /// <summary>
    /// Appends a new row from a foreign DataRow.
    /// Validates that the foreign row exactly matches the VirtualDataTable schema.
    /// </summary>
    public void AppendRow(DataRow foreignRow)
    {
        if (foreignRow == null)
            throw new ArgumentNullException(nameof(foreignRow));

        if (Columns == null || Columns.Count == 0)
            throw new InvalidOperationException("VirtualDataTable.Columns is not set.");

        // Validate that the foreign row has the same number of columns.
        if (foreignRow.Table.Columns.Count != Columns.Count)
            throw new InvalidOperationException("Foreign row does not have the same number of columns as VirtualDataTable.");

        // Build a dictionary of column values after validating each column's existence and type.
        var data = new Dictionary<string, object>();
        foreach (DataColumn col in Columns)
        {
            if (!foreignRow.Table.Columns.Contains(col.ColumnName))
                throw new InvalidOperationException($"Foreign row is missing required column '{col.ColumnName}'.");

            var foreignCol = foreignRow.Table.Columns[col.ColumnName];
            if (foreignCol.DataType != col.DataType)
                throw new InvalidOperationException(
                    $"Data type mismatch for column '{col.ColumnName}'. Expected {col.DataType}, but got {foreignCol.DataType}.");

            data[col.ColumnName] = foreignRow[col.ColumnName];
        }

        // Create a new DataRow from the validated dictionary and append it.
        DataRow newRow = CreateNewRowFromData(data);
        AppendNewRow(newRow);
    }

    /// <summary>
    /// Appends a new row from an enumerable of key/value pairs.
    /// Validates that the provided data exactly matches the VirtualDataTable schema.
    /// </summary>
    public void AppendRow(IEnumerable<KeyValuePair<string, object>> rowData)
    {
        if (rowData == null)
            throw new ArgumentNullException(nameof(rowData));

        if (Columns == null || Columns.Count == 0)
            throw new InvalidOperationException("VirtualDataTable.Columns is not set.");

        // Convert the provided data to a dictionary for easier validation.
        var data = rowData.ToDictionary(x => x.Key, x => x.Value);

        if (data.Count != Columns.Count)
            throw new InvalidOperationException("Provided row data does not have the same number of columns as VirtualDataTable.");

        // Validate that every column required by the VirtualDataTable is present and the value is of the correct type.
        foreach (DataColumn col in Columns)
        {
            if (!data.ContainsKey(col.ColumnName))
                throw new InvalidOperationException($"Provided row data is missing required column '{col.ColumnName}'.");

            object value = data[col.ColumnName];
            if (value != null && value != DBNull.Value)
            {
                if (!col.DataType.IsAssignableFrom(value.GetType()))
                    throw new InvalidOperationException(
                        $"Data type mismatch for column '{col.ColumnName}'. Expected {col.DataType}, but got {value.GetType()}.");
            }
        }

        // Create a new DataRow from the validated dictionary and append it.
        DataRow newRow = CreateNewRowFromData(data);
        AppendNewRow(newRow);
    }

    /// <summary>
    /// Creates a new DataRow using the VirtualDataTable's underlying schema from Columns.
    /// It assumes that the provided dictionary has been validated.
    /// </summary>
    private DataRow CreateNewRowFromData(IDictionary<string, object> data)
    {
        // Retrieve the underlying DataTable from one of the DataColumns.
        DataTable schemaTable = Columns[0].Table;
        if (schemaTable == null)
            throw new InvalidOperationException("The DataColumn in VirtualDataTable.Columns is not attached to any DataTable.");

        DataRow newRow = schemaTable.NewRow();

        // Use the VirtualDataTable's column order to assign values.
        foreach (DataColumn col in Columns)
        {
            newRow[col] = data[col.ColumnName];
        }

        return newRow;
    }

    /// <summary>
    /// Lazily appends the provided new DataRow to the Rows enumerable.
    /// </summary>
    private void AppendNewRow(DataRow newRow)
    {
        if (Rows == null)
        {
            Rows = Enumerable.Repeat(newRow, 1);
        }
        else
        {
            Rows = Rows.Concat(new[] { newRow });
        }
    }

    private void SetSchema(DataTable table)
    {
        if (Columns != null)
        {
            foreach (DataColumn col in Columns)
            {
                // Create a new DataColumn with the same name and data type.
                DataColumn newCol = new DataColumn(col.ColumnName, col.DataType)
                {
                    Caption = col.Caption,
                    DefaultValue = col.DefaultValue,
                    ReadOnly = col.ReadOnly,
                    Unique = col.Unique
                };

                table.Columns.Add(newCol);
            }
        }
    }
}

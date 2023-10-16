namespace SqlBuildingBlocks.LogicalEntities;

public class SqlTable : IEquatable<SqlTable>
{

    public SqlTable(string? databaseName, string tableName)
    {
        DatabaseName = databaseName;
        TableName = !string.IsNullOrEmpty(tableName) ? tableName : throw new ArgumentNullException(tableName);
    }

    public string? DatabaseName { get; set; }
    public string TableName { get; }
    public string? TableAlias { get; set; }

    public override bool Equals(object? obj) => Equals(obj as SqlTable);

    public bool Equals(SqlTable? t)
    {
        if (t is null)
        {
            return false;
        }

        // Optimization for a common success case.
        if (ReferenceEquals(this, t))
        {
            return true;
        }

        // If run-time types are not exactly the same, return false.
        if (GetType() != t.GetType())
        {
            return false;
        }

        // Return true if the fields match.
        // Note that the base class is not invoked because it is
        // System.Object, which defines Equals as reference equality.
        return (string.Compare(DatabaseName, t.DatabaseName, true) == 0) && (string.Compare(TableName, t.TableName, true) == 0);
    }

    public override int GetHashCode() => (DatabaseName == null ? null : DatabaseName.ToLower(), TableName.ToLower()).GetHashCode();

    public static bool operator ==(SqlTable lhs, SqlTable rhs)
    {
        if (lhs is null)
        {
            if (rhs is null)
            {
                return true;
            }

            // Only the left side is null.
            return false;
        }
        // Equals handles case of null on right side.
        return lhs.Equals(rhs);
    }

    public static bool operator !=(SqlTable lhs, SqlTable rhs) => !(lhs == rhs);

    public override string ToString()
    {
        var result = TableName;
        if (!string.IsNullOrEmpty(DatabaseName))
            result = $"{DatabaseName}.{result}";
        if (!string.IsNullOrEmpty(TableAlias))
            result = $"{result} AS {TableAlias}";
        return result;
    }
}

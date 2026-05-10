using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.CrossCutting.Tests;

/// <summary>
/// Cross-dialect parity tests. The same SQL is parsed under multiple dialect grammars
/// and the resulting SQL AST shapes must be equivalent.
///
/// Scope: only assertions are made for the dialects that actually claim support for the
/// SQL form. PostgreSQL has no PostgreSQL-specific SELECT subclass; it inherits the base
/// <see cref="SqlBuildingBlocks.SelectStmt"/> directly. SQLServer has its own SELECT
/// subclass; AnsiSQL and MySQL have their own SELECT subclasses too.
///
/// For INSERT/UPDATE/DELETE we deliberately use the base Core statement types because
/// the dialect-specific overrides (OUTPUT/RETURNING/ON CONFLICT/ON DUPLICATE KEY) are
/// covered in their own dialect-specific test projects. Cross-dialect parity here is
/// about plain ANSI-shaped SQL.
/// </summary>
public class CrossDialectParityTests
{
    // ── SELECT grammars (dialect-specific SelectStmt) ─────────────────────────

    private class AnsiSqlSelectGrammar : Grammar
    {
        public AnsiSqlSelectGrammar() : base(false)
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            AnsiSQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = selectStmt;
        }

        public SqlSelectDefinition Create(ParseTreeNode node) =>
            ((AnsiSQL.SelectStmt)Root).Create(node);
    }

    private class MySqlSelectGrammar : Grammar
    {
        public MySqlSelectGrammar() : base(false)
        {
            MySQL.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            MySQL.Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            MySQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);
            expr.AddIntervalSupport(this);

            Root = selectStmt;
        }

        public SqlSelectDefinition Create(ParseTreeNode node) =>
            ((MySQL.SelectStmt)Root).Create(node);
    }

    private class PostgreSqlSelectGrammar : Grammar
    {
        // PostgreSQL does not currently have a PostgreSQL-specific SelectStmt subclass
        // in src/Grammars/PostgreSQL. The PostgreSQL grammar reuses the base Core
        // SelectStmt directly. This class wires that up for the parity comparison.
        public PostgreSqlSelectGrammar() : base(false)
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = selectStmt;
        }

        public SqlSelectDefinition Create(ParseTreeNode node) =>
            ((SelectStmt)Root).Create(node);
    }

    private class SqlServerSelectGrammar : Grammar
    {
        public SqlServerSelectGrammar() : base(false)
        {
            SQLServer.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            SQLServer.TableHintOpt tableHintOpt = new(this);
            SQLServer.TableName tableName = new(this, aliasOpt, id, tableHintOpt);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SQLServer.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = selectStmt;
        }

        public SqlSelectDefinition Create(ParseTreeNode node) =>
            ((SQLServer.SelectStmt)Root).Create(node);
    }

    private static SqlSelectDefinition ParseSelect(Grammar grammar, string sql)
    {
        var node = GrammarParser.Parse(grammar, sql);
        return grammar switch
        {
            AnsiSqlSelectGrammar g => g.Create(node),
            MySqlSelectGrammar g => g.Create(node),
            PostgreSqlSelectGrammar g => g.Create(node),
            SqlServerSelectGrammar g => g.Create(node),
            _ => throw new InvalidOperationException("Unknown grammar")
        };
    }

    public static IEnumerable<object[]> SelectGrammars => new[]
    {
        new object[] { typeof(AnsiSqlSelectGrammar) },
        new object[] { typeof(MySqlSelectGrammar) },
        new object[] { typeof(PostgreSqlSelectGrammar) },
        new object[] { typeof(SqlServerSelectGrammar) },
    };

    private static Grammar BuildSelectGrammar(Type grammarType) => (Grammar)Activator.CreateInstance(grammarType)!;

    [Theory]
    [MemberData(nameof(SelectGrammars))]
    public void Select_Star_FromTableWithWhere_ProducesEquivalentAst(Type grammarType)
    {
        // Note: avoid one-letter table names like "t" or "f" — these collide with
        // reserved boolean-literal short forms ("T"/"F"/"yes"/"no") in the grammar.
        const string sql = "SELECT * FROM tbl WHERE x > 1";
        var grammar = BuildSelectGrammar(grammarType);
        var selectStmt = ParseSelect(grammar, sql);

        Assert.Single(selectStmt.Columns);
        Assert.IsType<SqlAllColumns>(selectStmt.Columns[0]);
        Assert.Equal("tbl", selectStmt.Table!.TableName);
        Assert.Empty(selectStmt.Joins);
        Assert.NotNull(selectStmt.WhereClause);
        Assert.Equal("x", selectStmt.WhereClause!.BinExpr!.Left.Column!.ColumnName);
        Assert.Equal(1, selectStmt.WhereClause.BinExpr.Right!.Value!.Int);
    }

    [Theory]
    [MemberData(nameof(SelectGrammars))]
    public void Select_NamedColumns_OrderByDesc_ProducesEquivalentAst(Type grammarType)
    {
        const string sql = "SELECT a, b FROM tbl ORDER BY a DESC";
        var grammar = BuildSelectGrammar(grammarType);
        var selectStmt = ParseSelect(grammar, sql);

        Assert.Equal(2, selectStmt.Columns.Count);
        Assert.Equal("a", ((SqlColumn)selectStmt.Columns[0]).ColumnName);
        Assert.Equal("b", ((SqlColumn)selectStmt.Columns[1]).ColumnName);
        Assert.Equal("tbl", selectStmt.Table!.TableName);
        Assert.Single(selectStmt.OrderBy);
        Assert.Equal("a", selectStmt.OrderBy[0].ColumnName);
        Assert.True(selectStmt.OrderBy[0].Descending);
    }

    [Theory]
    [MemberData(nameof(SelectGrammars))]
    public void Select_GroupByHaving_ProducesEquivalentAst(Type grammarType)
    {
        // HAVING uses a non-aggregate expression because the current grammar's
        // HAVING expr non-terminal does not accept aggregate functions on the LHS.
        // The aggregate-in-HAVING shape is a known grammar gap covered by other
        // dialect-specific testing — out of scope for cross-dialect parity.
        const string sql = "SELECT COUNT(*) FROM tbl GROUP BY x HAVING x > 5";
        var grammar = BuildSelectGrammar(grammarType);
        var selectStmt = ParseSelect(grammar, sql);

        Assert.Single(selectStmt.Columns);
        Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("tbl", selectStmt.Table!.TableName);
        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy!.Columns);
        Assert.Equal("x", selectStmt.GroupBy.Columns[0]);
        Assert.NotNull(selectStmt.HavingClause);
    }

    [Theory]
    [MemberData(nameof(SelectGrammars))]
    public void Select_InnerJoin_ProducesEquivalentAst(Type grammarType)
    {
        const string sql = "SELECT * FROM aa INNER JOIN bb ON aa.id = bb.id";
        var grammar = BuildSelectGrammar(grammarType);
        var selectStmt = ParseSelect(grammar, sql);

        Assert.Equal("aa", selectStmt.Table!.TableName);
        Assert.Single(selectStmt.Joins);
        var join = selectStmt.Joins[0];
        Assert.Equal(SqlJoinKind.Inner, join.JoinKind);
        Assert.Equal("bb", join.Table.TableName);
        Assert.Equal("aa", join.Condition.Left.Column!.TableName);
        Assert.Equal("id", join.Condition.Left.Column.ColumnName);
        Assert.Equal("bb", join.Condition.Right!.Column!.TableName);
        Assert.Equal("id", join.Condition.Right.Column.ColumnName);
    }

    public static IEnumerable<object[]> RangeIntervalSupportingSelectGrammars => new[]
    {
        new object[] { typeof(AnsiSqlSelectGrammar) },
        new object[] { typeof(MySqlSelectGrammar) },
        new object[] { typeof(PostgreSqlSelectGrammar) },
    };

    [Theory]
    [MemberData(nameof(RangeIntervalSupportingSelectGrammars))]
    public void Select_WindowFrame_RangeIntervalDayPreceding_ProducesEquivalentAst(Type grammarType)
    {
        // Issue #180: AnsiSQL/MySQL/PostgreSQL all accept SQL:2003 INTERVAL window-frame bounds.
        // The same SQL must produce the same logical entity shape across dialects.
        // SQL Server is excluded — it rejects INTERVAL bounds (covered by a separate test).
        const string sql =
            "SELECT EventDate, " +
            "SUM(Amount) OVER (ORDER BY EventDate RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW) AS rolling " +
            "FROM Events";
        var grammar = BuildSelectGrammar(grammarType);
        var selectStmt = ParseSelect(grammar, sql);

        var aggregate = Assert.IsType<SqlAggregate>(selectStmt.Columns[1]);
        var frame = aggregate.WindowSpecification!.Frame!;
        Assert.Equal(WindowFrameMode.Range, frame.Mode);
        Assert.Equal(WindowFrameBoundType.Preceding, frame.Start.Type);
        Assert.Equal(SqlWindowFrameBoundOffsetKind.Interval, frame.Start.Offset!.Kind);
        Assert.Equal(1L, frame.Start.Offset.Interval!.Magnitude);
        Assert.Equal(IntervalQualifier.Day, frame.Start.Offset.Interval.Qualifier);
        Assert.Equal(WindowFrameBoundType.CurrentRow, frame.End!.Type);
    }

    [Fact]
    public void Select_WindowFrame_RangeIntervalDayPreceding_SqlServer_Rejects()
    {
        // Issue #180: SQL Server's grammar accepts the input syntactically (inherited from
        // core), but the SQL Server-specific SelectStmt.CreateWindowFrameBound override
        // surfaces a clear NotSupportedException during AST construction. This is the
        // "clean parse error" promised by the dialect contract.
        const string sql =
            "SELECT EventDate, " +
            "SUM(Amount) OVER (ORDER BY EventDate RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW) AS rolling " +
            "FROM Events";
        var grammar = new SqlServerSelectGrammar();

        var ex = Assert.Throws<NotSupportedException>(() => ParseSelect(grammar, sql));
        Assert.Contains("INTERVAL", ex.Message);
        Assert.Contains("SQL Server", ex.Message);
    }

    // ── INSERT / UPDATE / DELETE grammars (base Core statement types) ─────────
    //
    // For DML parity we use each dialect's SimpleId (so dialect-specific quoting
    // seams are exercised) and the base Core InsertStmt/UpdateStmt/DeleteStmt.
    // Dialect-specific extensions (OUTPUT, RETURNING, ON CONFLICT, ON DUPLICATE
    // KEY) are intentionally out of scope here — those live in their own dialect
    // test projects. Cross-dialect parity here is about plain ANSI-shaped SQL.

    private class AnsiSqlDml : Grammar
    {
        public AnsiSqlDml() : base(false)
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            ReturningClauseOpt returningClauseOpt = new(this, id);
            SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            expr.InitializeRule(selectStmt, funcCall);

            Insert = new InsertStmt(this, id, expr, selectStmt);
            Update = new UpdateStmt(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);
            Delete = new DeleteStmt(this, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt);

            Stmt stmt = new(this, Insert, Update, Delete);
            Root = stmt;
        }

        public InsertStmt Insert { get; }
        public UpdateStmt Update { get; }
        public DeleteStmt Delete { get; }
    }

    private class MySqlDml : Grammar
    {
        public MySqlDml() : base(false)
        {
            MySQL.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            ReturningClauseOpt returningClauseOpt = new(this, id);
            SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            expr.InitializeRule(selectStmt, funcCall);

            Insert = new InsertStmt(this, id, expr, selectStmt);
            Update = new UpdateStmt(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);
            Delete = new DeleteStmt(this, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt);

            Stmt stmt = new(this, Insert, Update, Delete);
            Root = stmt;
        }

        public InsertStmt Insert { get; }
        public UpdateStmt Update { get; }
        public DeleteStmt Delete { get; }
    }

    private class PostgreSqlDml : Grammar
    {
        public PostgreSqlDml() : base(false)
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            ReturningClauseOpt returningClauseOpt = new(this, id);
            SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            expr.InitializeRule(selectStmt, funcCall);

            Insert = new InsertStmt(this, id, expr, selectStmt);
            Update = new UpdateStmt(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);
            Delete = new DeleteStmt(this, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt);

            Stmt stmt = new(this, Insert, Update, Delete);
            Root = stmt;
        }

        public InsertStmt Insert { get; }
        public UpdateStmt Update { get; }
        public DeleteStmt Delete { get; }
    }

    private class SqlServerDml : Grammar
    {
        public SqlServerDml() : base(false)
        {
            SQLServer.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            ReturningClauseOpt returningClauseOpt = new(this, id);
            SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            expr.InitializeRule(selectStmt, funcCall);

            Insert = new InsertStmt(this, id, expr, selectStmt);
            Update = new UpdateStmt(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);
            Delete = new DeleteStmt(this, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt);

            Stmt stmt = new(this, Insert, Update, Delete);
            Root = stmt;
        }

        public InsertStmt Insert { get; }
        public UpdateStmt Update { get; }
        public DeleteStmt Delete { get; }
    }

    public static IEnumerable<object[]> DmlGrammars => new[]
    {
        new object[] { typeof(AnsiSqlDml) },
        new object[] { typeof(MySqlDml) },
        new object[] { typeof(PostgreSqlDml) },
        new object[] { typeof(SqlServerDml) },
    };

    private static (InsertStmt insert, UpdateStmt update, DeleteStmt delete, Grammar grammar) BuildDml(Type type)
    {
        var grammar = (Grammar)Activator.CreateInstance(type)!;
        return type switch
        {
            _ when grammar is AnsiSqlDml a => (a.Insert, a.Update, a.Delete, a),
            _ when grammar is MySqlDml m => (m.Insert, m.Update, m.Delete, m),
            _ when grammar is PostgreSqlDml p => (p.Insert, p.Update, p.Delete, p),
            _ when grammar is SqlServerDml s => (s.Insert, s.Update, s.Delete, s),
            _ => throw new InvalidOperationException("Unknown DML grammar")
        };
    }

    [Theory]
    [MemberData(nameof(DmlGrammars))]
    public void Insert_BasicValues_ProducesEquivalentAst(Type grammarType)
    {
        const string sql = "INSERT INTO tbl (a, b) VALUES (1, 2)";
        var (insert, _, _, grammar) = BuildDml(grammarType);
        var node = GrammarParser.Parse(grammar, sql);
        var stmt = insert.Create(node);

        Assert.Equal("tbl", stmt.Table!.TableName);
        Assert.Equal(2, stmt.Columns.Count);
        Assert.Equal("a", stmt.Columns[0].ColumnName);
        Assert.Equal("b", stmt.Columns[1].ColumnName);
        Assert.NotNull(stmt.Values);
        Assert.Single(stmt.Values!);
        Assert.Equal(1, stmt.Values![0][0].Value!.Int);
        Assert.Equal(2, stmt.Values[0][1].Value!.Int);
    }

    [Theory]
    [MemberData(nameof(DmlGrammars))]
    public void Update_SetWithWhere_ProducesEquivalentAst(Type grammarType)
    {
        const string sql = "UPDATE tbl SET a = 1 WHERE b = 2";
        var (_, update, _, grammar) = BuildDml(grammarType);
        var node = GrammarParser.Parse(grammar, sql);
        var stmt = update.Create(node);

        Assert.Equal("tbl", stmt.Table!.TableName);
        Assert.Single(stmt.Assignments);
        Assert.Equal("a", stmt.Assignments[0].Column.ColumnName);
        Assert.Equal(1, stmt.Assignments[0].Value!.Int);

        Assert.NotNull(stmt.WhereClause);
        Assert.Equal("b", stmt.WhereClause!.BinExpr!.Left.Column!.ColumnName);
        Assert.Equal(2, stmt.WhereClause.BinExpr.Right!.Value!.Int);
    }

    [Theory]
    [MemberData(nameof(DmlGrammars))]
    public void Delete_WithWhere_ProducesEquivalentAst(Type grammarType)
    {
        const string sql = "DELETE FROM tbl WHERE x = 1";
        var (_, _, delete, grammar) = BuildDml(grammarType);
        var node = GrammarParser.Parse(grammar, sql);
        var stmt = delete.Create(node);

        Assert.Equal("tbl", stmt.Table!.TableName);
        Assert.NotNull(stmt.WhereClause);
        Assert.Equal("x", stmt.WhereClause!.BinExpr!.Left.Column!.ColumnName);
        Assert.Equal(1, stmt.WhereClause.BinExpr.Right!.Value!.Int);
    }
}

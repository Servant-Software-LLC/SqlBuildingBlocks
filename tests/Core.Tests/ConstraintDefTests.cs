using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class ConstraintDefTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);

            ConstraintDef constraintDef = new(this, id);

            Root = constraintDef;

            // Match punctuation stripping that CreateTableStmt applies when used in production
            MarkPunctuation("(", ")");
        }

        public virtual (SqlConstraintDefinition? Constraint, bool Handled) Create(ParseTreeNode createTableStmt, IList<SqlColumnDefinition> columns) =>
            ((ConstraintDef)Root).Create(createTableStmt, columns);
    }

    [Fact]
    public void ForeignKey_BasicNoActions()
    {
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_PersonOrder FOREIGN KEY (PersonID) REFERENCES Persons(PersonID)";
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node, new List<SqlColumnDefinition>());

        Assert.NotNull(result.Constraint);
        Assert.True(result.Handled);
        Assert.Equal("FK_PersonOrder", result.Constraint.Name);

        var fk = result.Constraint.ForeignKeyConstraint;
        Assert.NotNull(fk);
        Assert.Equal("Persons", fk.ParentTable.TableName);
        Assert.Single(fk.ColumnReferences);
        Assert.Equal("PersonID", fk.ColumnReferences[0].Column);
        Assert.Equal("PersonID", fk.ColumnReferences[0].ParentColumn);
        Assert.Null(fk.OnDeleteAction);
        Assert.Null(fk.OnUpdateAction);
    }

    [Fact]
    public void ForeignKey_CompositeKey()
    {
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_Composite FOREIGN KEY (OrderID, ItemID) REFERENCES OrderItems(OrderID, ItemID)";
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node, new List<SqlColumnDefinition>());

        Assert.NotNull(result.Constraint);
        Assert.True(result.Handled);
        var fk = result.Constraint!.ForeignKeyConstraint;
        Assert.NotNull(fk);
        Assert.Equal("OrderItems", fk.ParentTable.TableName);
        Assert.Equal(2, fk.ColumnReferences.Count);
        Assert.Equal("OrderID", fk.ColumnReferences[0].Column);
        Assert.Equal("OrderID", fk.ColumnReferences[0].ParentColumn);
        Assert.Equal("ItemID", fk.ColumnReferences[1].Column);
        Assert.Equal("ItemID", fk.ColumnReferences[1].ParentColumn);
    }

    [Fact]
    public void ForeignKey_OnDeleteCascade()
    {
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_Child FOREIGN KEY (ParentID) REFERENCES Parent(ID) ON DELETE CASCADE";
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node, new List<SqlColumnDefinition>());

        var fk = result.Constraint!.ForeignKeyConstraint;
        Assert.NotNull(fk);
        Assert.Equal(ForeignKeyReferentialAction.Cascade, fk.OnDeleteAction);
        Assert.Null(fk.OnUpdateAction);
    }

    [Fact]
    public void ForeignKey_OnUpdateSetNull()
    {
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_Child FOREIGN KEY (ParentID) REFERENCES Parent(ID) ON UPDATE SET NULL";
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node, new List<SqlColumnDefinition>());

        var fk = result.Constraint!.ForeignKeyConstraint;
        Assert.NotNull(fk);
        Assert.Null(fk.OnDeleteAction);
        Assert.Equal(ForeignKeyReferentialAction.SetNull, fk.OnUpdateAction);
    }

    [Fact]
    public void ForeignKey_BothDeleteAndUpdate()
    {
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_Child FOREIGN KEY (ParentID) REFERENCES Parent(ID) ON DELETE CASCADE ON UPDATE RESTRICT";
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node, new List<SqlColumnDefinition>());

        var fk = result.Constraint!.ForeignKeyConstraint;
        Assert.NotNull(fk);
        Assert.Equal(ForeignKeyReferentialAction.Cascade, fk.OnDeleteAction);
        Assert.Equal(ForeignKeyReferentialAction.Restrict, fk.OnUpdateAction);
    }

    [Fact]
    public void ForeignKey_OnDeleteNoAction()
    {
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_Child FOREIGN KEY (ParentID) REFERENCES Parent(ID) ON DELETE NO ACTION";
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node, new List<SqlColumnDefinition>());

        var fk = result.Constraint!.ForeignKeyConstraint;
        Assert.NotNull(fk);
        Assert.Equal(ForeignKeyReferentialAction.NoAction, fk.OnDeleteAction);
    }

    [Fact]
    public void ForeignKey_OnDeleteSetDefault()
    {
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_Child FOREIGN KEY (ParentID) REFERENCES Parent(ID) ON DELETE SET DEFAULT";
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node, new List<SqlColumnDefinition>());

        var fk = result.Constraint!.ForeignKeyConstraint;
        Assert.NotNull(fk);
        Assert.Equal(ForeignKeyReferentialAction.SetDefault, fk.OnDeleteAction);
    }

    [Fact]
    public void ForeignKey_QualifiedReferencedTable()
    {
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_Child FOREIGN KEY (ParentID) REFERENCES dbo.Parent(ID)";
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node, new List<SqlColumnDefinition>());

        var fk = result.Constraint!.ForeignKeyConstraint;
        Assert.NotNull(fk);
        Assert.Equal("Parent", fk.ParentTable.TableName);
        Assert.Equal("dbo", fk.ParentTable.DatabaseName);
    }
}

using SqlBuildingBlocks.POCOs;
using System.Data;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.POCOs;

public class VirtualDataTableConcurrencyTests
{
    [Fact]
    public void ConcurrentColumnReads_DoNotThrow()
    {
        // Arrange: VirtualDataTable with schema
        var vdt = CreateVirtualDataTable();
        var barrier = new Barrier(participantCount: 10);
        var exceptions = new List<Exception>();

        // Act: 10 threads read Columns concurrently
        var threads = Enumerable.Range(0, 10).Select(_ => new Thread(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (int i = 0; i < 100; i++)
                {
                    var cols = vdt.Columns;
                    var count = cols.Count;
                    foreach (DataColumn col in cols)
                    {
                        var name = col.ColumnName;
                    }
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        })).ToList();

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // Assert
        Assert.Empty(exceptions);
    }

    [Fact]
    public void ConcurrentAdoptAndColumnRead_DoNotThrow()
    {
        // Arrange: VirtualDataTable that will have its schema swapped while being read
        var vdt = CreateVirtualDataTable();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var exceptions = new List<Exception>();

        // Readers
        var readers = Enumerable.Range(0, 5).Select(_ => new Thread(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var cols = vdt.Columns;
                    var count = cols.Count;
                    var name = vdt.TableName;
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        })).ToList();

        // Writers: swap schemas
        var writers = Enumerable.Range(0, 3).Select(i => new Thread(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var dt = new DataTable($"Table_{i}_{Thread.CurrentThread.ManagedThreadId}");
                    dt.Columns.Add("Id", typeof(int));
                    dt.Columns.Add("Name", typeof(string));
                    vdt.AdoptDataTable(dt);
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        })).ToList();

        // Act
        foreach (var t in readers.Concat(writers)) t.Start();
        cts.Token.WaitHandle.WaitOne();
        foreach (var t in readers.Concat(writers)) t.Join(TimeSpan.FromSeconds(5));

        // Assert: no exceptions from concurrent access
        Assert.Empty(exceptions);
    }

    [Fact]
    public void ConcurrentNewRowAndAdopt_DoNotThrow()
    {
        var vdt = CreateVirtualDataTable();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var exceptions = new List<Exception>();

        var newRowThreads = Enumerable.Range(0, 5).Select(_ => new Thread(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var row = vdt.NewRow();
                    row["Id"] = 1;
                    row["Name"] = "Test";
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        })).ToList();

        var adoptThread = new Thread(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var dt = new DataTable("Swapped");
                    dt.Columns.Add("Id", typeof(int));
                    dt.Columns.Add("Name", typeof(string));
                    vdt.AdoptDataTable(dt);
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        });

        foreach (var t in newRowThreads) t.Start();
        adoptThread.Start();
        cts.Token.WaitHandle.WaitOne();
        foreach (var t in newRowThreads) t.Join(TimeSpan.FromSeconds(5));
        adoptThread.Join(TimeSpan.FromSeconds(5));

        Assert.Empty(exceptions);
    }

    [Fact]
    public void ConcurrentCreateNewRowFromData_DoNotThrow()
    {
        var vdt = CreateVirtualDataTable();
        var barrier = new Barrier(participantCount: 8);
        var exceptions = new List<Exception>();

        var threads = Enumerable.Range(0, 8).Select(i => new Thread(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (int j = 0; j < 50; j++)
                {
                    var data = new Dictionary<string, object>
                    {
                        ["Id"] = i * 50 + j,
                        ["Name"] = $"Thread{i}_Row{j}"
                    };
                    var row = vdt.CreateNewRowFromData(data);
                    Assert.Equal(i * 50 + j, row["Id"]);
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        })).ToList();

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.Empty(exceptions);
    }

    [Fact]
    public void ConcurrentToDataTable_DoNotThrow()
    {
        var vdt = CreateVirtualDataTable();
        // Add some rows
        var dt = new DataTable("Test");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Rows.Add(1, "Alice");
        dt.Rows.Add(2, "Bob");
        vdt.AdoptDataTable(dt);

        var barrier = new Barrier(participantCount: 8);
        var exceptions = new List<Exception>();

        var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (int j = 0; j < 20; j++)
                {
                    var result = vdt.ToDataTable();
                    Assert.True(result.Columns.Count >= 2);
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        })).ToList();

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.Empty(exceptions);
    }

    private VirtualDataTable CreateVirtualDataTable()
    {
        DataTable dt = new DataTable("TestTable");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        return new VirtualDataTable(dt);
    }
}

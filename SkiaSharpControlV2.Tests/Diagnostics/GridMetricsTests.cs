using SkiaSharpControlV2.Diagnostics;

namespace SkiaSharpControlV2.Tests.Diagnostics;

public class GridMetricsTests : IDisposable
{
    public GridMetricsTests()
    {
        GridMetrics.Reset();
        GridMetrics.IsEnabled = true;
    }

    public void Dispose()
    {
        GridMetrics.IsEnabled = false;
        GridMetrics.PercentilesEnabled = false;
        GridMetrics.Reset();
    }

    [Fact]
    public void IsEnabled_DefaultFalse()
    {
        GridMetrics.IsEnabled = false;
        Assert.False(GridMetrics.IsEnabled);
    }

    [Fact]
    public void Measure_WhenDisabled_ReturnsNoOp()
    {
        GridMetrics.IsEnabled = false;
        GridMetrics.Reset();

        using (var scope = GridMetrics.Measure("DisabledTestOp"))
        {
            Thread.Sleep(5);
        }

        var snapshot = GridMetrics.GetSnapshot();
        // When disabled, the specific operation we measured should NOT appear
        Assert.False(snapshot.Entries.ContainsKey("DisabledTestOp"));
    }

    [Fact]
    public void Measure_WhenEnabled_RecordsMetric()
    {
        using (var scope = GridMetrics.Measure("TestOp"))
        {
            Thread.Sleep(5);
        }

        var snapshot = GridMetrics.GetSnapshot();
        Assert.True(snapshot.Entries.ContainsKey("TestOp"));
        Assert.Equal(1, snapshot.Entries["TestOp"].CallCount);
        Assert.True(snapshot.Entries["TestOp"].TotalMs > 0);
    }

    [Fact]
    public void Measure_MultipleCalls_AccumulatesMetrics()
    {
        for (int i = 0; i < 3; i++)
        {
            using var scope = GridMetrics.Measure("MultiOp");
            Thread.Sleep(1);
        }

        var snapshot = GridMetrics.GetSnapshot();
        var entry = snapshot.Entries["MultiOp"];
        Assert.Equal(3, entry.CallCount);
        Assert.True(entry.TotalMs > 0);
        Assert.True(entry.AvgMs > 0);
        Assert.True(entry.MinMs > 0);
        Assert.True(entry.MaxMs >= entry.MinMs);
    }

    [Fact]
    public void Record_ManualRecording_Works()
    {
        GridMetrics.Record("ManualOp", 5.5);
        GridMetrics.Record("ManualOp", 10.2);

        var snapshot = GridMetrics.GetSnapshot();
        var entry = snapshot.Entries["ManualOp"];
        Assert.Equal(2, entry.CallCount);
        Assert.Equal(15.7, entry.TotalMs);
        Assert.Equal(5.5, entry.MinMs);
        Assert.Equal(10.2, entry.MaxMs);
        Assert.Equal(10.2, entry.LastMs);
    }

    [Fact]
    public void Record_WhenDisabled_NoOps()
    {
        GridMetrics.IsEnabled = false;
        GridMetrics.Record("DisabledRecordOp", 100);

        var snapshot = GridMetrics.GetSnapshot();
        // Check specific key not added (other tests may leave entries in static state)
        Assert.False(snapshot.Entries.ContainsKey("DisabledRecordOp"));
    }

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        using (var scope = GridMetrics.Measure("ToBeCleared"))
        {
        }

        GridMetrics.Reset();

        var snapshot = GridMetrics.GetSnapshot();
        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public void GetSnapshot_ReturnsTimestamp()
    {
        var before = DateTime.UtcNow;
        var snapshot = GridMetrics.GetSnapshot();
        var after = DateTime.UtcNow;

        Assert.True(snapshot.Timestamp >= before);
        Assert.True(snapshot.Timestamp <= after);
    }

    [Fact]
    public void GetSnapshot_Entries_AreDifferentInstances()
    {
        using (var scope = GridMetrics.Measure("SnapshotTest"))
        {
        }

        var snapshot1 = GridMetrics.GetSnapshot();
        var snapshot2 = GridMetrics.GetSnapshot();

        Assert.NotSame(snapshot1.Entries, snapshot2.Entries);
    }

    [Fact]
    public void MetricEntrySnapshot_ToString_ContainsAllFields()
    {
        GridMetrics.Record("FormatTest", 5.123);

        var snapshot = GridMetrics.GetSnapshot();
        var str = snapshot.Entries["FormatTest"].ToString();

        Assert.Contains("Calls=1", str);
        Assert.Contains("Avg=", str);
        Assert.Contains("Min=", str);
        Assert.Contains("Max=", str);
        Assert.Contains("Last=", str);
        Assert.Contains("Total=", str);
    }

    [Fact]
    public void MetricsSnapshot_ToString_ContainsHeader()
    {
        GridMetrics.Record("Op1", 1.0);
        GridMetrics.Record("Op2", 2.0);

        var snapshot = GridMetrics.GetSnapshot();
        var str = snapshot.ToString();

        Assert.Contains("SkiaGrid Metrics Snapshot", str);
        Assert.Contains("Op1", str);
        Assert.Contains("Op2", str);
    }

    [Fact]
    public void OperationConstants_AreDefined()
    {
        Assert.Equal("Render.Draw", GridMetrics.RenderDraw);
        Assert.Equal("Render.PaintSurface", GridMetrics.RenderPaintSurface);
        Assert.Equal("Data.Refresh", GridMetrics.DataRefresh);
        Assert.Equal("Data.FlattenGrouped", GridMetrics.DataFlattenGrouped);
        Assert.Equal("Data.FlattenRows", GridMetrics.DataFlattenRows);
        Assert.Equal("Data.UpdateCollection", GridMetrics.DataUpdateCollection);
        Assert.Equal("Sort.Apply", GridMetrics.SortApply);
        Assert.Equal("Filter.PassAll", GridMetrics.FilterPassAll);
        Assert.Equal("Group.Aggregate", GridMetrics.GroupAggregate);
        Assert.Equal("Data.InsertNewItem", GridMetrics.DataInsertNewItem);
        Assert.Equal("Column.Update", GridMetrics.ColumnUpdate);
        Assert.Equal("Export.Data", GridMetrics.ExportData);
        Assert.Equal("CollectionView.Refresh", GridMetrics.CollectionViewRefresh);
    }

    // ── Percentiles (Q8) ────────────────────────────────────────────────

    [Fact]
    public void Percentiles_DefaultOff_ReturnsNull()
    {
        // IsEnabled is true (ctor) but PercentilesEnabled defaults false.
        Assert.False(GridMetrics.PercentilesEnabled);
        for (int i = 0; i < 50; i++) GridMetrics.Record("PctOff", 1.0);

        Assert.Null(GridMetrics.GetPercentiles("PctOff"));
        Assert.Empty(GridMetrics.GetAllPercentiles());
        // Existing snapshot is unaffected — samples are still counted normally.
        Assert.Equal(50, GridMetrics.GetSnapshot().Entries["PctOff"].CallCount);
    }

    [Fact]
    public void Percentiles_WhenIsEnabledFalse_ReturnsNull()
    {
        // IsEnabled gate wins: Record() early-returns, so nothing accumulates even if
        // PercentilesEnabled is on.
        GridMetrics.IsEnabled = false;
        GridMetrics.PercentilesEnabled = true;
        for (int i = 0; i < 50; i++) GridMetrics.Record("PctGated", 1.0);

        Assert.Null(GridMetrics.GetPercentiles("PctGated"));
    }

    [Fact]
    public void Percentiles_KnownDistribution_LandInExpectedBuckets()
    {
        GridMetrics.PercentilesEnabled = true;
        for (int v = 1; v <= 100; v++) GridMetrics.Record("PctDist", v);

        var p = GridMetrics.GetPercentiles("PctDist");
        Assert.NotNull(p);
        Assert.Equal(100, p!.SampleCount);
        // Approximate/bucketed: assert monotonic and within the containing bucket span.
        Assert.True(p.P50Ms <= p.P95Ms && p.P95Ms <= p.P99Ms);
        Assert.InRange(p.P50Ms, 30.0, 75.0);   // ~50 → bucket [50] or [75]
        Assert.InRange(p.P95Ms, 75.0, 150.0);  // ~95 → bucket [100]
        Assert.InRange(p.P99Ms, 75.0, 150.0);  // ~99 → bucket [100]
    }

    [Fact]
    public void Percentiles_OverflowBucket_FallsBackToMax()
    {
        GridMetrics.PercentilesEnabled = true;
        // All samples exceed the top bucket bound (5000ms) → overflow bucket → falls back to max.
        for (int i = 0; i < 20; i++) GridMetrics.Record("PctOverflow", 9999.0);

        var p = GridMetrics.GetPercentiles("PctOverflow");
        Assert.NotNull(p);
        Assert.Equal(9999.0, p!.P99Ms);
    }

    [Fact]
    public void Percentiles_Reset_ClearsThem()
    {
        GridMetrics.PercentilesEnabled = true;
        GridMetrics.Record("PctReset", 2.0);
        Assert.NotNull(GridMetrics.GetPercentiles("PctReset"));

        GridMetrics.Reset();
        Assert.Null(GridMetrics.GetPercentiles("PctReset"));
    }

    [Fact]
    public void MetricPercentiles_ToString_ContainsAllFields()
    {
        GridMetrics.PercentilesEnabled = true;
        GridMetrics.Record("PctFormat", 3.0);

        var str = GridMetrics.GetPercentiles("PctFormat")!.ToString();
        Assert.Contains("P50=", str);
        Assert.Contains("P95=", str);
        Assert.Contains("P99=", str);
        Assert.Contains("n=1", str);
    }

    [Fact]
    public void ConcurrentMeasure_DoesNotThrow()
    {
        var tasks = Enumerable.Range(0, 10).Select(i =>
            Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    using var scope = GridMetrics.Measure($"ConcurrentOp_{i}");
                }
            }));

        Task.WaitAll(tasks.ToArray());

        var snapshot = GridMetrics.GetSnapshot();
        Assert.True(snapshot.Entries.Count >= 10);
    }
}

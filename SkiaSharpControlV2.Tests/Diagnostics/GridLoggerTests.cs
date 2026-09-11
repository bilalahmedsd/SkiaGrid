using SkiaSharpControlV2.Diagnostics;

namespace SkiaSharpControlV2.Tests.Diagnostics;

public class GridLoggerTests
{
    [Fact]
    public void Log_DoesNotThrow()
    {
        GridLogger.Log(GridLogger.Render, "Test message");
    }

    [Fact]
    public void Log_WithArgs_DoesNotThrow()
    {
        GridLogger.Log(GridLogger.Data, "Items count: {0}, Filters: {1}", 100, 3);
    }

    [Fact]
    public void LogWarning_DoesNotThrow()
    {
        GridLogger.LogWarning(GridLogger.Sort, "Sort warning message");
    }

    [Fact]
    public void LogError_WithoutException_DoesNotThrow()
    {
        GridLogger.LogError(GridLogger.Filter, "Filter error");
    }

    [Fact]
    public void LogError_WithException_DoesNotThrow()
    {
        var ex = new InvalidOperationException("test error");
        GridLogger.LogError(GridLogger.Lifecycle, "Disposal failed", ex);
    }

    [Fact]
    public void AllCategories_AreDefined()
    {
        Assert.Equal("Render", GridLogger.Render);
        Assert.Equal("Data", GridLogger.Data);
        Assert.Equal("Sort", GridLogger.Sort);
        Assert.Equal("Filter", GridLogger.Filter);
        Assert.Equal("Group", GridLogger.Group);
        Assert.Equal("Selection", GridLogger.Selection);
        Assert.Equal("Scroll", GridLogger.Scroll);
        Assert.Equal("Column", GridLogger.Column);
        Assert.Equal("Lifecycle", GridLogger.Lifecycle);
        Assert.Equal("Export", GridLogger.Export);
        Assert.Equal("Input", GridLogger.Input);
    }
}

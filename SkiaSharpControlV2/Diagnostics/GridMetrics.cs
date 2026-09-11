using System.Collections.Concurrent;
using System.Diagnostics;

namespace SkiaSharpControlV2.Diagnostics
{
    /// <summary>
    /// Performance metrics collector for SkiaGridViewV2.
    /// Always compiled (needed for production profiling), but no-ops when IsEnabled is false.
    /// Use <c>using var _ = GridMetrics.Measure("operation")</c> to time a scope.
    /// Call <see cref="GetSnapshot"/> to extract all collected metrics.
    /// </summary>
    public static class GridMetrics
    {
        // ── Rendering metrics (timed) ──────────────────────────────────
        public const string RenderDraw = "Render.Draw";
        public const string RenderPaintSurface = "Render.PaintSurface";

        // ── Data/CollectionView metrics (timed) ────────────────────────
        public const string DataFlattenGrouped = "Data.FlattenGrouped";
        public const string DataFlattenRows = "Data.FlattenRows";
        public const string DataUpdateCollection = "Data.UpdateCollection";
        public const string DataInsertNewItem = "Data.InsertNewItem";
        public const string CollectionViewRefresh = "CollectionView.Refresh";

        // ── Sort/Filter/Group metrics (timed) ──────────────────────────
        public const string SortApply = "Sort.Apply";
        public const string FilterApply = "Filter.Apply";
        public const string GroupAggregate = "Group.Aggregate";

        // ── Input metrics (timed) ──────────────────────────────────────
        public const string InputMouseClick = "Input.MouseClick";
        public const string InputMouseWheel = "Input.MouseWheel";
        public const string InputKeyDown = "Input.KeyDown";

        // ── Selection/Scroll metrics (timed) ───────────────────────────
        public const string SelectionUpdate = "Selection.Update";
        public const string ScrollUpdate = "Scroll.Update";

        // ── Misc (timed) ───────────────────────────────────────────────
        public const string ColumnUpdate = "Column.Update";
        public const string ExportData = "Export.Data";

        // ── Per-cell/per-frame operations use counters (not timers) ────
        // Stopwatch overhead in tight loops distorts measurements.
        // See CounterCellsDrawn, CounterTriggersEvaluated, CounterReflectionCalls, CounterFilterEvaluations below.
        [Obsolete("Use CounterCellsDrawn instead. Per-cell timing adds overhead to hot loops.")]
        public const string RenderCellDraw = "Render.CellDraw";
        [Obsolete("Use counters for per-frame operations.")]
        public const string RenderGroupRow = "Render.GroupRow";
        [Obsolete("Use counters for per-frame operations.")]
        public const string RenderButton = "Render.Button";
        [Obsolete("Use counters for per-frame operations.")]
        public const string RenderCheckbox = "Render.Checkbox";
        [Obsolete("Use CollectionViewRefresh instead.")]
        public const string DataRefresh = "Data.Refresh";
        [Obsolete("Use counters for per-property-change operations.")]
        public const string DataPropertyChanged = "Data.PropertyChanged";
        [Obsolete("Use CounterFilterEvaluations instead. Per-item timing adds overhead.")]
        public const string FilterPassAll = "Filter.PassAll";
        [Obsolete("Use CounterTriggersEvaluated instead. Per-cell timing adds overhead.")]
        public const string TriggerEvaluate = "Trigger.Evaluate";
        [Obsolete("Per-cell timing not instrumented.")]
        public const string SetterResolve = "Setter.Resolve";
        [Obsolete("Use CounterReflectionCalls instead. Per-cell timing adds overhead.")]
        public const string ReflectionGet = "Reflection.Get";

        private static readonly ConcurrentDictionary<string, MetricEntry> _metrics = new();
        private static readonly ConcurrentDictionary<string, CounterEntry> _counters = new();
        private static bool _isEnabled;
        private static bool _percentilesEnabled;

        /// <summary>
        /// Enable or disable metrics collection. When disabled, Measure() returns a no-op disposable.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// Opt-in percentile (p50/p95/p99) collection for timed metrics. Default false.
        /// Requires <see cref="IsEnabled"/> to also be true (percentiles are recorded from the
        /// same Record path that IsEnabled gates). When false, no histogram is allocated and no
        /// extra work is done — behavior is identical to having no percentile support at all.
        /// Values reported by <see cref="GetPercentiles"/> are APPROXIMATE — quantized to a fixed
        /// coarse histogram (bounded memory, ~232 bytes per metric key regardless of run length),
        /// not exact order statistics. Suitable for load-test profiling of a trading UI.
        /// </summary>
        public static bool PercentilesEnabled
        {
            get => _percentilesEnabled;
            set => _percentilesEnabled = value;
        }

        /// <summary>
        /// Start timing an operation. Use with <c>using</c> statement.
        /// Returns immediately with no-op if IsEnabled is false.
        /// </summary>
        public static IDisposable Measure(string operation)
        {
            if (!_isEnabled)
                return NoOpDisposable.Instance;

            return new MeasureScope(operation);
        }

        /// <summary>
        /// Record a metric manually (for cases where using-scope isn't suitable).
        /// </summary>
        public static void Record(string operation, double elapsedMs)
        {
            if (!_isEnabled) return;

            _metrics.AddOrUpdate(operation,
                _ => new MetricEntry(elapsedMs),
                (_, existing) => { existing.Record(elapsedMs); return existing; });
        }

        /// <summary>
        /// Get a snapshot of all collected metrics.
        /// </summary>
        public static MetricsSnapshot GetSnapshot()
        {
            var entries = new Dictionary<string, MetricEntrySnapshot>();
            foreach (var kvp in _metrics)
            {
                entries[kvp.Key] = kvp.Value.ToSnapshot();
            }
            var counters = GetAllCounters();
            return new MetricsSnapshot(entries, DateTime.UtcNow, counters);
        }

        /// <summary>
        /// Clear all collected metrics.
        /// </summary>
        public static void Reset()
        {
            _metrics.Clear();
            _counters.Clear();
        }

        // ── Counter support (non-timing metrics) ─────────────────────

        // Counter names
        public const string CounterCellsDrawn = "Counter.CellsDrawn";
        public const string CounterTriggersMatched = "Counter.TriggersMatched";
        public const string CounterTriggersEvaluated = "Counter.TriggersEvaluated";
        public const string CounterPaintCacheHits = "Counter.PaintCacheHits";
        public const string CounterPaintCacheMisses = "Counter.PaintCacheMisses";
        public const string CounterReflectionCalls = "Counter.ReflectionCalls";
        public const string CounterFilterEvaluations = "Counter.FilterEvaluations";
        public const string CounterCollectionChanges = "Counter.CollectionChanges";

        /// <summary>Increment a named counter. Useful for tracking how often something happens.</summary>
        public static void Increment(string counter, long delta = 1)
        {
            if (!_isEnabled) return;
            _counters.AddOrUpdate(counter,
                _ => new CounterEntry(delta),
                (_, existing) => { existing.Add(delta); return existing; });
        }

        /// <summary>Get the current value of a counter.</summary>
        public static long GetCounter(string counter)
        {
            return _counters.TryGetValue(counter, out var c) ? c.Value : 0;
        }

        /// <summary>Get all counter values as dictionary.</summary>
        public static Dictionary<string, long> GetAllCounters()
        {
            var result = new Dictionary<string, long>();
            foreach (var kvp in _counters)
                result[kvp.Key] = kvp.Value.Value;
            return result;
        }

        /// <summary>
        /// Get approximate p50/p95/p99 for a timed operation, or null if the operation has no
        /// recorded samples or <see cref="PercentilesEnabled"/> was never on while it recorded.
        /// Values are histogram-bucketed (see <see cref="PercentilesEnabled"/>).
        /// </summary>
        public static MetricPercentiles? GetPercentiles(string operation)
            => _metrics.TryGetValue(operation, out var e) ? e.ToPercentiles(operation) : null;

        /// <summary>Get approximate percentiles for every timed operation that has samples.</summary>
        public static Dictionary<string, MetricPercentiles> GetAllPercentiles()
        {
            var result = new Dictionary<string, MetricPercentiles>();
            foreach (var kvp in _metrics)
            {
                var p = kvp.Value.ToPercentiles(kvp.Key);
                if (p != null) result[kvp.Key] = p;
            }
            return result;
        }

        internal sealed class CounterEntry
        {
            private long _value;
            public CounterEntry(long initial) { _value = initial; }
            public void Add(long delta) => System.Threading.Interlocked.Add(ref _value, delta);
            public long Value => System.Threading.Interlocked.Read(ref _value);
        }

        #region Internal types

        private sealed class MeasureScope : IDisposable
        {
            private readonly string _operation;
            private readonly Stopwatch _sw;

            public MeasureScope(string operation)
            {
                _operation = operation;
                _sw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                var elapsedMs = _sw.Elapsed.TotalMilliseconds;

                _metrics.AddOrUpdate(_operation,
                    _ => new MetricEntry(elapsedMs),
                    (_, existing) => { existing.Record(elapsedMs); return existing; });

                // Per-scope logging removed — was causing severe debug lag
                // Use GridMetrics.GetSnapshot() to extract timing data instead
            }
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();
            public void Dispose() { }
        }

        internal sealed class MetricEntry
        {
            private long _callCount;
            private double _totalMs;
            private double _minMs;
            private double _maxMs;
            private double _lastMs;
            private readonly object _lock = new();

            // Fixed-bucket histogram for approximate percentiles. Lazily allocated only when
            // PercentilesEnabled records a sample, so memory is zero for the default case and
            // bounded to (buckets+1)*8 bytes ≈ 232 bytes/metric key regardless of sample count.
            private static readonly double[] _bucketUpperBoundsMs =
            {
                0.05, 0.1, 0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4, 5, 7.5, 10, 15, 20,
                30, 50, 75, 100, 150, 200, 300, 500, 750, 1000, 2000, 5000
            };
            private long[]? _histogram; // index [buckets] is the overflow bucket (> top bound)

            public MetricEntry(double firstMs)
            {
                _callCount = 1;
                _totalMs = firstMs;
                _minMs = firstMs;
                _maxMs = firstMs;
                _lastMs = firstMs;
                RecordHistogram(firstMs);
            }

            public void Record(double elapsedMs)
            {
                lock (_lock)
                {
                    _callCount++;
                    _totalMs += elapsedMs;
                    _lastMs = elapsedMs;
                    if (elapsedMs < _minMs) _minMs = elapsedMs;
                    if (elapsedMs > _maxMs) _maxMs = elapsedMs;
                    RecordHistogram(elapsedMs);
                }
            }

            // Must be called under _lock (Record) or from the constructor (not yet shared).
            private void RecordHistogram(double elapsedMs)
            {
                if (!_percentilesEnabled) return;
                _histogram ??= new long[_bucketUpperBoundsMs.Length + 1];
                int b = _bucketUpperBoundsMs.Length; // default = overflow bucket
                for (int i = 0; i < _bucketUpperBoundsMs.Length; i++)
                {
                    if (elapsedMs <= _bucketUpperBoundsMs[i]) { b = i; break; }
                }
                _histogram[b]++;
            }

            public MetricEntrySnapshot ToSnapshot()
            {
                lock (_lock)
                {
                    return new MetricEntrySnapshot(
                        _callCount,
                        Math.Round(_totalMs, 3),
                        Math.Round(_minMs, 3),
                        Math.Round(_maxMs, 3),
                        Math.Round(_lastMs, 3),
                        _callCount > 0 ? Math.Round(_totalMs / _callCount, 3) : 0);
                }
            }

            public MetricPercentiles? ToPercentiles(string operation)
            {
                lock (_lock)
                {
                    if (_histogram == null) return null;
                    long total = 0;
                    foreach (var c in _histogram) total += c;
                    if (total == 0) return null;
                    return new MetricPercentiles(
                        operation, total,
                        PercentileFromHistogram(total, 0.50),
                        PercentileFromHistogram(total, 0.95),
                        PercentileFromHistogram(total, 0.99));
                }
            }

            // Caller holds _lock. Returns the bucket upper bound at the requested quantile;
            // the overflow bucket falls back to the exact recorded max for a sane upper value.
            private double PercentileFromHistogram(long total, double p)
            {
                long threshold = (long)Math.Ceiling(p * total);
                long cumulative = 0;
                for (int i = 0; i < _histogram!.Length; i++)
                {
                    cumulative += _histogram[i];
                    if (cumulative >= threshold)
                        return i < _bucketUpperBoundsMs.Length
                            ? Math.Round(_bucketUpperBoundsMs[i], 3)
                            : Math.Round(_maxMs, 3);
                }
                return Math.Round(_maxMs, 3);
            }
        }

        #endregion
    }

    /// <summary>
    /// Immutable snapshot of a single metric entry.
    /// </summary>
    public record MetricEntrySnapshot(
        long CallCount,
        double TotalMs,
        double MinMs,
        double MaxMs,
        double LastMs,
        double AvgMs)
    {
        public override string ToString() =>
            $"Calls={CallCount}, Avg={AvgMs:F3}ms, Min={MinMs:F3}ms, Max={MaxMs:F3}ms, Last={LastMs:F3}ms, Total={TotalMs:F3}ms";
    }

    /// <summary>
    /// Approximate percentile summary for a timed operation. Values are histogram-bucketed
    /// (see <see cref="GridMetrics.PercentilesEnabled"/>), not exact order statistics.
    /// </summary>
    public record MetricPercentiles(
        string Operation,
        long SampleCount,
        double P50Ms,
        double P95Ms,
        double P99Ms)
    {
        public override string ToString() =>
            $"P50={P50Ms:F3}ms, P95={P95Ms:F3}ms, P99={P99Ms:F3}ms (n={SampleCount})";
    }

    /// <summary>
    /// Immutable snapshot of all collected metrics at a point in time.
    /// </summary>
    public record MetricsSnapshot(
        Dictionary<string, MetricEntrySnapshot> Entries,
        DateTime Timestamp,
        Dictionary<string, long>? Counters = null)
    {
        public override string ToString()
        {
            var lines = new List<string> { $"=== SkiaGrid Metrics Snapshot ({Timestamp:HH:mm:ss.fff}) ===" };
            lines.Add("-- Timed Operations --");
            foreach (var kvp in Entries.OrderBy(x => x.Key))
            {
                lines.Add($"  {kvp.Key,-30} {kvp.Value}");
            }
            if (Counters != null && Counters.Count > 0)
            {
                lines.Add("-- Counters --");
                foreach (var kvp in Counters.OrderBy(x => x.Key))
                {
                    lines.Add($"  {kvp.Key,-30} {kvp.Value:N0}");
                }
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}

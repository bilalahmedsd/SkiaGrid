using SkiaSharp;

namespace SkiaSharpControlV2.Renderer
{
    /// <summary>
    /// Static cache for reusable SKPaint objects keyed by color hex string.
    /// Eliminates per-frame SKPaint allocations in ButtonRenderer, SetterResolver, and GroupRowRenderer.
    /// SKPaint objects are created once per unique color and reused across all frames.
    /// Call Clear() on application shutdown to release native resources.
    /// </summary>
    public static class SKPaintCache
    {
        private static readonly Dictionary<string, SKPaint> _cache = new();

        /// <summary>
        /// Get or create a cached SKPaint for the given color hex string.
        /// Returns the same SKPaint instance for the same color — do NOT dispose the returned object.
        /// </summary>
        public static SKPaint Get(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex))
                return new SKPaint { Color = SKColors.Transparent, StrokeWidth = 1, IsAntialias = true };

            if (!_cache.TryGetValue(colorHex, out var paint))
            {
                SkiaSharpControlV2.Diagnostics.GridMetrics.Increment(SkiaSharpControlV2.Diagnostics.GridMetrics.CounterPaintCacheMisses);
                try
                {
                    paint = new SKPaint { Color = SKColor.Parse(colorHex), StrokeWidth = 1, IsAntialias = true };
                }
                catch
                {
                    paint = new SKPaint { Color = SKColors.Transparent, StrokeWidth = 1, IsAntialias = true };
                }
                _cache[colorHex] = paint;
            }
            else
            {
                SkiaSharpControlV2.Diagnostics.GridMetrics.Increment(SkiaSharpControlV2.Diagnostics.GridMetrics.CounterPaintCacheHits);
            }
            return paint;
        }

        /// <summary>
        /// Dispose all cached paints and clear the cache.
        /// Call on application shutdown or when the last SkiaRenderer is disposed.
        /// </summary>
        public static void Clear()
        {
            foreach (var p in _cache.Values)
                p?.Dispose();
            _cache.Clear();
        }

        /// <summary>Number of cached paints (for diagnostics).</summary>
        public static int Count => _cache.Count;
    }
}

using System.Collections.Concurrent;

namespace SkiaSharpControlV2.Renderer
{
    /// <summary>
    /// Manages expiration of timer-based trigger highlighting.
    /// For timer triggers: highlight for Duration seconds after value changes, then stop.
    /// Thread-safe via ConcurrentDictionary. Auto-cleans expired entries periodically.
    ///
    /// Performance: only called for timer-based triggers. Non-timer triggers bypass this entirely.
    /// Key is pre-computed per trigger instance to avoid per-frame string allocation.
    /// </summary>
    public static class SKTriggerStateCache
    {
        private static readonly ConcurrentDictionary<long, (object? LastValue, DateTime ExpireAt)> _cache = new();
        private static DateTime _lastCleanup = DateTime.Now;
        private static DateTime _frameNow = DateTime.Now;
        private const int CleanupIntervalSeconds = 30;

        /// <summary>Clear all cached trigger state. Call on Dispose to prevent unbounded growth.</summary>
        public static void Clear() => _cache.Clear();

        /// <summary>Number of cached entries (for diagnostics).</summary>
        public static int Count => _cache.Count;

        /// <summary>Call once per frame to cache DateTime.Now (avoids repeated syscalls per cell).</summary>
        public static void BeginFrame()
        {
            _frameNow = DateTime.Now;
        }

        /// <summary>
        /// Build a cache key from item + trigger + bindingPath. Uses hash combining instead of string allocation.
        /// </summary>
        public static long BuildKey(object item, object trigger, string bindingPath)
        {
            unchecked
            {
                long hash = 17;
                hash = hash * 31 + item.GetHashCode();
                hash = hash * 31 + trigger.GetHashCode();
                hash = hash * 31 + (bindingPath?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Check if a timer-based trigger should apply its highlight.
        /// ONLY call for timer-based triggers (IsTimerBased == true).
        /// </summary>
        public static bool ShouldApplyTimer(long key, double duration, object? currentValue)
        {
            _cache.TryGetValue(key, out var entry);

            // Value changed → reset timer
            if (!Equals(entry.LastValue, currentValue))
            {
                var dur = Math.Max(duration, 0.1); // min 100ms
                _cache[key] = (currentValue, _frameNow.AddSeconds(dur));
                return true;
            }

            // Same value, still in active duration
            if (entry.ExpireAt > _frameNow)
                return true;

            return false; // Expired
        }

        /// <summary>
        /// Remove expired timer entries to prevent unbounded cache growth.
        /// Called once per frame from the render loop.
        /// </summary>
        public static void CleanupExpired()
        {
            if ((_frameNow - _lastCleanup).TotalSeconds < CleanupIntervalSeconds)
                return;

            _lastCleanup = _frameNow;
            var keysToRemove = _cache
                .Where(kvp => kvp.Value.ExpireAt != DateTime.MinValue && kvp.Value.ExpireAt < _frameNow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
                _cache.TryRemove(key, out _);
        }
    }
}

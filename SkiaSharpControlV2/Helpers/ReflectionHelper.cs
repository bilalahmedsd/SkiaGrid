
using System.Linq.Expressions;

namespace SkiaSharpControlV2.Helpers
{
    public class ReflectionHelper
    {
        private readonly Dictionary<(Type, string), Func<object, object?>?> _getterCache = new();
        // Caches the Split('.') of each distinct dotted path so a per-cell/per-frame resolution
        // reuses one string[] instead of re-splitting. Bounded by the number of distinct paths
        // (~= column BindingPath/ValuePath count). Cleared alongside the getter/setter caches.
        private readonly Dictionary<string, string[]> _pathSegmentsCache = new();
        // Caches a compiled, boxing-free Comparison<object> per (type, property) for sorting.
        private readonly Dictionary<(Type, string), Comparison<object>?> _comparisonCache = new();

        /// <summary>Return the cached '.'-split segments of a dotted path (splits once per distinct path).</summary>
        private string[] GetPathSegments(string path)
        {
            if (!_pathSegmentsCache.TryGetValue(path, out var segs))
            {
                segs = path.Split('.');
                _pathSegmentsCache[path] = segs;
            }
            return segs;
        }

        /// <summary>
        /// Reads a property from currentItem using fast compiled reflection and returns value and type.
        /// Caches both successful and failed lookups to avoid repeated Expression.Lambda compilation.
        /// </summary>
        internal (string? Value, Type? Type) ReadCurrentItemWithTypes(object? currentItem, string propertyName)
        {
            if (currentItem == null || string.IsNullOrWhiteSpace(propertyName))
                return (null, null);

            // Support dot-notation paths (e.g., "Order.Price")
            if (propertyName.Contains('.'))
            {
                try
                {
                    var result = GetNestedValue(currentItem, propertyName);
                    return (result?.ToString(), result?.GetType());
                }
                catch { return (null, null); }
            }

            var type = currentItem.GetType();
            var key = (type, propertyName);

            if (!_getterCache.TryGetValue(key, out var getter))
            {
                // Check if property exists BEFORE attempting Expression compilation
                var propInfo = type.GetProperty(propertyName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                if (propInfo == null)
                {
                    // Cache null for non-existent properties — prevents repeated ArgumentException
                    _getterCache[key] = null;
                    return (null, null);
                }

                var param = Expression.Parameter(typeof(object), "obj");
                var castedObj = Expression.Convert(param, type);
                var property = Expression.Property(castedObj, propInfo);
                var convert = Expression.Convert(property, typeof(object));
                getter = Expression.Lambda<Func<object, object?>>(convert, param).Compile();
                _getterCache[key] = getter;
            }

            if (getter == null)
                return (null, null); // Cached as non-existent property

            try
            {
                SkiaSharpControlV2.Diagnostics.GridMetrics.Increment(SkiaSharpControlV2.Diagnostics.GridMetrics.CounterReflectionCalls);
                var result = getter(currentItem);
                return (result?.ToString(), result?.GetType());
            }
            catch
            {
                return (null, null);
            }
        }

        public object GetPropValue(object obj, string prop)
        {
            // Support dot-notation paths (e.g., "Order.Price")
            if (prop.Contains('.'))
                return GetNestedValue(obj, prop)!;

            var type = obj.GetType();
            var key = (type, prop);
            if (!_getterCache.TryGetValue(key, out var getter) || getter == null)
            {
                var propInfo = type.GetProperty(prop,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propInfo == null)
                {
                    _getterCache[key] = null;
                    return null!;
                }

                var param = Expression.Parameter(typeof(object));
                var body = Expression.Property(Expression.Convert(param, type), propInfo);
                var convert = Expression.Convert(body, typeof(object));
                getter = Expression.Lambda<Func<object, object>>(convert, param).Compile();
                _getterCache[key] = getter;
            }
            return getter!(obj);
        }

        /// <summary>
        /// Return a compiled, boxing-free <see cref="Comparison{Object}"/> for a single (non-dotted)
        /// property on <paramref name="type"/>, backed by <c>Comparer&lt;TProp&gt;.Default</c>. Compiled
        /// and cached once per (type, prop). Returns null for a dotted path or a missing property so the
        /// caller can fall back to the object-getter + non-generic Comparer.Default path.
        /// Uses Public|Instance (case-sensitive) to match <see cref="GetPropValue"/>.
        /// </summary>
        internal Comparison<object>? GetTypedComparison(Type type, string prop)
        {
            if (prop.Contains('.')) return null;

            var key = (type, prop);
            if (_comparisonCache.TryGetValue(key, out var cached)) return cached;

            var propInfo = type.GetProperty(prop,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (propInfo == null)
            {
                _comparisonCache[key] = null;
                return null;
            }

            try
            {
                // (object x, object y) => Comparer<TProp>.Default.Compare(((T)x).Prop, ((T)y).Prop)
                var xParam = Expression.Parameter(typeof(object), "x");
                var yParam = Expression.Parameter(typeof(object), "y");
                var xProp = Expression.Property(Expression.Convert(xParam, type), propInfo);
                var yProp = Expression.Property(Expression.Convert(yParam, type), propInfo);

                var comparerType = typeof(Comparer<>).MakeGenericType(propInfo.PropertyType);
                var defaultComparer = Expression.Property(null, comparerType.GetProperty("Default")!);
                var compareMethod = comparerType.GetMethod("Compare",
                    new[] { propInfo.PropertyType, propInfo.PropertyType })!;
                var call = Expression.Call(defaultComparer, compareMethod, xProp, yProp);

                var lambda = Expression.Lambda<Comparison<object>>(call, xParam, yParam).Compile();
                _comparisonCache[key] = lambda;
                return lambda;
            }
            catch
            {
                _comparisonCache[key] = null;
                return null;
            }
        }

        /// <summary>
        /// Traverse a dot-notation property path (e.g., "Order.Price").
        /// Each segment uses the compiled expression cache for performance.
        /// </summary>
        private object? GetNestedValue(object obj, string path)
        {
            object? current = obj;
            foreach (var segment in GetPathSegments(path))
            {
                if (current == null) return null;

                var type = current.GetType();
                var key = (type, segment);

                if (!_getterCache.TryGetValue(key, out var getter))
                {
                    var propInfo = type.GetProperty(segment,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                    if (propInfo == null)
                    {
                        _getterCache[key] = null;
                        return null;
                    }

                    var param = Expression.Parameter(typeof(object), "obj");
                    var castedObj = Expression.Convert(param, type);
                    var property = Expression.Property(castedObj, propInfo);
                    var convert = Expression.Convert(property, typeof(object));
                    getter = Expression.Lambda<Func<object, object?>>(convert, param).Compile();
                    _getterCache[key] = getter;
                }

                if (getter == null) return null;
                current = getter(current);
            }
            return current;
        }

        // ── Setter (Write) ──────────────────────────────────────────────

        private readonly Dictionary<(Type, string), Action<object, object?>?> _setterCache = new();

        /// <summary>
        /// Set a property value on an object. Uses compiled expression cache for performance.
        /// Supports dot-notation: for "Order.IsActive", traverses to Order then sets IsActive.
        /// </summary>
        public void SetPropValue(object obj, string prop, object? value)
        {
            if (obj == null || string.IsNullOrEmpty(prop)) return;

            // Dot-notation: traverse to parent, set leaf property
            if (prop.Contains('.'))
            {
                var parts = GetPathSegments(prop);
                var parentPath = string.Join('.', parts[..^1]);
                var leafProp = parts[^1];
                var parent = GetNestedValue(obj, parentPath);
                if (parent != null)
                    SetPropValueDirect(parent, leafProp, value);
                return;
            }

            SetPropValueDirect(obj, prop, value);
        }

        private void SetPropValueDirect(object obj, string prop, object? value)
        {
            var type = obj.GetType();
            var key = (type, prop);

            if (!_setterCache.TryGetValue(key, out var setter))
            {
                var propInfo = type.GetProperty(prop,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propInfo == null || !propInfo.CanWrite)
                {
                    _setterCache[key] = null;
                    return;
                }

                var objParam = Expression.Parameter(typeof(object), "obj");
                var valParam = Expression.Parameter(typeof(object), "val");
                var castObj = Expression.Convert(objParam, type);
                var castVal = Expression.Convert(valParam, propInfo.PropertyType);
                var setProp = Expression.Assign(Expression.Property(castObj, propInfo), castVal);
                setter = Expression.Lambda<Action<object, object?>>(setProp, objParam, valParam).Compile();
                _setterCache[key] = setter;
            }

            setter?.Invoke(obj, value);
        }

        // ── Cache Management ────────────────────────────────────────────

        /// <summary>Number of cached getter delegates.</summary>
        public int CacheCount => _getterCache.Count;

        /// <summary>
        /// Clear the getter cache. Useful when ItemsSource changes to a different type,
        /// or on Dispose to release compiled delegates.
        /// </summary>
        public void ClearCache() { _getterCache.Clear(); _setterCache.Clear(); _pathSegmentsCache.Clear(); _comparisonCache.Clear(); }

        /// <summary>
        /// Remove cached entries for a specific type (e.g., when that data type is no longer used).
        /// </summary>
        public void ClearCacheForType(Type type)
        {
            var keysToRemove = _getterCache.Keys.Where(k => k.Item1 == type).ToList();
            foreach (var key in keysToRemove)
                _getterCache.Remove(key);

            var cmpKeysToRemove = _comparisonCache.Keys.Where(k => k.Item1 == type).ToList();
            foreach (var key in cmpKeysToRemove)
                _comparisonCache.Remove(key);
        }
    }
}

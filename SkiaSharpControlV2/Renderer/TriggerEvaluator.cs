using SkiaSharp;

using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;

namespace SkiaSharpControlV2.Renderer
{
    /// <summary>
    /// Evaluates triggers (row/cell and group level) and resolves their setters to paint values.
    /// Extracted from SkiaRenderer (Step 4 refactoring).
    /// Performance-critical: called per-cell on every paint frame.
    /// </summary>
    internal class TriggerEvaluator
    {
        private readonly SetterResolver _setterResolver;

        public TriggerEvaluator(SetterResolver setterResolver)
        {
            _setterResolver = setterResolver;
        }

        // ── Row/Cell Trigger Evaluation ─────────────────────────────────

        /// <summary>
        /// Evaluate row/cell triggers. Returns paint values from first matching trigger (first-match-wins).
        /// All triggers go through SKTriggerStateCache (timer and non-timer).
        /// </summary>
        // Per-trigger cached metadata to avoid per-frame LINQ/string allocations
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<SKTrigger, (string valuePath, string bindingPath)>
            _triggerMetaCache = new();

        public static (SKPaint BackgroundColor, SKPaint Foregroundcolor, SKPaint BorderColor) GetTriggerTemplate(
            object item, ReflectionHelper reflection, IEnumerable<SKTrigger> triggers)
        {
            SKPaint backgroundColor = null;
            SKPaint foregroundcolor = null;
            SKPaint borderColor = null;

            if (triggers is ICollection<SKTrigger> col && col.Count == 0)
                return (backgroundColor, foregroundcolor, borderColor);
            if (triggers == null)
                return (backgroundColor, foregroundcolor, borderColor);

            foreach (var trigger in triggers)
            {
                SkiaSharpControlV2.Diagnostics.GridMetrics.Increment(SkiaSharpControlV2.Diagnostics.GridMetrics.CounterTriggersEvaluated);
                if (!trigger.Evaluate(item, reflection))
                    continue;

                SkiaSharpControlV2.Diagnostics.GridMetrics.Increment(SkiaSharpControlV2.Diagnostics.GridMetrics.CounterTriggersMatched);

                // Non-timer triggers: skip cache entirely — just resolve setters
                if (!trigger.IsTimerBased)
                    return SetterResolver.GetSetterValues(reflection, trigger.Setters, item);

                // Timer triggers: use cached metadata + timer state cache
                var meta = GetTriggerMeta(trigger);

                object? currentValue = null;
                if (meta.valuePath.Length > 0)
                {
                    var (strVal, _) = reflection.ReadCurrentItemWithTypes(item, meta.valuePath);
                    currentValue = strVal;
                }

                var key = SKTriggerStateCache.BuildKey(item, trigger, meta.bindingPath);
                if (SKTriggerStateCache.ShouldApplyTimer(key, trigger.Duration, currentValue))
                    return SetterResolver.GetSetterValues(reflection, trigger.Setters, item);
            }

            return (backgroundColor, foregroundcolor, borderColor);
        }

        /// <summary>
        /// Cache per-trigger metadata (valuePath, bindingPath) to avoid per-frame LINQ allocations.
        /// Computed once per trigger instance.
        /// </summary>
        private static (string valuePath, string bindingPath) GetTriggerMeta(SKTrigger trigger)
        {
            return _triggerMetaCache.GetOrAdd(trigger, t =>
            {
                string valuePath, bindingPath;

                if (t is SKDataTrigger dt)
                {
                    valuePath = dt.BindingPath ?? "";
                    bindingPath = valuePath;
                }
                else if (t is SKMultiTrigger mt && mt.Conditions != null)
                {
                    // Only data-item BindingPaths (skip ViewModel Binding conditions)
                    var dataPaths = new List<string>();
                    string firstPath = "";
                    foreach (var c in mt.Conditions)
                    {
                        if (!string.IsNullOrEmpty(c.BindingPath))
                        {
                            dataPaths.Add(c.BindingPath!);
                            if (firstPath.Length == 0) firstPath = c.BindingPath!;
                        }
                    }
                    valuePath = firstPath;
                    bindingPath = string.Join("|", dataPaths);
                }
                else
                {
                    valuePath = "";
                    bindingPath = "";
                }

                return (valuePath, bindingPath);
            });
        }

        /// <summary>Clear cached trigger metadata. Call when triggers are reconfigured.</summary>
        public static void ClearTriggerMetaCache() => _triggerMetaCache.Clear();

        // ── Group Trigger Evaluation ────────────────────────────────────

        /// <summary>
        /// Evaluate group-level triggers. Returns paint values from first matching trigger.
        /// Group triggers do NOT go through SKTriggerStateCache (no timer support).
        /// </summary>
        public (SKPaint BackgroundColor, SKPaint ForegroundColor, SKPaint BorderColor) GetGroupTriggerTemplate(
            ReflectionHelper reflectionHelper,
            IEnumerable<GroupModel> groupItems,
            IEnumerable<SKGroupTrigger>? triggers)
        {
            SKPaint backgroundColor = null;
            SKPaint foregroundColor = null;
            SKPaint borderColor = null;

            if (triggers == null || !triggers.Any())
                return (backgroundColor, foregroundColor, borderColor);

            foreach (var trigger in triggers)
            {
                bool isMatch = false;

                switch (trigger)
                {
                    case SkGroupDataTrigger dataTrigger:
                        isMatch = EvaluateGroupDataTriggerInternal(dataTrigger, groupItems, reflectionHelper);
                        break;

                    case SKGroupMultiTrigger multiTrigger:
                        isMatch = EvaluateGroupMultiTriggerInternal(multiTrigger, groupItems, reflectionHelper);
                        break;
                }

                if (isMatch && trigger.Setters != null && trigger.Setters.Any())
                {
                    return SetterResolver.GetSetterValues(reflectionHelper, trigger.Setters, groupItems);
                }
            }

            return (backgroundColor, foregroundColor, borderColor);
        }

        // ── Group Trigger Internal Evaluation ───────────────────────────

        private bool EvaluateGroupDataTriggerInternal(
            SkGroupDataTrigger trigger,
            IEnumerable<GroupModel> groupItems,
            ReflectionHelper reflectionHelper)
        {
            object? leftValue = null;

            if (trigger.Binding != null) // direct binding mode
            {
                leftValue = trigger.Binding;
            }
            else if (!string.IsNullOrEmpty(trigger.BindingPath)) // aggregation mode
            {
                leftValue = _setterResolver.CalculateGroupAggregation(
                    groupItems,
                    reflectionHelper,
                    trigger.BindingPath!,
                    trigger.Aggregation
                );
            }

            if (leftValue == null)
                return false;

            return CompareValues(leftValue, trigger.Value, trigger.Operator);
        }

        private bool EvaluateGroupMultiTriggerInternal(
            SKGroupMultiTrigger multiTrigger,
            IEnumerable<GroupModel> groupItems,
            ReflectionHelper reflectionHelper)
        {
            if (multiTrigger.Conditions == null || multiTrigger.Conditions.Count == 0)
                return false;

            return multiTrigger.Conditions.All(c => EvaluateGroupConditionInternal(c, groupItems, reflectionHelper));
        }

        private bool EvaluateGroupConditionInternal(
            SKGroupCondition condition,
            IEnumerable<GroupModel> groupItems,
            ReflectionHelper reflectionHelper)
        {
            object? leftValue = null;

            if (condition.Binding != null) // Direct Binding
            {
                leftValue = condition.Binding;
            }
            else if (!string.IsNullOrEmpty(condition.BindingPath)) // Aggregation mode
            {
                leftValue = _setterResolver.CalculateGroupAggregation(
                    groupItems,
                    reflectionHelper,
                    condition.BindingPath!,
                    condition.Aggregation
                );
            }

            if (leftValue == null)
                return false;

            return CompareValues(leftValue, condition.Value, condition.Operator);
        }

        // ── Value Comparison ────────────────────────────────────────────

        /// <summary>
        /// Type-aware value comparison. Priority: double → bool → string (case-insensitive).
        /// Supports all 6 operators. Returns false if either value is null.
        /// </summary>
        internal static bool CompareValues(object left, object right, SKOperation op)
        {
            if (left == null || right == null)
                return false;

            // Try to normalize types
            if (left is IConvertible && right is IConvertible)
            {
                // Try number compare
                if (double.TryParse(left.ToString(), out var leftNum) &&
                    double.TryParse(right.ToString(), out var rightNum))
                {
                    return op switch
                    {
                        SKOperation.Equals => leftNum == rightNum,
                        SKOperation.NotEquals => leftNum != rightNum,
                        SKOperation.GreaterThan => leftNum > rightNum,
                        SKOperation.GreaterThanOrEqual => leftNum >= rightNum,
                        SKOperation.LessThan => leftNum < rightNum,
                        SKOperation.LessThanOrEqual => leftNum <= rightNum,
                        _ => false
                    };
                }

                // Try bool compare
                if (bool.TryParse(left.ToString(), out var leftBool) &&
                    bool.TryParse(right.ToString(), out var rightBool))
                {
                    return op switch
                    {
                        SKOperation.Equals => leftBool == rightBool,
                        SKOperation.NotEquals => leftBool != rightBool,
                        _ => false // For bool, only equality/inequality make sense
                    };
                }
            }

            // String compare (case-insensitive)
            var leftStr = left.ToString();
            var rightStr = right.ToString();

            return op switch
            {
                SKOperation.Equals => string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase),
                SKOperation.NotEquals => !string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase),
                SKOperation.GreaterThan => string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) > 0,
                SKOperation.GreaterThanOrEqual => string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) >= 0,
                SKOperation.LessThan => string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) < 0,
                SKOperation.LessThanOrEqual => string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) <= 0,
                _ => false
            };
        }
    }
}

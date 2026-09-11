
using SkiaSharpControlV2.Helpers;
using System.Collections;
using System.Windows;

namespace SkiaSharpControlV2
{
    /// <summary>Base class for conditional style triggers. First matching trigger wins.</summary>
    public abstract class SKTrigger : Freezable
    {

        public bool IsTimerBased
        {
            get => (bool)GetValue(IsTimerBasedProperty);
            set => SetValue(IsTimerBasedProperty, value);
        }
        public static readonly DependencyProperty IsTimerBasedProperty =
       DependencyProperty.Register(nameof(IsTimerBased), typeof(bool), typeof(SKTrigger), new PropertyMetadata(false));

        public double Duration
        {
            get => (double)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }
        public static readonly DependencyProperty DurationProperty =
       DependencyProperty.Register(nameof(Duration), typeof(double), typeof(SKTrigger), new PropertyMetadata(0.0, OnDurationChanged));

        private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Auto-infer IsTimerBased when Duration > 0 — prevents misconfiguration
            if (d is SKTrigger trigger && e.NewValue is double duration && duration > 0)
                trigger.IsTimerBased = true;
        }

        public FreezableCollection<SKSetter> Setters
        {
            get => (FreezableCollection<SKSetter>)GetValue(SettersProperty);
            set => SetValue(SettersProperty, value);
        }
        public static readonly DependencyProperty SettersProperty =
            DependencyProperty.Register(nameof(Setters), typeof(FreezableCollection<SKSetter>), typeof(SKTrigger));

        protected SKTrigger()
        {
            SetValue(SettersProperty, new FreezableCollection<SKSetter>());
        }

        public abstract bool Evaluate(object dataContext, ReflectionHelper helper);
        public static bool EvaluateCondition(string? leftStr, Type? leftType, object rightVal, SKOperation op)
        {
            if (leftType == null || leftStr == null) return false;

            try
            {
                var left = Convert.ChangeType(leftStr, leftType);
                var right = Convert.ChangeType(rightVal, leftType);

                int cmp = Comparer.DefaultInvariant.Compare(left, right);

                return op switch
                {
                    SKOperation.Equals => cmp == 0,
                    SKOperation.NotEquals => cmp != 0,
                    SKOperation.GreaterThan => cmp > 0,
                    SKOperation.LessThan => cmp < 0,
                    SKOperation.GreaterThanOrEqual => cmp >= 0,
                    SKOperation.LessThanOrEqual => cmp <= 0,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Base class for group-level conditional triggers (no timer support).</summary>
    public abstract class SKGroupTrigger : Freezable
    {
        public FreezableCollection<SKSetter> Setters
        {
            get => (FreezableCollection<SKSetter>)GetValue(SettersProperty);
            set => SetValue(SettersProperty, value);
        }
        public static readonly DependencyProperty SettersProperty =
            DependencyProperty.Register(nameof(Setters), typeof(FreezableCollection<SKSetter>), typeof(SKGroupTrigger));

        protected SKGroupTrigger()
        {
            SetValue(SettersProperty, new FreezableCollection<SKSetter>());
        }
    }

    /// <summary>Single-condition group trigger evaluated against aggregated data.</summary>
    public class SkGroupDataTrigger : SKGroupTrigger
    {
        public object Binding
        {
            get => GetValue(BindingProperty);
            set => SetValue(BindingProperty, value);
        }
        public static readonly DependencyProperty BindingProperty =
       DependencyProperty.Register(nameof(Binding), typeof(object), typeof(SkGroupDataTrigger), new PropertyMetadata(null));
        public string? BindingPath { get; set; }

        public SkAggregation Aggregation { get; set; }

        public SKOperation Operator { get; set; }
        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(object), typeof(SkGroupDataTrigger), new PropertyMetadata(null));

        protected override Freezable CreateInstanceCore() => new SkGroupDataTrigger();
    }

    /// <summary>Multi-condition (AND) group trigger evaluated against aggregated data.</summary>
    public class SKGroupMultiTrigger : SKGroupTrigger
    {
        public SKGroupMultiTrigger()
        {
            SetValue(ConditionsProperty, new FreezableCollection<SKGroupCondition>());
        }

        public FreezableCollection<SKGroupCondition> Conditions
        {
            get => (FreezableCollection<SKGroupCondition>)GetValue(ConditionsProperty);
            set => SetValue(ConditionsProperty, value);
        }
        public static readonly DependencyProperty ConditionsProperty =
            DependencyProperty.Register(nameof(Conditions), typeof(FreezableCollection<SKGroupCondition>), typeof(SKGroupMultiTrigger));

        protected override Freezable CreateInstanceCore() => new SKGroupMultiTrigger();
    }

    /// <summary>Single-condition cell/row trigger. Supports timer-based highlighting.</summary>
    public class SKDataTrigger : SKTrigger
    {
        public object Binding
        {
            get => GetValue(BindingProperty);
            set => SetValue(BindingProperty, value);
        }
        public static readonly DependencyProperty BindingProperty =
       DependencyProperty.Register(nameof(Binding), typeof(object), typeof(SKDataTrigger), new PropertyMetadata(null));

        public string? BindingPath { get; set; }
        public SKOperation Operator { get; set; }
        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(object), typeof(SKDataTrigger), new PropertyMetadata(null));


        public override bool Evaluate(object dataContext, ReflectionHelper helper)
        {
            string? actualValue = null;
            Type? valueType = null;

            if (!string.IsNullOrEmpty(BindingPath))
            {
                (actualValue, valueType) = helper.ReadCurrentItemWithTypes(dataContext, BindingPath);
            }
            else if (Binding != null)
            {
                actualValue = Binding?.ToString();
                valueType = Binding?.GetType();
            }

            // If still null, then invalid comparison
            if (actualValue == null || Value == null)
                return false;

            return EvaluateCondition(actualValue, valueType, Value, Operator);
        }

        protected override Freezable CreateInstanceCore() => new SKDataTrigger();
    }

    /// <summary>Multi-condition (AND) cell/row trigger. All conditions must match.</summary>
    public class SKMultiTrigger : SKTrigger
    {
        public SKMultiTrigger()
        {
            SetValue(ConditionsProperty, new FreezableCollection<SKCondition>());
        }

        public FreezableCollection<SKCondition> Conditions
        {
            get => (FreezableCollection<SKCondition>)GetValue(ConditionsProperty);
            set => SetValue(ConditionsProperty, value);
        }
        public static readonly DependencyProperty ConditionsProperty =
            DependencyProperty.Register(nameof(Conditions), typeof(FreezableCollection<SKCondition>), typeof(SKMultiTrigger));

        public override bool Evaluate(object dataContext, ReflectionHelper helper)
        {
            foreach (var item in Conditions)
            {
                string? actualValue = null;
                Type? valueType = null;

                if (!string.IsNullOrEmpty(item.BindingPath))
                {
                    (actualValue, valueType) = helper.ReadCurrentItemWithTypes(dataContext, item.BindingPath);
                }
                else if (item.Binding != null)
                {
                    actualValue = item.Binding?.ToString();
                    valueType = item.Binding?.GetType();
                }

                // If still null, then invalid comparison
                if (actualValue == null || item.Value == null)
                    return false;

                var res = EvaluateCondition(actualValue, valueType, item.Value, item.Operator);
                if (!res) return false;
            }
            return true;
        }

        protected override Freezable CreateInstanceCore() => new SKMultiTrigger();
    }
}

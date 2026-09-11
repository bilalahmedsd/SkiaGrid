
using System.Windows;

namespace SkiaSharpControlV2
{
    /// <summary>Single condition for trigger evaluation: BindingPath + Operator + Value.</summary>
    public class SKCondition : Freezable
    {
        public object Binding
        {
            get => GetValue(BindingProperty);
            set => SetValue(BindingProperty, value);
        }
        public static readonly DependencyProperty BindingProperty =
       DependencyProperty.Register(nameof(Binding), typeof(object), typeof(SKCondition), new PropertyMetadata(null));
        public string BindingPath { get; set; } = "";
        public SKOperation Operator { get; set; }


        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(object), typeof(SKCondition), new PropertyMetadata(null));

        protected override Freezable CreateInstanceCore() => new SKCondition();
    }

    /// <summary>Group-level condition with aggregation support for group trigger evaluation.</summary>
    public class SKGroupCondition : Freezable
    {
        public object Binding
        {
            get => GetValue(BindingProperty);
            set => SetValue(BindingProperty, value);
        }
        public static readonly DependencyProperty BindingProperty =
       DependencyProperty.Register(nameof(Binding), typeof(object), typeof(SKGroupCondition), new PropertyMetadata(null));
        public string? BindingPath { get; set; }

        public SkAggregation Aggregation { get; set; }

        public SKOperation Operator { get; set; }
        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(object), typeof(SKGroupCondition), new PropertyMetadata(null));

        protected override Freezable CreateInstanceCore() => new SKGroupCondition();
    }
}

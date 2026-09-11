
using System.Windows;
using System.Windows.Markup;

namespace SkiaSharpControlV2
{
    /// <summary>Sets a style property value. Use Value for literal, ValuePath for data-bound dynamic colors.</summary>
    public class SKSetter : Freezable
    {
        public SkStyleProperty Property { get; set; }

        public string? ValuePath { get; set; }

        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        public static readonly DependencyProperty ValueProperty =
       DependencyProperty.Register(nameof(Value), typeof(object), typeof(SKSetter), new PropertyMetadata(null));

        protected override Freezable CreateInstanceCore() => new SKSetter();
    }

    [ContentProperty(nameof(Triggers))]
    /// <summary>Cell template with setters, triggers, and optional buttons.</summary>
    public class SKCellTemplate : Freezable
    {
        public SKCellTemplate()
        {
            SetValue(SettersProperty, new FreezableCollection<SKSetter>());
            SetValue(TriggersProperty, new FreezableCollection<SKTrigger>());
            SetValue(SkButtonsProperty, new FreezableCollection<SkButton>());
        }

        public SkButton? SkButton
        {
            get => (SkButton?)GetValue(SkButtonProperty);
            set => SetValue(SkButtonProperty, value);
        }
        public static readonly DependencyProperty SkButtonProperty =
            DependencyProperty.Register(nameof(SkButton), typeof(SkButton), typeof(SKCellTemplate), new PropertyMetadata(null));

        public FreezableCollection<SkButton> SkButtons
        {
            get => (FreezableCollection<SkButton>)GetValue(SkButtonsProperty);
            set => SetValue(SkButtonsProperty, value);
        }
        public static readonly DependencyProperty SkButtonsProperty =
            DependencyProperty.Register(nameof(SkButtons), typeof(FreezableCollection<SkButton>), typeof(SKCellTemplate));

        public Func<object, List<SkButton>> DrawButton
        {
            get => (Func<object, List<SkButton>>)GetValue(DrawButtonProperty);
            set => SetValue(DrawButtonProperty, value);
        }

        public static readonly DependencyProperty DrawButtonProperty =
            DependencyProperty.Register(nameof(DrawButton), typeof(Func<object, List<SkButton>>), typeof(SKCellTemplate), new PropertyMetadata(default));

        /// <summary>
        /// Optional custom drawing delegate for cells in this template. When set, the
        /// renderer draws the background (so triggers and ItemStatus still work), clips
        /// the canvas to the cell rect, invokes this delegate, then SKIPS the default
        /// text rendering. The delegate has full Skia access via <see cref="SkCellDrawContext.Canvas"/>
        /// — draw sparklines, mini-charts, trend boxes, progress bars, anything.
        /// <para>
        /// Any drawing outside the cell rect is silently clipped by Skia; the delegate
        /// cannot bleed into neighbouring cells.
        /// </para>
        /// </summary>
        public Action<SkCellDrawContext>? CustomDraw
        {
            get => (Action<SkCellDrawContext>?)GetValue(CustomDrawProperty);
            set => SetValue(CustomDrawProperty, value);
        }

        public static readonly DependencyProperty CustomDrawProperty =
            DependencyProperty.Register(nameof(CustomDraw), typeof(Action<SkCellDrawContext>), typeof(SKCellTemplate), new PropertyMetadata(default));

        public FreezableCollection<SKSetter> Setters
        {
            get => (FreezableCollection<SKSetter>)GetValue(SettersProperty);
            set => SetValue(SettersProperty, value);
        }
        public static readonly DependencyProperty SettersProperty =
            DependencyProperty.Register(nameof(Setters), typeof(FreezableCollection<SKSetter>), typeof(SKCellTemplate));

        public FreezableCollection<SKTrigger> Triggers
        {
            get => (FreezableCollection<SKTrigger>)GetValue(TriggersProperty);
            set => SetValue(TriggersProperty, value);
        }
        public static readonly DependencyProperty TriggersProperty =
            DependencyProperty.Register(nameof(Triggers), typeof(FreezableCollection<SKTrigger>), typeof(SKCellTemplate));

        protected override Freezable CreateInstanceCore() => new SKCellTemplate();
    }

    [ContentProperty(nameof(Triggers))]
    /// <summary>Template for group header cells with setters and group-level triggers.</summary>
    public class SKGroupCellTemplate : Freezable
    {
        public SKGroupCellTemplate()
        {
            SetValue(SettersProperty, new FreezableCollection<SKSetter>());
            SetValue(TriggersProperty, new FreezableCollection<SKGroupTrigger>());
        }

        public FreezableCollection<SKSetter> Setters
        {
            get => (FreezableCollection<SKSetter>)GetValue(SettersProperty);
            set => SetValue(SettersProperty, value);
        }
        public static readonly DependencyProperty SettersProperty =
            DependencyProperty.Register(nameof(Setters), typeof(FreezableCollection<SKSetter>), typeof(SKGroupCellTemplate));

        public FreezableCollection<SKGroupTrigger> Triggers
        {
            get => (FreezableCollection<SKGroupTrigger>)GetValue(TriggersProperty);
            set => SetValue(TriggersProperty, value);
        }
        public static readonly DependencyProperty TriggersProperty =
            DependencyProperty.Register(nameof(Triggers), typeof(FreezableCollection<SKGroupTrigger>), typeof(SKGroupCellTemplate));

        protected override Freezable CreateInstanceCore() => new SKGroupCellTemplate();
    }

    [ContentProperty(nameof(Triggers))]
    /// <summary>Row-level template with setters and triggers for conditional row styling.</summary>
    public class SKRowTemplate : Freezable
    {
        public SKRowTemplate()
        {
            SetValue(SettersProperty, new FreezableCollection<SKSetter>());
            SetValue(TriggersProperty, new FreezableCollection<SKTrigger>());
        }

        public FreezableCollection<SKSetter> Setters
        {
            get => (FreezableCollection<SKSetter>)GetValue(SettersProperty);
            set => SetValue(SettersProperty, value);
        }
        public static readonly DependencyProperty SettersProperty =
            DependencyProperty.Register(nameof(Setters), typeof(FreezableCollection<SKSetter>), typeof(SKRowTemplate));

        public FreezableCollection<SKTrigger> Triggers
        {
            get => (FreezableCollection<SKTrigger>)GetValue(TriggersProperty);
            set => SetValue(TriggersProperty, value);
        }
        public static readonly DependencyProperty TriggersProperty =
            DependencyProperty.Register(nameof(Triggers), typeof(FreezableCollection<SKTrigger>), typeof(SKRowTemplate));

        protected override Freezable CreateInstanceCore() => new SKRowTemplate();
    }
}

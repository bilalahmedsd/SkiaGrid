
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SkiaSharpControlV2
{
    /// <summary>Defines grouping behavior: GroupBy property, target column, header fields, toggle symbols.</summary>
    public class SKGroupDefinition : Freezable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public SKGroupDefinition()
        {
            SetValue(HeaderFieldsProperty, new FreezableCollection<SKGroupField>());
        }

        public string ForegroundColor
        {
            get { return (string)GetValue(ForegroundColorProperty); }
            set { SetValue(ForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.Register(nameof(ForegroundColor), typeof(string), typeof(SKGroupDefinition), new PropertyMetadata(default, (s, e) => TriggerChanged(s, e)));

        public string RowBackground
        {
            get { return (string)GetValue(RowBackgroundProperty); }
            set { SetValue(RowBackgroundProperty, value); }
        }

        public static readonly DependencyProperty RowBackgroundProperty =
            DependencyProperty.Register(nameof(RowBackground), typeof(string), typeof(SKGroupDefinition), new PropertyMetadata(default, (s, e) => TriggerChanged(s, e)));

        public SKGroupCellTemplate? GroupCellTemplate
        {
            get => (SKGroupCellTemplate?)GetValue(GroupCellTemplateProperty);
            set => SetValue(GroupCellTemplateProperty, value);
        }
        public static readonly DependencyProperty GroupCellTemplateProperty =
            DependencyProperty.Register(nameof(GroupCellTemplate), typeof(SKGroupCellTemplate), typeof(SKGroupDefinition), new PropertyMetadata(null));

        public string? GroupBy { get; set; }
        public string? Target { get; set; }

        public FreezableCollection<SKGroupField> HeaderFields
        {
            get => (FreezableCollection<SKGroupField>)GetValue(HeaderFieldsProperty);
            set => SetValue(HeaderFieldsProperty, value);
        }
        public static readonly DependencyProperty HeaderFieldsProperty =
            DependencyProperty.Register(nameof(HeaderFields), typeof(FreezableCollection<SKGroupField>), typeof(SKGroupDefinition));

        public SKGroupToggleSymbol? ToggleSymbol { get; set; }

        /// <summary>
        /// Opt-in (default <c>false</c>): when a group is collapsed, keep its subtotal rows visible
        /// under the group header instead of hiding them with the data rows. Covers the per-group
        /// subtotal rows (<c>IsGroupSubTotal</c>) and, on <c>CollapseAll</c>, the grand-total rows
        /// (<c>IsHeaderSubTotal</c>). Additive: with the default <c>false</c> the collapse behavior is
        /// byte-for-byte unchanged (subtotals collapse away with the data rows).
        /// </summary>
        public bool ShowSubtotalsWhenCollapsed
        {
            get => (bool)GetValue(ShowSubtotalsWhenCollapsedProperty);
            set => SetValue(ShowSubtotalsWhenCollapsedProperty, value);
        }

        public static readonly DependencyProperty ShowSubtotalsWhenCollapsedProperty =
            DependencyProperty.Register(nameof(ShowSubtotalsWhenCollapsed), typeof(bool), typeof(SKGroupDefinition), new PropertyMetadata(false));


        private static void TriggerChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            if (s is SKGroupDefinition a) a.OnPropertyChanged(e.Property.ToString());
        }

        protected override Freezable CreateInstanceCore() => new SKGroupDefinition();
    }

    /// <summary>Defines an aggregation field shown in group headers (e.g., Sum of Price).</summary>
    public class SKGroupField : Freezable
    {
        public string BindingPath { get; set; } = "";
        public string TargetColumns { get; set; } = "";
        public SkAggregation Aggregation { get; set; }  // Sum, Count, Avg, Min, Max

        public SKGroupCellTemplate? GroupCellTemplate
        {
            get => (SKGroupCellTemplate?)GetValue(GroupCellTemplateProperty);
            set => SetValue(GroupCellTemplateProperty, value);
        }
        public static readonly DependencyProperty GroupCellTemplateProperty =
            DependencyProperty.Register(nameof(GroupCellTemplate), typeof(SKGroupCellTemplate), typeof(SKGroupField), new PropertyMetadata(null));

        protected override Freezable CreateInstanceCore() => new SKGroupField();
    }

    /// <summary>Configures expand/collapse toggle symbols and styling for group headers.</summary>
    public class SKGroupToggleSymbol : Freezable
    {
        public string? TargetColumns { get; set; }
        public string? Expand { get; set; }
        public string? Collapse { get; set; }
        public bool? ShowGroupDetail { get; set; }
        public string? BackgroundColor
        {
            get => (string?)GetValue(BackgroundColorProperty);
            set { SetValue(BackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(nameof(BackgroundColor), typeof(string), typeof(SKGroupToggleSymbol), new PropertyMetadata(null));

        public string? ForegroundColor
        {
            get => (string?)GetValue(ForegroundColorProperty);
            set { SetValue(ForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.Register(nameof(ForegroundColor), typeof(string), typeof(SKGroupToggleSymbol), new PropertyMetadata(null));

        protected override Freezable CreateInstanceCore() => new SKGroupToggleSymbol();
    }
}

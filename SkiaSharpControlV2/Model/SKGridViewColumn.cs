
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SkiaSharpControlV2
{
    /// <summary>Base column class with binding path, content alignment, format, and cell template properties.</summary>
    public class SKBaseColumn : Freezable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        internal void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));


        public string? Name
        {
            get => (string?)GetValue(NameProperty);
            set { SetValue(NameProperty, value); OnPropertyChanged(); }
        }

        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register(nameof(Name), typeof(string), typeof(SKBaseColumn), new PropertyMetadata(null));


        public string BindingPath
        {
            get => (string)GetValue(BindingPathProperty);
            set => SetValue(BindingPathProperty, value);
        }

        public static readonly DependencyProperty BindingPathProperty =
            DependencyProperty.Register(nameof(BindingPath), typeof(string), typeof(SKBaseColumn), new PropertyMetadata(null));

        public CellContentAlignment ContentAlignment
        {
            get => (CellContentAlignment)GetValue(ContentAlignmentProperty);
            set => SetValue(ContentAlignmentProperty, value);
        }

        public static readonly DependencyProperty ContentAlignmentProperty =
            DependencyProperty.Register(nameof(ContentAlignment), typeof(CellContentAlignment), typeof(SKBaseColumn), new PropertyMetadata(CellContentAlignment.Left));


        public SKCellTemplate? CellTemplate
        {
            get => (SKCellTemplate?)GetValue(CellTemplateProperty);
            set => SetValue(CellTemplateProperty, value);
        }
        public static readonly DependencyProperty CellTemplateProperty =
            DependencyProperty.Register(nameof(CellTemplate), typeof(SKCellTemplate), typeof(SKBaseColumn), new PropertyMetadata(null));

        public string? Format
        {
            get => (string?)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }
        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register(nameof(Format), typeof(string), typeof(SKBaseColumn), new PropertyMetadata(null));

        public bool ShowBracketOnNegative
        {
            get => (bool)GetValue(ShowBracketOnNegativeProperty);
            set => SetValue(ShowBracketOnNegativeProperty, value);
        }
        public static readonly DependencyProperty ShowBracketOnNegativeProperty =
            DependencyProperty.Register(nameof(ShowBracketOnNegative), typeof(bool), typeof(SKBaseColumn), new PropertyMetadata(false));
        public bool FormatWithAcronym
        {
            get => (bool)GetValue(FormatWithAcronymProperty);
            set => SetValue(FormatWithAcronymProperty, value);
        }
        public static readonly DependencyProperty FormatWithAcronymProperty =
            DependencyProperty.Register(nameof(FormatWithAcronym), typeof(bool), typeof(SKBaseColumn), new PropertyMetadata(false));

        public bool DataVisible
        {
            get => (bool)GetValue(DataVisibleProperty);
            set => SetValue(DataVisibleProperty, value);
        }
        public static readonly DependencyProperty DataVisibleProperty =
            DependencyProperty.Register(nameof(DataVisible), typeof(bool), typeof(SKBaseColumn), new PropertyMetadata(true));

        internal static void TriggerChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            if (s is SKBaseColumn a) a.OnPropertyChanged(e.Property.ToString());
        }

        protected override Freezable CreateInstanceCore() => new SKBaseColumn();
    }

    /// <summary>Collection of SKGridViewColumn. Extends FreezableCollection for DataContext inheritance.</summary>
    public class SkGridColumnCollection : FreezableCollection<SKGridViewColumn>
    {

    }

    /// <summary>Column definition for SkiaGridViewV2 with visibility, sorting, sizing, and display properties.</summary>
    public class SKGridViewColumn : SKBaseColumn
    {
        public string? Header
        {
            get => (string?)GetValue(HeaderProperty);
            set { SetValue(HeaderProperty, value); OnPropertyChanged(); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(SKGridViewColumn), new PropertyMetadata(null));
        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(nameof(IsVisible), typeof(bool?), typeof(SKGridViewColumn), new PropertyMetadata(true, (s, e) => TriggerChanged(s, e)));


        public bool? CanUserResize
        {
            get => (bool?)GetValue(CanUserResizeProperty);
            set => SetValue(CanUserResizeProperty, value);
        }

        public static readonly DependencyProperty CanUserResizeProperty =
            DependencyProperty.Register(nameof(CanUserResize), typeof(bool?), typeof(SKGridViewColumn), new PropertyMetadata(true, (s, e) => TriggerChanged(s, e)));

        public bool? CanUserReorder
        {
            get => (bool?)GetValue(CanUserReorderProperty);
            set => SetValue(CanUserReorderProperty, value);
        }

        public static readonly DependencyProperty CanUserReorderProperty =
            DependencyProperty.Register(nameof(CanUserReorder), typeof(bool?), typeof(SKGridViewColumn), new PropertyMetadata(true, (s, e) => TriggerChanged(s, e)));

        public bool? CanUserSort
        {
            get => (bool?)GetValue(CanUserSortProperty);
            set => SetValue(CanUserSortProperty, value);
        }

        public static readonly DependencyProperty CanUserSortProperty =
            DependencyProperty.Register(nameof(CanUserSort), typeof(bool?), typeof(SKGridViewColumn), new PropertyMetadata(true, (s, e) => TriggerChanged(s, e)));

        public SkGridViewColumnSort GridViewColumnSort
        {
            get => (SkGridViewColumnSort)GetValue(GridViewColumnSortProperty);
            set => SetValue(GridViewColumnSortProperty, value);
        }

        public static readonly DependencyProperty GridViewColumnSortProperty =
            DependencyProperty.Register(nameof(GridViewColumnSort), typeof(SkGridViewColumnSort), typeof(SKGridViewColumn), new PropertyMetadata(SkGridViewColumnSort.None, (s, e) => TriggerChanged(s, e)));

        public string? DisplayHeader
        {
            get => (string?)GetValue(DisplayHeaderProperty);
            set => SetValue(DisplayHeaderProperty, value);
        }

        public static readonly DependencyProperty DisplayHeaderProperty =
            DependencyProperty.Register(nameof(DisplayHeader), typeof(string), typeof(SKGridViewColumn), new PropertyMetadata(null));

        /// <summary>
        /// Name of a data property on the row item whose value is shown as this cell's tooltip.
        /// Default null → no per-cell tooltip. Opt-in and requires the grid's
        /// <c>IsTooltipEnabled</c>. Resolved against each row's data item at hover time,
        /// INDEPENDENT of <c>BindingPath</c> and of text truncation — so an icon / checkbox /
        /// custom-drawn cell (no BindingPath) can still show a tooltip. When null the grid falls
        /// back to the existing truncated-bound-text tooltip. Dot-notation paths are supported.
        /// </summary>
        public string? TooltipPath
        {
            get => (string?)GetValue(TooltipPathProperty);
            set => SetValue(TooltipPathProperty, value);
        }

        public static readonly DependencyProperty TooltipPathProperty =
            DependencyProperty.Register(nameof(TooltipPath), typeof(string), typeof(SKGridViewColumn), new PropertyMetadata(null));

        public double Width
        {
            get => (double)GetValue(WidthProperty);
            set { SetValue(WidthProperty, value); }
        }

        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.Register(nameof(Width), typeof(double), typeof(SKGridViewColumn), new PropertyMetadata(100.0, (s, e) => TriggerChanged(s, e)));



        public string? BackColor
        {
            get => (string?)GetValue(BackColorProperty);
            set => SetValue(BackColorProperty, value);
        }

        public static readonly DependencyProperty BackColorProperty =
            DependencyProperty.Register(nameof(BackColor), typeof(string), typeof(SKGridViewColumn), new PropertyMetadata(null, (s, e) => TriggerChanged(s, e)));

        public int? DisplayIndex
        {
            get => (int?)GetValue(DisplayIndexProperty);
            set => SetValue(DisplayIndexProperty, value);
        }
        public static readonly DependencyProperty DisplayIndexProperty =
            DependencyProperty.Register(nameof(DisplayIndex), typeof(int?), typeof(SKGridViewColumn), new PropertyMetadata(null, (s, e) => TriggerChanged(s, e)));

        public bool IsExpandableColumnForChildRows
        {
            get => (bool)GetValue(IsExpandableColumnForChildRowsProperty);
            set => SetValue(IsExpandableColumnForChildRowsProperty, value);
        }

        public static readonly DependencyProperty IsExpandableColumnForChildRowsProperty =
            DependencyProperty.Register(nameof(IsExpandableColumnForChildRows), typeof(bool), typeof(SKGridViewColumn), new PropertyMetadata(null));

        public bool ShowSubTotalOnSort
        {
            get => (bool)GetValue(ShowSubTotalOnSortProperty);
            set => SetValue(ShowSubTotalOnSortProperty, value);
        }

        public static readonly DependencyProperty ShowSubTotalOnSortProperty =
            DependencyProperty.Register(nameof(ShowSubTotalOnSort), typeof(bool), typeof(SKGridViewColumn), new PropertyMetadata(false));
        public bool ShowGroupAggregateData
        {
            get => (bool)GetValue(ShowGroupAggregateDataProperty);
            set => SetValue(ShowGroupAggregateDataProperty, value);
        }

        public static readonly DependencyProperty ShowGroupAggregateDataProperty =
            DependencyProperty.Register(nameof(ShowGroupAggregateData), typeof(bool), typeof(SKGridViewColumn), new PropertyMetadata(true));

        /// <summary>Icon shown when child rows are expanded (default: "-").</summary>
        public string ExpandIcon
        {
            get => (string)GetValue(ExpandIconProperty);
            set => SetValue(ExpandIconProperty, value);
        }

        public static readonly DependencyProperty ExpandIconProperty =
            DependencyProperty.Register(nameof(ExpandIcon), typeof(string), typeof(SKGridViewColumn), new PropertyMetadata("-"));

        /// <summary>Icon shown when child rows are collapsed (default: "+").</summary>
        public string CollapseIcon
        {
            get => (string)GetValue(CollapseIconProperty);
            set => SetValue(CollapseIconProperty, value);
        }

        public static readonly DependencyProperty CollapseIconProperty =
            DependencyProperty.Register(nameof(CollapseIcon), typeof(string), typeof(SKGridViewColumn), new PropertyMetadata("+"));

        /// <summary>When true, renders a checkbox glyph (☑/☐) bound to a boolean property instead of text.</summary>
        public bool IsCheckboxColumn
        {
            get => (bool)GetValue(IsCheckboxColumnProperty);
            set => SetValue(IsCheckboxColumnProperty, value);
        }

        public static readonly DependencyProperty IsCheckboxColumnProperty =
            DependencyProperty.Register(nameof(IsCheckboxColumn), typeof(bool), typeof(SKGridViewColumn), new PropertyMetadata(false));

        protected override Freezable CreateInstanceCore() => new SKGridViewColumn();
    }

    // Enums moved to Enum/ folder (Step 2 cleanup)
}

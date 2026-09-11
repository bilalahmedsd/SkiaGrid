
using System.Windows;
using System.Windows.Media;

namespace SkiaSharpControlV2
{
    /// <summary>Clickable button rendered inside a grid cell with text, image, colors, and click handler.</summary>
    public class SkButton : Freezable
    {

        public string Name
        {
            get => (string)GetValue(NameProperty);
            set { SetValue(NameProperty, value); }
        }

        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register(nameof(Name), typeof(string), typeof(SkButton), new PropertyMetadata(OnNameChanged));

        private static void OnNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == null)
            {
                throw new InvalidOperationException("SkButton.Name is required.");
            }
        }
        public string? Text
        {
            get => (string?)GetValue(TextProperty);
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(SkButton), new PropertyMetadata(null));

        /// <summary>
        /// Name of a data property on the ROW item whose value is shown as this button's
        /// tooltip. Default null → no button tooltip. Opt-in and requires the grid's
        /// <c>IsTooltipEnabled</c>. SkButton is a single template instance reused across rows,
        /// so the text is resolved against each row's data item at hover time (not a static
        /// string). Dot-notation paths are supported. <see cref="TooltipProvider"/> takes
        /// precedence when both are set.
        /// </summary>
        public string? TooltipPath
        {
            get => (string?)GetValue(TooltipPathProperty);
            set { SetValue(TooltipPathProperty, value); }
        }

        public static readonly DependencyProperty TooltipPathProperty =
            DependencyProperty.Register(nameof(TooltipPath), typeof(string), typeof(SkButton), new PropertyMetadata(null));

        /// <summary>
        /// Callback given the ROW's data item, returning this button's tooltip text. Default
        /// null. Opt-in and requires the grid's <c>IsTooltipEnabled</c>. Takes precedence over
        /// <see cref="TooltipPath"/> when both are set. For code-driven tooltips that don't map
        /// to a single property. Invoked on hover-change only, never per frame.
        /// </summary>
        public Func<object, string?>? TooltipProvider
        {
            get => (Func<object, string?>?)GetValue(TooltipProviderProperty);
            set { SetValue(TooltipProviderProperty, value); }
        }

        public static readonly DependencyProperty TooltipProviderProperty =
            DependencyProperty.Register(nameof(TooltipProvider), typeof(Func<object, string?>), typeof(SkButton), new PropertyMetadata(null));

        public double? Width
        {
            get => (double?)GetValue(WidthProperty);
            set { SetValue(WidthProperty, value); }
        }

        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.Register(nameof(Width), typeof(double?), typeof(SkButton), new PropertyMetadata(null));

        public string? BackgroundColor
        {
            get => (string?)GetValue(BackgroundColorProperty);
            set { SetValue(BackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(nameof(BackgroundColor), typeof(string), typeof(SkButton), new PropertyMetadata(null));

        public string? BorderColor
        {
            get => (string?)GetValue(BorderColorProperty);
            set { SetValue(BorderColorProperty, value); }
        }

        public static readonly DependencyProperty BorderColorProperty =
            DependencyProperty.Register(nameof(BorderColor), typeof(string), typeof(SkButton), new PropertyMetadata(null));

        public string? ForegroundColor
        {
            get => (string?)GetValue(ForegroundColorProperty);
            set { SetValue(ForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.Register(nameof(ForegroundColor), typeof(string), typeof(SkButton), new PropertyMetadata(null));

        /// <summary>
        /// Background color (hex) used while the pointer is over this button. Default null
        /// → no hover effect (button keeps its normal BackgroundColor). Opt-in per button.
        /// </summary>
        public string? HoverBackgroundColor
        {
            get => (string?)GetValue(HoverBackgroundColorProperty);
            set { SetValue(HoverBackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty HoverBackgroundColorProperty =
            DependencyProperty.Register(nameof(HoverBackgroundColor), typeof(string), typeof(SkButton), new PropertyMetadata(null));

        /// <summary>
        /// Text/icon color (hex) used while the pointer is over this button. Default null
        /// → no hover effect (button keeps its normal ForegroundColor). Opt-in per button.
        /// </summary>
        public string? HoverForegroundColor
        {
            get => (string?)GetValue(HoverForegroundColorProperty);
            set { SetValue(HoverForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty HoverForegroundColorProperty =
            DependencyProperty.Register(nameof(HoverForegroundColor), typeof(string), typeof(SkButton), new PropertyMetadata(null));

        public ImageSource? ImageSource
        {
            get => (ImageSource?)GetValue(ImageSourceProperty);
            set { SetValue(ImageSourceProperty, value); }
        }

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(SkButton), new PropertyMetadata(null));

        /// <summary>
        /// Explicit square size (px) for <see cref="ImageSource"/>. Default <c>0</c> = auto: the
        /// largest square that fits the row band (row height minus 1 px of breathing room above
        /// and below) without exceeding the button's <see cref="Width"/>. Set a value only to pin
        /// the glyph smaller than the row allows — the requested size is still clamped to the row
        /// height and the button's slot, because a glyph taller than the row is overpainted by the
        /// next row. Ignored for text buttons.
        /// </summary>
        public double ImageSize
        {
            get => (double)GetValue(ImageSizeProperty);
            set { SetValue(ImageSizeProperty, value); }
        }

        public static readonly DependencyProperty ImageSizeProperty =
            DependencyProperty.Register(nameof(ImageSize), typeof(double), typeof(SkButton), new PropertyMetadata(0.0));

        public double MarginRight
        {
            get => (double)GetValue(MarginRightProperty);
            set { SetValue(MarginRightProperty, value); }
        }

        public static readonly DependencyProperty MarginRightProperty =
            DependencyProperty.Register(nameof(MarginRight), typeof(double), typeof(SkButton), new PropertyMetadata(0.0));


        public double MarginLeft
        {
            get => (double)GetValue(MarginLeftProperty);
            set { SetValue(MarginLeftProperty, value); }
        }

        public static readonly DependencyProperty MarginLeftProperty =
            DependencyProperty.Register(nameof(MarginLeft), typeof(double), typeof(SkButton), new PropertyMetadata(0.0));

        public CellContentAlignment ContentAlignment
        {
            get => (CellContentAlignment)GetValue(ContentAlignmentProperty);
            set => SetValue(ContentAlignmentProperty, value);
        }

        public static readonly DependencyProperty ContentAlignmentProperty =
            DependencyProperty.Register(nameof(ContentAlignment), typeof(CellContentAlignment), typeof(SkButton), new PropertyMetadata(CellContentAlignment.Center));

        /// <summary>
        /// Controls whether this button is drawn. Default <c>true</c>. When <c>false</c>,
        /// the renderer skips both the draw call AND advancing the layout cursor — so the
        /// next visible button in a multi-button row collapses into this button's slot
        /// (no gap). Bind to a row property for data-driven visibility:
        /// <code>&lt;skia:SkButton IsVisible="{Binding IsEditable}" .../&gt;</code>
        /// </summary>
        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(SkButton), new PropertyMetadata(true));

        public Action<SkButton, object> OnClicked
        {
            get { return (Action<SkButton, object>)GetValue(OnClickedProperty); }
            set { SetValue(OnClickedProperty, value); }
        }

        public static readonly DependencyProperty OnClickedProperty =
            DependencyProperty.Register(nameof(OnClicked), typeof(Action<SkButton, object>), typeof(SkButton), new PropertyMetadata(default));

        /// <summary>ICommand alternative to OnClicked. CommandParameter receives the row data item.</summary>
        public System.Windows.Input.ICommand Command
        {
            get => (System.Windows.Input.ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(System.Windows.Input.ICommand), typeof(SkButton), new PropertyMetadata(null));

        /// <summary>Optional parameter for Command. If null, the row data item is used.</summary>
        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(SkButton), new PropertyMetadata(null));

        protected override Freezable CreateInstanceCore() => new SkButton();
    }
}

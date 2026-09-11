using SkiaSharp;
using SkiaSharp.Views.WPF;

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SkiaSharpControlV2.Renderer
{
    /// <summary>
    /// Handles button rendering inside grid cells: background, border, image/text, hit-test registration.
    /// Implements IDisposable to clean up cached image objects (B28 fix).
    /// Extracted from SkiaRenderer (Step 5).
    /// </summary>
    internal class ButtonRenderer : IDisposable
    {
        private readonly Dictionary<ImageSource, SKImage> _imageCache = new();
        private readonly Dictionary<(object, string), (float x, float y, float height, float width, SkButton btn)> _buttonDetails;
        // NOT readonly: SkiaRenderer.UpdateFont() disposes + recreates its fonts (v2.7.5
        // native-crash fix), so these captured references go stale when fonts change at
        // runtime. UpdateFonts() below refreshes them — without it, the next button draw
        // after a font change would measure/draw with a DISPOSED SKFont (native AV).
        private SKFont _symbolFont;
        private SKFont _unicodeFont;
        private readonly SKPaint _defaultForeground;
        private readonly SKPaint _defaultBorder;

        public ButtonRenderer(
            Dictionary<(object, string), (float x, float y, float height, float width, SkButton btn)> buttonDetails,
            SKFont symbolFont,
            SKFont unicodeFont,
            SKPaint defaultForeground,
            SKPaint defaultBorder)
        {
            _buttonDetails = buttonDetails;
            _symbolFont = symbolFont;
            _unicodeFont = unicodeFont;
            _defaultForeground = defaultForeground;
            _defaultBorder = defaultBorder;
        }

        /// <summary>
        /// Re-point at the renderer's current font objects. Called by SkiaRenderer.UpdateFont()
        /// after it disposes + recreates SymbolFont/UniCodeFont so this class never holds
        /// references to disposed fonts.
        /// </summary>
        public void UpdateFonts(SKFont symbolFont, SKFont unicodeFont)
        {
            _symbolFont = symbolFont;
            _unicodeFont = unicodeFont;
        }

        /// <summary>
        /// The button currently under the pointer, as (rowData, buttonName). Set by the
        /// grid on MouseMove. A button whose (data, name) matches this and that defines a
        /// HoverBackgroundColor / HoverForegroundColor draws in its hover color.
        /// </summary>
        public (object? Data, string? Name) HoveredButton { get; set; }

        /// <summary>
        /// Draw a button inside a cell. Registers hit-test coordinates in ButtonDetails dictionary.
        /// </summary>
        public void DrawButton(SKCanvas canvas, SkButton skButton, object data, float currentX, float y, double width, float rowHeight, SKPaint rowBackColor, int columnsIndex, int rowIndex, bool isBorderDrawed)
        {
            if (skButton == null) return;
            if (!skButton.IsVisible) return; // defense-in-depth — CellRenderer already filters in its button loop

            // Update or register hit-test coordinates
            if (_buttonDetails.ContainsKey((data, skButton.Name)))
            {
                var values = _buttonDetails[(data, skButton.Name)];
                values.x = (float)(currentX + skButton.MarginLeft);
                values.y = y;
                values.width = !skButton.Width.HasValue ? 0 : (float)skButton.Width;
                values.height = rowHeight;
                _buttonDetails[(data, skButton.Name)] = values;
            }
            else
            {
                _buttonDetails.Add((data, skButton.Name), (currentX, y, rowHeight, !skButton.Width.HasValue ? 0 : (float)skButton.Width, skButton));
            }

            // Is THIS button (this row's instance) the one under the pointer?
            bool isHovered = HoveredButton.Data != null
                && Equals(HoveredButton.Data, data)
                && HoveredButton.Name == skButton.Name;

            // Hover colors override the normal ones only when hovered AND set (opt-in).
            string? bgColor = (isHovered && skButton.HoverBackgroundColor != null) ? skButton.HoverBackgroundColor : skButton.BackgroundColor;
            string? fgColor = (isHovered && skButton.HoverForegroundColor != null) ? skButton.HoverForegroundColor : skButton.ForegroundColor;

            // Use cached SKPaint instead of creating new per frame (memory leak fix)
            var btnBackColor = bgColor != null ? SKPaintCache.Get(bgColor) : rowBackColor;
            var btnForeColor = fgColor != null ? SKPaintCache.Get(fgColor) : _defaultForeground;
            var btnBorderColor = skButton.BorderColor != null ? SKPaintCache.Get(skButton.BorderColor) : _defaultBorder;

            DrawButtonInternal(
                canvas, columnsIndex, rowIndex,
                skButton.Text ?? "", btnForeColor, _symbolFont,
                btnBackColor, btnBorderColor,
                !skButton.Width.HasValue ? 0 : (float)skButton.Width,
                currentX, y, skButton.ContentAlignment,
                rowHeight, (float)skButton.MarginRight, (float)skButton.MarginLeft,
                isBorderDrawed, skButton, (float)width);
        }

        private void DrawButtonInternal(
            SKCanvas canvas, int columnsIndex, int rowIndex,
            string value, SKPaint fontcolor, SKFont textFont,
            SKPaint backColor, SKPaint? borderColor,
            float width, float x, float y,
            CellContentAlignment cellContentAlignment,
            float rowHeight, float marginRight, float marginLeft,
            bool isBorderDrawed, SkButton? skButton = null, float cellWidth = 0f)
        {
            var rowBackColor = backColor;
            var rowTextColor = fontcolor;
            float contentX = x + marginLeft;
            float contentY = y - 3;
            float contentWidth = width;
            float contentHeight = rowHeight;
            if (isBorderDrawed)
            {
                y += 2;
                rowHeight -= 5;
            }
            // background rect
            CellRenderer.DrawBorder2(canvas, null, 0, 0, 0, 0); // no-op placeholder for rect
            canvas.DrawRect(SKRect.Create(x + marginLeft, (isBorderDrawed ? y : y) + 0.25f, width, rowHeight), rowBackColor);

            // border
            if (borderColor != null)
            {
                CellRenderer.DrawBorder2(canvas, borderColor, width + 1, x + marginLeft, y - 1, rowHeight + 2);
            }

            if (skButton?.ImageSource != null)
            {
                var skImage = GetOrCreateImage(skButton.ImageSource);
                if (skImage != null)
                {
                    // A button configured wider than the cell it sits in would otherwise centre
                    // (or right-align) its glyph past the column edge. Clamp the slot to what is
                    // actually visible; a normally-sized button is unaffected by this Min.
                    float slot = cellWidth > 0f
                        ? Math.Min(contentWidth, Math.Max(0f, cellWidth - marginLeft - marginRight))
                        : contentWidth;
                    // contentY + 3 is the row band's top: contentY was taken as y - 3 before the
                    // isBorderDrawed shrink below moved y.
                    var imageRect = ImageRect(contentX, slot, contentY + 3f, contentHeight,
                                              skButton.ImageSize, cellContentAlignment);
                    if (imageRect.Width > 0f)
                        canvas.DrawImage(skImage, imageRect, ImageSampling);
                }
            }
            else
            {
                // Draw text with unicode font for symbol buttons
                if (width < 10 || string.IsNullOrEmpty(value)) return;
                float textX = contentX + 5;
                if (cellContentAlignment == CellContentAlignment.Right)
                {
                    float tw = _unicodeFont.MeasureText(value, out _);
                    textX = contentX + contentWidth - tw - 5;
                }
                else if (cellContentAlignment == CellContentAlignment.Center)
                {
                    float tw = _unicodeFont.MeasureText(value, out _);
                    textX = contentX + (contentWidth - tw) / 2;
                }
                // contentHeight is the row height captured BEFORE the isBorderDrawed shrink, so the
                // glyph stays centred on the same band the caller reserved. Same centring rule as
                // CellRenderer.TextBaselineY — zero shift at the historic font+4 row height.
                canvas.DrawText(value, textX, CellRenderer.TextBaselineY(contentY, contentHeight, _unicodeFont.Size), _unicodeFont, rowTextColor);
            }
        }

        /// <summary>
        /// Linear + mipmap sampling for image buttons. Icons are authored larger than the row band
        /// they land in (a 40 px decode drawn at 12 px on a 14 px row), and the default nearest-
        /// neighbour path drops pixels on that downscale — thin glyph strokes break up.
        /// </summary>
        private static readonly SKSamplingOptions ImageSampling =
            new(SKFilterMode.Linear, SKMipmapMode.Linear);

        /// <summary>
        /// Destination rect for a button's image: a SQUARE, centred vertically in the row band and
        /// placed horizontally per <paramref name="alignment"/> inside <paramref name="slotWidth"/>.
        /// <para>
        /// Fixed in v2.16.0. The previous math derived <c>size = Min(bandHeight - 4, slotWidth - 2)</c>
        /// and then drew it into a rect whose width was <c>size - 4</c> but whose height was
        /// <c>size - 1</c> — non-square, and at the 14 px Compact row height that is a 6x9 px glyph
        /// out of a 10 px budget. It also ignored <paramref name="alignment"/> entirely, pinning
        /// every image to the left edge of its slot.
        /// </para>
        /// <paramref name="requestedSize"/> of 0 = auto (fill the band less 1 px above and below,
        /// capped at the slot). A requested size is honoured but still clamped to the band and the
        /// slot — a glyph taller than the row is overpainted by the next row.
        /// </summary>
        internal static SKRect ImageRect(
            float slotX, float slotWidth,
            float bandTop, float bandHeight,
            double requestedSize,
            CellContentAlignment alignment)
        {
            float ceiling = Math.Min(bandHeight, slotWidth);
            float size = requestedSize > 0
                ? Math.Min((float)requestedSize, ceiling)
                : Math.Min(bandHeight - 2f, slotWidth);

            if (size <= 0f) return SKRect.Empty;

            float x = alignment switch
            {
                CellContentAlignment.Center => slotX + (slotWidth - size) / 2f,
                CellContentAlignment.Right => slotX + slotWidth - size,
                _ => slotX,
            };
            return SKRect.Create(x, bandTop + (bandHeight - size) / 2f, size, size);
        }

        /// <summary>
        /// Cached <see cref="SKImage"/> for a WPF <see cref="ImageSource"/>. Caches the SKImage
        /// rather than the SKBitmap because only the DrawImage overloads take
        /// <see cref="SKSamplingOptions"/>, and image buttons are always drawn scaled.
        /// </summary>
        private SKImage? GetOrCreateImage(ImageSource? source)
        {
            if (source == null) return null;

            if (_imageCache.TryGetValue(source, out var cached))
                return cached;

            if (source is BitmapSource bitmapSource)
            {
                using var bmp = bitmapSource.ToSKBitmap();
                var img = SKImage.FromBitmap(bmp);
                _imageCache[source] = img;
                return img;
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var bmp in _imageCache.Values)
                bmp?.Dispose();
            _imageCache.Clear();
        }
    }
}

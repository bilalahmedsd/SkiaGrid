using SkiaSharp;

using System.Collections;

namespace SkiaSharpControlV2.Renderer
{
    /// <summary>
    /// Handles cell-level rendering: background rect, border, text measurement/truncation/alignment,
    /// selection highlighting. Extracted from SkiaRenderer (Step 5).
    /// </summary>
    internal class CellRenderer
    {
        private readonly bool _showGridLines;

        public bool ShowGridLines
        {
            set => _showGridLinesInternal = value;
        }
        private bool _showGridLinesInternal;

        public CellRenderer()
        {
            _showGridLinesInternal = false;
        }

        // ── Cell Draw (orchestrates background, border, buttons, text) ──

        public void DrawCell(
            SKCanvas canvas, int columnsIndex, int rowIndex,
            string value, SKPaint fontcolor, SKFont textFont,
            SKPaint backColor, SKPaint? borderColor,
            float width, float x, float y,
            CellContentAlignment cellContentAlignment, float rowHeight,
            bool isSelectedRow, bool isWindowActive,
            SKPaint selectedRowBg, SKPaint selectedRowText,
            SKCellTemplate? cellTemplate, object? data,
            ButtonRenderer buttonRenderer,
            SKSelectionStyle selectionStyle = SKSelectionStyle.Fill)
        {
            SkiaSharpControlV2.Diagnostics.GridMetrics.Increment(SkiaSharpControlV2.Diagnostics.GridMetrics.CounterCellsDrawn);
            // NOTE: Per-cell timing removed — stopwatch overhead would tank perf (1000s of calls per frame).
            // See CounterCellsDrawn for per-cell count. Use Render.Draw for aggregate timing.

            bool isSelectedFill = isSelectedRow && isWindowActive && selectionStyle == SKSelectionStyle.Fill;
            var rowBackColor = isSelectedFill ? selectedRowBg : backColor;
            // On selection the text normally switches to the selected-row text color for
            // contrast against the fill. Exception: a fully-transparent cell foreground means
            // "hide this text" — honor it even when the row is selected instead of forcing
            // it visible (white). Any other foreground still uses the selected-row color.
            bool foregroundTransparent = fontcolor != null && fontcolor.Color.Alpha == 0;
            var rowTextColor = (isSelectedFill && !foregroundTransparent) ? selectedRowText : fontcolor;

            var textX = x;
            var textTotalX = 0.0f;
            var textY = y;

            DrawRect(canvas, rowIndex, x, y, rowBackColor, width, rowHeight);

            if (borderColor != null && !isSelectedFill)
            {
                DrawBorder2(canvas, borderColor, width, x, y, rowHeight);
            }

            // Custom draw delegate — when set, takes over the cell's content area.
            // Background is already drawn above. Canvas is hard-clipped to the cell
            // rect so the delegate cannot bleed into neighbouring cells. We swallow
            // exceptions per-cell so a misbehaving drawer doesn't kill the paint loop.
            if (cellTemplate?.CustomDraw is { } customDraw)
            {
                var cellRect = new SKRect(x, y, x + width, y + rowHeight);
                int savePoint = canvas.Save();
                try
                {
                    canvas.ClipRect(cellRect);
                    var ctx = new SkCellDrawContext
                    {
                        Canvas      = canvas,
                        Data        = data,
                        Bounds      = cellRect,
                        RowIndex    = rowIndex,
                        ColumnIndex = columnsIndex,
                        Background  = rowBackColor,
                        Foreground  = rowTextColor,
                        Font        = textFont,
                    };
                    try { customDraw(ctx); }
                    catch (Exception ex)
                    {
                        SkiaSharpControlV2.Diagnostics.GridLogger.LogError(
                            SkiaSharpControlV2.Diagnostics.GridLogger.Render,
                            $"CustomDraw threw for cell ({rowIndex},{columnsIndex})", ex);
                    }
                }
                finally
                {
                    canvas.RestoreToCount(savePoint);
                }
                return; // skip default text + button rendering
            }

            // Draw buttons if cell template has them
            if (cellTemplate != null)
            {
                var dynamicButtons = cellTemplate.DrawButton?.Invoke(data);
                if (dynamicButtons != null && dynamicButtons.Count > 0)
                {
                    foreach (var item in dynamicButtons)
                    {
                        if (!item.IsVisible) continue; // hidden — skip draw AND cursor advance (collapse next button into this slot)
                        buttonRenderer.DrawButton(canvas, item, data!, textX, y, width, rowHeight, rowBackColor, columnsIndex, rowIndex, borderColor != null && !(isSelectedRow && isWindowActive));
                        textX += (item.Width.HasValue ? (float)(item.Width + item.MarginLeft + item.MarginRight) : 0);
                        textTotalX += (item.Width.HasValue ? (float)(item.Width + item.MarginLeft + item.MarginRight) : 0);
                    }
                }
                else
                {
                    if (cellTemplate.SkButton != null && cellTemplate.SkButtons.Count == 0)
                    {
                        if (cellTemplate.SkButton.IsVisible)
                        {
                            buttonRenderer.DrawButton(canvas, cellTemplate.SkButton, data!, textX, y, width, rowHeight, rowBackColor, columnsIndex, rowIndex, borderColor != null && !(isSelectedRow && isWindowActive));
                            textX += (cellTemplate.SkButton.Width.HasValue ? (float)cellTemplate.SkButton.Width : width) + (float)cellTemplate.SkButton.MarginLeft + (float)cellTemplate.SkButton.MarginRight;
                            textTotalX += (cellTemplate.SkButton.Width.HasValue ? (float)cellTemplate.SkButton.Width : width) + (float)cellTemplate.SkButton.MarginLeft + (float)cellTemplate.SkButton.MarginRight;
                        }
                    }
                    else if (cellTemplate.SkButtons != null)
                    {
                        foreach (var item in cellTemplate.SkButtons)
                        {
                            if (!item.IsVisible) continue; // hidden — collapse next visible button into this slot
                            buttonRenderer.DrawButton(canvas, item, data!, textX, y + 0.5f, width, rowHeight - 2, rowBackColor, columnsIndex, rowIndex, borderColor != null && !(isSelectedRow && isWindowActive));
                            textX += (item.Width.HasValue ? (float)(item.Width + item.MarginLeft + item.MarginRight) : 0);
                            textTotalX += (item.Width.HasValue ? (float)(item.Width + item.MarginLeft + item.MarginRight) : 0);
                        }
                    }
                }
            }
            DrawText(canvas, columnsIndex, rowIndex, value, rowTextColor, textFont, width - textTotalX, textX, textY, cellContentAlignment, rowHeight);
        }

        // ── Vertical text placement ─────────────────────────────────────

        /// <summary>
        /// Baseline Y for cell text. The historic baseline was <c>y + fontSize</c>, which assumed
        /// the row height was always <c>fontSize + 4</c>. Now that the row height is a separate,
        /// consumer-settable value (<c>SKRowHeight</c>) any surplus/deficit is split evenly above
        /// and below the glyphs, so text stays vertically centred in tall AND short rows. When the
        /// row height IS <c>fontSize + 4</c> the offset is zero and the baseline is byte-identical
        /// to the pre-v2.14.0 behavior. <paramref name="rowHeight"/> of 0 means "unknown" and also
        /// falls back to the legacy baseline.
        /// </summary>
        internal static float TextBaselineY(float y, float rowHeight, float fontSize)
            => rowHeight <= 0f
                ? y + fontSize
                : y + fontSize + (rowHeight - fontSize - 4f) / 2f;

        // ── Checkbox Rendering ──────────────────────────────────────────

        /// <summary>
        /// Draw a checkbox glyph (☑/☐) centered in the cell. Returns the checkbox bounds for hit-testing.
        /// </summary>
        public (float x, float y, float w, float h) DrawCheckbox(
            SKCanvas canvas, int rowIndex, string value,
            SKPaint fontcolor, SKFont textFont,
            SKPaint backColor, SKPaint? borderColor,
            float width, float x, float y, float rowHeight,
            bool isSelectedRow, bool isWindowActive,
            SKPaint selectedRowBg, SKPaint selectedRowText,
            SKSelectionStyle selectionStyle)
        {
            bool isSelectedFill = isSelectedRow && isWindowActive && selectionStyle == SKSelectionStyle.Fill;
            var rowBackColor = isSelectedFill ? selectedRowBg : backColor;
            // Honor a fully-transparent foreground even when selected (see DrawCell).
            bool foregroundTransparent = fontcolor != null && fontcolor.Color.Alpha == 0;
            var rowTextColor = (isSelectedFill && !foregroundTransparent) ? selectedRowText : fontcolor;

            DrawRect(canvas, rowIndex, x, y, rowBackColor, width, rowHeight);

            if (borderColor != null && !isSelectedFill)
                DrawBorder2(canvas, borderColor, width, x, y, rowHeight);

            bool isChecked = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
            string glyph = isChecked ? "☑" : "☐";

            // Center the glyph
            float glyphWidth = textFont.MeasureText(glyph, out _);
            float cx = x + (width - glyphWidth) / 2;
            float cy = y + rowHeight / 2 + textFont.Size / 3;

            canvas.DrawText(glyph, cx, cy, textFont, rowTextColor);

            // Return checkbox bounds for hit-testing
            float boxSize = Math.Min(rowHeight - 4, width - 4);
            float bx = x + (width - boxSize) / 2;
            float by = y + (rowHeight - boxSize) / 2;
            return (bx, by, boxSize, boxSize);
        }

        // ── Text Rendering with ellipsis truncation ─────────────────────

        public void DrawText(SKCanvas canvas, int columnsIndex, int rowIndex, string value, SKPaint fontColor, SKFont textFont, float width, float x, float y, CellContentAlignment cellContentAlignment, float rowHeight = 0f)
        {
            if (width < 10 || string.IsNullOrEmpty(value))
                return;

            float maxTextWidth = width - 10;
            ReadOnlySpan<char> span = value;
            ReadOnlySpan<char> ellipsis = "...";
            float ellipsisWidth = textFont.MeasureText(ellipsis, out _);

            int left = 0;
            int right = span.Length;
            int fitLength = span.Length;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var testSpan = span.Slice(0, mid);
                float testWidth = textFont.MeasureText(testSpan, out _);

                if (testWidth + (mid < span.Length ? ellipsisWidth : 0) <= maxTextWidth)
                {
                    fitLength = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            string finalText = span.Slice(0, fitLength).ToString();
            bool wasTrimmed = fitLength < span.Length;
            if (wasTrimmed)
                finalText += "...";

            float finalWidth = textFont.MeasureText(finalText, out _);

            float textX = x + 5;
            if (cellContentAlignment == CellContentAlignment.Right)
                textX = x + width - finalWidth - 5;
            else if (cellContentAlignment == CellContentAlignment.Center)
                textX = x + (width - finalWidth) / 2;

            canvas.DrawText(finalText, textX, TextBaselineY(y, rowHeight, textFont.Size), textFont, fontColor);
        }

        // ── Rectangle Drawing ───────────────────────────────────────────

        public void DrawRect(SKCanvas canvas, int rowIndex, float x, float y, SKPaint backColor, float width, float rowHeight)
        {
            float left = x; // ShowGridLines ternary was dead code (both branches identical)
            float top = _showGridLinesInternal ? y : y - 1;
            float right = x + width;
            float bottom = y + rowHeight;

            canvas.DrawRect(SKRect.Create(left, top + 0.25f, right - left, bottom - top), backColor);
        }

        // ── Border Drawing ──────────────────────────────────────────────

        /// <summary>Row-level border (used for RowTemplate BorderColor)</summary>
        public static void DrawBorder(SKCanvas canvas, SKPaint? borderColor, float width, float x, float y, float rowHeight)
        {
            if (borderColor == null) return;
            canvas.DrawLine(x + 1f, y, x + 1f, y + rowHeight, borderColor);
            canvas.DrawLine(x + width - 1f, y, x + width - 1f, y + rowHeight, borderColor);
            canvas.DrawLine(x, y + rowHeight - 2f, x + width, y + rowHeight - 2f, borderColor); // bottom
            canvas.DrawLine(x, y + 0.5f, x + width, y + 0.5f, borderColor); // top
        }

        /// <summary>Cell-level border (used for cell/button borders)</summary>
        public static void DrawBorder2(SKCanvas canvas, SKPaint? borderColor, float width, float x, float y, float rowHeight)
        {
            if (borderColor == null) return;
            canvas.DrawLine(x + 1f, y + 1f, x + 1f, y + rowHeight - 1f, borderColor); // left
            canvas.DrawLine(x + width - 2f, y + 1f, x + width - 2f, y + rowHeight - 2f, borderColor); // right
            canvas.DrawLine(x + 1f, y + rowHeight - 2f, x + width - 2f, y + rowHeight - 2f, borderColor); // bottom
            canvas.DrawLine(x + 1f, y + 1f, x + width - 2f, y + 1f, borderColor); // top
        }

        // ── Selection Highlighting ──────────────────────────────────────

        // Per-instance frame selection set. MUST be instance-level (not static):
        // when multiple SkiaGridViewV2 instances exist (e.g., several windows), each
        // SkiaRenderer has its own CellRenderer. Static state caused row-selection
        // flicker because BeginFrame() in one window overwrote the set used by
        // another window's still-in-progress paint.
        private HashSet<object>? _frameSelectionSet;

        /// <summary>
        /// Call once at the start of each paint frame to rebuild the selection HashSet.
        /// Much cheaper than rebuilding per-cell, and always reflects current SelectedItems state.
        /// </summary>
        public void BeginFrame(IEnumerable? selectedItems)
        {
            // Reuse the existing HashSet instead of allocating a new one each frame
            if (_frameSelectionSet == null)
                _frameSelectionSet = new HashSet<object>();
            else
                _frameSelectionSet.Clear();

            if (selectedItems != null)
            {
                foreach (var sel in selectedItems)
                    if (sel != null) _frameSelectionSet.Add(sel);
            }
        }

        public bool HighlightSelected(object? item, IEnumerable? selectedItems)
        {
            if (item == null || _frameSelectionSet == null) return false;
            return _frameSelectionSet.Contains(item);
        }

        /// <summary>Invalidate selection cache (call on Dispose).</summary>
        public void InvalidateSelectionCache()
        {
            _frameSelectionSet = null;
        }
    }
}


using SkiaSharp;
using SkiaSharp.Views.WPF;

using SkiaSharpControlV2.Diagnostics;

using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace SkiaSharpControlV2.Renderer
{
    public class SkiaRenderer : IDisposable
    {
        internal readonly SetterResolver _setterResolver = new();
        internal readonly TriggerEvaluator _triggerEvaluator;
        internal readonly CellRenderer _cellRenderer = new();
        internal ButtonRenderer? _buttonRenderer;
        internal GroupRowRenderer? _groupRowRenderer;

        // Refcount of live SkiaRenderer instances. Process-wide caches
        // (SKPaintCache, SKTriggerStateCache) must only be cleared when the
        // LAST renderer is disposed — otherwise closing one window invalidates
        // SKPaint references still held by other live grids, causing flicker
        // and broken visuals.
        private static int _activeInstanceCount;

        // Set once in Dispose(). Guards every native-handle access (font setup, Draw)
        // so a post-dispose re-entry — e.g. the consumer's Loaded handler firing again
        // after SkiaGridViewV2.Dispose() during a tab-switch / window re-parent — cannot
        // touch a freed SKFont/SKPaint and crash the process with a native AccessViolation.
        private volatile bool _disposed;

        /// <summary>
        /// Render context built by SkiaGridViewV2 before each paint cycle.
        /// Replaces the old CurrentContext back-reference (Step 14).
        /// </summary>
        internal GridRenderContext? RenderContext { get; set; }

        public SkiaRenderer(ReflectionHelper reflectionHelper)
        {
            this.reflectionHelper = reflectionHelper;
            _triggerEvaluator = new TriggerEvaluator(_setterResolver);
            _groupRowRenderer = new GroupRowRenderer(this);
            System.Threading.Interlocked.Increment(ref _activeInstanceCount);
        }

        /// <summary>
        /// Initialize ButtonRenderer with the context's ButtonDetails.
        /// Called once after first RenderContext is set.
        /// </summary>
        internal void InitButtonRenderer()
        {
            if (RenderContext != null && _buttonRenderer == null)
                _buttonRenderer = new ButtonRenderer(RenderContext.ButtonDetails, SymbolFont, UniCodeFont, ButtonForegroundcolor, ButtonBordercolor);
        }

        /// <summary>
        /// Public entry point for GroupRowRenderer to call cell-level Draw.
        /// </summary>
        internal void DrawCellPublic(SKCanvas canvas, int colIdx, int rowIdx, string value, SKPaint fg, SKFont font, SKPaint bg, SKPaint? border, float w, float x, float y, CellContentAlignment align, float rh, bool isSelected, SKCellTemplate? template = null, object? data = null)
            => Draw(canvas, colIdx, rowIdx, value, fg, font, bg, border, w, x, y, align, rh, isSelected, template, data);

        public ReflectionHelper reflectionHelper;
        private SKFont SymbolFont { get; set; } = new() { Size = 11, Typeface = SKTypeface.FromFamilyName("Microsoft Sans Serif") };
        private SKFont UniCodeFont { get; set; } = new() { Size = 11, Typeface = SKTypeface.FromFamilyName("Segoe UI Symbol") };
        private string FontFamily { get; set; } = "Microsoft Sans Serif";
        private string FontStyle { get; set; } = "Normal";
        private float FontSize { get; set; } = 11;
        public List<RowModel>? Items { get; set; } = new();
        private IEnumerable? SelectedItems { get; set; }
        private IEnumerable<SKGridViewColumn>? Columns { get; set; } = [];
        private SKGroupDefinition? Group { get; set; }
        private ScrollBar? HorizontalScrollViewer { get; set; }
        private ScrollBar? VerticalScrollViewer { get; set; }
        private bool ShowGridLines { get; set; }

        private bool IsWindowActive = true;
        internal SKSelectionStyle SelectionStyle { get; set; } = SKSelectionStyle.Fill;

        private readonly List<SKGridViewColumn> _visibleColumnsCache = new();
        private SKPaint SelectedRowBackgroundHighlighting = new SKPaint() { Color = SKColor.Parse("#0072C6"), IsAntialias = true };
        private SKPaint SelectedRowTextColor = new SKPaint { Color = SKColors.White, StrokeWidth = 1, IsAntialias = true };
        private SKPaint GridLineColor = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, IsAntialias = true };
        private SKPaint FontColor = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, IsAntialias = true };
        private SKPaint RowBackgroundColor = new SKPaint { Color = SKColors.White, StrokeWidth = 1, IsAntialias = true };
        private SKPaint? GroupRowBackgroundColor = null;
        private SKPaint? GroupFontColor = null;
        private SKPaint AlternatingRowBackground = null;

        private SKPaint ButtonBackgroundcolor = new SKPaint { Color = SKColors.Transparent, StrokeWidth = 1, IsAntialias = true };
        private readonly SKPaint ButtonForegroundcolor = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, IsAntialias = true };
        private readonly SKPaint ButtonBordercolor = new SKPaint { Color = SKColors.Transparent, StrokeWidth = 1, IsAntialias = true };


        private SKPaint CellBackgroundColor = new SKPaint { Color = SKColors.White, StrokeWidth = 1, IsAntialias = true };
        private SKPaint CellBorderColor = new SKPaint { Color = SKColors.Green, StrokeWidth = 1, IsAntialias = true };
        public List<GroupModel> GroupItemSource { get; set; } = new();

        // ── Row drag visuals (2.18.0) ───────────────────────────────────────────────
        // Written by RowDragController via the control, only while a drag is live. The whole
        // overlay pass is gated on RowDragInsertIndex < 0, so a grid that is not being dragged
        // (i.e. every frame of every grid that never opted in) costs one int comparison.
        internal int RowDragInsertIndex { get; set; } = -1;
        internal List<int>? RowDragRowIndexes { get; set; }
        internal string RowDragIndicatorColorHex { get; set; } = DefaultRowDragIndicatorColor;
        internal const string DefaultRowDragIndicatorColor = "#3399FF";
        private const float RowDragIndicatorThickness = 2f;

        public void SetWindowActive(bool isWindowActive)
        {
            IsWindowActive = isWindowActive;
        }
        public void UpdateSelectedItems(IEnumerable selectedItems)
        {
            SelectedItems = selectedItems;
        }
        public void SetScrollBars(ScrollBar horizontalScrollViewer, ScrollBar verticalScrollViewer)
        {
            HorizontalScrollViewer = horizontalScrollViewer;
            VerticalScrollViewer = verticalScrollViewer;
        }
        public void SetColumns(IEnumerable<SKGridViewColumn> columns)
        {
            Columns = columns;
        }
        public void SetGroup(SKGroupDefinition? group)
        {
            Group = group;
        }
        public void SetFontSize(float size)
        {
            if (_disposed) return;
            FontSize = size;
            UpdateFont();
        }

        public void SetFontFamily(string fontFamily)
        {
            if (_disposed) return;
            FontFamily = fontFamily;
            UpdateFont();
        }
        public void SetFontStyle(string fontStyle)
        {
            if (_disposed) return;
            FontStyle = fontStyle;
            UpdateFont();
        }
        private void UpdateFont()
        {
            if (_disposed) return;
            if (!Helper.IsFontInstalled(FontFamily))
                throw new ArgumentException($" \"{FontFamily}\" font not installed");

            // Recreate BOTH fonts (do NOT mutate in place). Previously UniCodeFont.Size was
            // mutated directly — if its native handle had been freed (renderer Disposed and
            // then re-entered via a repeated Loaded, or a cross-thread Skia race), the
            // assignment crashed the process with a native AccessViolation in sk_font_set_size
            // (a Corrupted State Exception — uncatchable, process-fatal, Event 1026). Assigning
            // fresh SKFont objects guarantees a valid native handle every time. Disposing the
            // old instances also fixes the prior leak (SymbolFont used to be reassigned without
            // disposing the previous one).
            var oldSymbol = SymbolFont;
            var oldUnicode = UniCodeFont;
            SymbolFont = SkFontFactory.CreateSkFont(FontFamily, FontStyle, FontSize);
            UniCodeFont = new SKFont { Size = FontSize, Typeface = SKTypeface.FromFamilyName("Segoe UI Symbol") };

            // ButtonRenderer captured the old font references at construction — re-point it
            // at the new instances BEFORE disposing the old ones, or its next button draw
            // would measure/draw with a disposed SKFont (native AV, process-fatal).
            _buttonRenderer?.UpdateFonts(SymbolFont, UniCodeFont);

            oldSymbol?.Dispose();
            oldUnicode?.Dispose();
        }

        public void SetGridLinesVisibility(bool showGridLines)
        {
            ShowGridLines = showGridLines;
            _cellRenderer.ShowGridLines = showGridLines;
        }
        // _disposed guards on every color setter below: after Dispose() frees the SKPaint
        // fields, mutating them (FontColor.Color = ...) writes through a freed native
        // handle → AccessViolation in sk_paint_set_color — uncatchable in .NET 8,
        // process-fatal. This is the sibling of the v2.7.5 font-path fix: WPF re-fires
        // Loaded on visual-tree re-attach (observed on sleep/resume in multi-window apps),
        // and the Loaded handler re-runs the whole appearance chain on the disposed
        // renderer. Fonts were guarded in 2.7.5; the chain then died at the first
        // unguarded color setter (QA crash: SetForeground → sk_paint_set_color).

        public void SetGridLinesColor(string color)
        {
            if (_disposed) return;
            if (!string.IsNullOrEmpty(color))
                GridLineColor.Color = SKColor.Parse(color);
        }
        public void SetGroupRowBackgroundColor(string? color)
        {
            if (_disposed) return;
            GroupRowBackgroundColor?.Dispose(); // Dispose old before replacement
            GroupRowBackgroundColor = color != null ? new SKPaint { Color = SKColor.Parse(color), StrokeWidth = 1, IsAntialias = true } : null;
        }
        public void SetGroupFontColor(string? color)
        {
            if (_disposed) return;
            GroupFontColor?.Dispose(); // Dispose old before replacement
            GroupFontColor = color != null ? new SKPaint { Color = SKColor.Parse(color), StrokeWidth = 1, IsAntialias = true } : null;
        }
        public void SetForeground(string color)
        {
            if (_disposed) return;
            if (!string.IsNullOrEmpty(color))
                FontColor.Color = SKColor.Parse(color);
        }
        public void SetRowBackgroundColor(string color)
        {
            if (_disposed) return;
            if (!string.IsNullOrEmpty(color))
                RowBackgroundColor.Color = SKColor.Parse(color);
        }
        public void SetAlternatingRowBackground(string? color)
        {
            if (_disposed) return;
            AlternatingRowBackground?.Dispose(); // Dispose old before replacement
            AlternatingRowBackground = color != null ? new SKPaint { Color = SKColor.Parse(color), StrokeWidth = 1, IsAntialias = true } : null;
        }
        public void SetSelectedRowBackground(string? color)
        {
            if (_disposed) return;
            if (!string.IsNullOrEmpty(color))
                SelectedRowBackgroundHighlighting.Color = SKColor.Parse(color);
        }
        public void SetSelectedRowForeground(string? color)
        {
            if (_disposed) return;
            if (!string.IsNullOrEmpty(color))
                SelectedRowTextColor.Color = SKColor.Parse(color);
        }
        public void Draw(SKCanvas canvas, float scrollOffsetX, float scrollOffsetY, float rowHeight, int totalRows)
        {
            // A paint can still be queued (PaintSurface) after the renderer is disposed —
            // e.g. an InvalidateVisual in flight when the window tears down. Drawing would
            // touch freed SKFont/SKPaint handles → native crash. Bail out cleanly.
            if (_disposed) return;
            using var _metrics = GridMetrics.Measure(GridMetrics.RenderDraw);
            try
            {
                // Per-frame setup: cache DateTime.Now, cleanup expired timer entries
                SKTriggerStateCache.BeginFrame();
                SKTriggerStateCache.CleanupExpired();

                // Rebuild selection HashSet once per frame for O(1) highlight checks.
                // Per-instance call — multiple grids in different windows do NOT share state.
                _cellRenderer.BeginFrame(SelectedItems);

                // Reset per-frame hit-test dictionaries so they only describe what's
                // currently rendered. Entries are repopulated below for each visible cell
                // that draws a checkbox or button. Prevents:
                //   1. CORRECTNESS — stale entries from deleted rows surviving and being
                //      matched by the hit-test in MouseHandler (the checkbox-on-deleted-row bug).
                //   2. MEMORY — unbounded growth of the dictionaries over long sessions.
                RenderContext?.CheckboxDetails?.Clear();
                RenderContext?.ButtonDetails?.Clear();

                int firstVisibleRow = Math.Max(0, (int)(scrollOffsetY / rowHeight));

                int firstVisibleCol = 0;
                int visibleRowCount = Math.Min((int?)(VerticalScrollViewer?.ViewportSize / rowHeight) ?? 0, totalRows - firstVisibleRow);
                int visibleColCount = 0;

                double columnSum = scrollOffsetX;
                int columnCounter = 0;

                var visibleColumns = _visibleColumnsCache;//(?.Where(x => x.Width > 0) ?? []).ToList();

                // Find the first column whose right edge is strictly inside the viewport.
                // Use `< 0` (not `<= 0`): a column whose right edge sits exactly at the
                // viewport's left edge has zero pixels visible — skip it.
                foreach (var item in visibleColumns)
                {
                    columnSum -= item.Width;
                    if (columnSum < 0)
                    {
                        firstVisibleCol = columnCounter;
                        break;
                    }
                    columnCounter++;
                }

                // Amount of firstVisibleCol scrolled off the LEFT edge (in panel pixels).
                // After the loop columnSum = scrollOffsetX - cumulativeRight[firstVisibleCol].
                // Adding the column's own width yields scrollOffsetX - cumulativeLeft[firstVisibleCol] = partial offset.
                double partialOffset = 0;
                if (firstVisibleCol < visibleColumns.Count)
                    partialOffset = columnSum + visibleColumns[firstVisibleCol].Width;

                // Sum widths until we cover the viewport. Must extend the target by
                // `partialOffset` because the first column's leading pixels are off-screen,
                // so its full width is NOT available viewport coverage. Without this the
                // last partially-visible column gets dropped.
                double viewportTarget = (HorizontalScrollViewer?.ViewportSize ?? 0) + partialOffset;
                columnSum = 0;

                for (int i = firstVisibleCol; i < visibleColumns.Count; i++)
                {
                    columnSum += visibleColumns[i].Width;
                    visibleColCount = i + 1;
                    if (columnSum >= viewportTarget)
                        break;
                }
                if ((firstVisibleRow + visibleRowCount) > totalRows)
                    return;

                float currentY = firstVisibleRow * rowHeight;
                List<GroupModel>? GroupItems = null;

                IEnumerator<RowModel> items = Items!.Skip(firstVisibleRow).Take(visibleRowCount).GetEnumerator();
                items?.MoveNext();
                if (Group != null)
                {
                    GroupItems = GroupItemSource.Where(x => x.IsGroupHeader || x.IsExpanded).ToList();
                }

                for (int row = firstVisibleRow; row < firstVisibleRow + visibleRowCount; row++)
                {

                    var item = items?.Current;
                    float currentX = 0;
                    float currentX1 = 0;

                    var columnList = visibleColumns;

                    for (int i = 0; i < firstVisibleCol && i < columnList?.Count; i++)
                    {
                        currentX += (float)columnList[i].Width;
                    }
                    currentX1 += currentX;

                    // Row-constant values hoisted out of the per-column loop — computed ONCE per row
                    // instead of once per visible column (~20x redundant with many columns).
                    // rowcolor is needed by both the group and non-group branches. The RowTemplate
                    // cascade (Steps 1-2) is row-level and only applies to the non-group branch, so it
                    // is guarded by GroupItems == null — matching the prior behavior where these ran
                    // only inside the else-branch below.
                    var rowcolor = row % 2 == 0 ? RowBackgroundColor : AlternatingRowBackground ?? RowBackgroundColor;
                    SKPaint rowResolvedBg = default!;
                    SKPaint rowResolvedFg = default!;
                    SKPaint? rowBorderColor = null;
                    if (GroupItems == null)
                    {
                        // Step 1: RowTemplate.Setters
                        var defaultRowtemplate = GetSetterValues(reflectionHelper, RenderContext?.RowTemplate?.Setters, item?.Item);
                        rowResolvedBg = defaultRowtemplate.BackgroundColor ?? rowcolor;
                        rowResolvedFg = defaultRowtemplate.Foregroundcolor ?? FontColor;

                        // Step 2: RowTemplate.Triggers (first-match-wins)
                        var defaultrowtriggerTemplate = GetTriggerTemplate(item?.Item, reflectionHelper, RenderContext?.RowTemplate?.Triggers);
                        rowResolvedBg = defaultrowtriggerTemplate.BackgroundColor ?? rowResolvedBg;
                        rowResolvedFg = defaultrowtriggerTemplate.Foregroundcolor ?? rowResolvedFg;

                        // Row-level border: only from RowTemplate (spans entire row, not per-cell)
                        rowBorderColor = defaultrowtriggerTemplate.BorderColor ?? defaultRowtemplate.BorderColor;
                    }

                    for (int colIndex = firstVisibleCol; colIndex < visibleColCount; colIndex++)
                    {
                        float GVColumnWidth = (float)visibleColumns[colIndex].Width;
                        if (GroupItems != null)
                        {
                            // B15 fix: bounds check on GroupItems[row]
                            if (row >= GroupItems.Count) continue;
                            // Delegates to GroupRowRenderer (extracted in Step 6)
                            _groupRowRenderer!.RenderGroupCell(
                                canvas!, row, colIndex, GVColumnWidth, currentX, currentY, rowHeight,
                                GroupItems, GroupItemSource, visibleColumns, Group, rowcolor,
                                reflectionHelper, FontColor, GroupFontColor, GroupRowBackgroundColor,
                                SymbolFont, UniCodeFont, FontSize, SelectedItems,
                                RenderContext?.RowTemplate, RenderContext?.CellTemplate,
                                RenderContext!.GroupToggleDetails);
                            
                        }
                        else
                        {
                            var CurrentColumns = visibleColumns[colIndex];
                            var value = reflectionHelper.ReadCurrentItemWithTypes(item?.Item, CurrentColumns.BindingPath);
                            var val = Helper.ApplyFormat(value.Type, value.Value, CurrentColumns.Format, CurrentColumns.ShowBracketOnNegative, CurrentColumns.FormatWithAcronym);

                            // 6-step style cascade: RowTemplate → CellTemplate → Column.CellTemplate
                            // Steps 1-2 (RowTemplate setters + triggers) are row-level and were hoisted
                            // above the per-column loop (rowResolvedBg / rowResolvedFg / rowBorderColor).
                            SKPaint BackgroundColor = rowResolvedBg;
                            SKPaint Foregroundcolor = rowResolvedFg;

                            // Cell-level border starts fresh — RowTemplate border should NOT cascade into cells
                            SKPaint BorderColor = null;

                            // Steps 3-6: CellTemplate and Column.CellTemplate (cell-level border only)
                            var defaultcelltemplate = GetSetterValues(reflectionHelper, RenderContext?.CellTemplate?.Setters, item?.Item);
                            BackgroundColor = defaultcelltemplate.BackgroundColor ?? BackgroundColor;
                            Foregroundcolor = defaultcelltemplate.Foregroundcolor ?? Foregroundcolor;
                            BorderColor = defaultcelltemplate.BorderColor ?? BorderColor;

                            var defaulttriggerTemplate = GetTriggerTemplate(item?.Item, reflectionHelper, RenderContext?.CellTemplate?.Triggers); // B38 fix: pass item.Item (was RowModel wrapper)
                            BackgroundColor = defaulttriggerTemplate.BackgroundColor ?? BackgroundColor;
                            Foregroundcolor = defaulttriggerTemplate.Foregroundcolor ?? Foregroundcolor;
                            BorderColor = defaulttriggerTemplate.BorderColor ?? BorderColor;

                            var celltemplate = GetSetterValues(reflectionHelper, CurrentColumns?.CellTemplate?.Setters, item?.Item);
                            BackgroundColor = celltemplate.BackgroundColor ?? BackgroundColor;
                            Foregroundcolor = celltemplate.Foregroundcolor ?? Foregroundcolor;
                            BorderColor = celltemplate.BorderColor ?? BorderColor;

                            var triggerTemplate = GetTriggerTemplate(item?.Item, reflectionHelper, CurrentColumns?.CellTemplate?.Triggers);
                            BackgroundColor = triggerTemplate.BackgroundColor ?? BackgroundColor;
                            Foregroundcolor = triggerTemplate.Foregroundcolor ?? Foregroundcolor;
                            BorderColor = triggerTemplate.BorderColor ?? BorderColor;

                            CellContentAlignment cellContentAlignment = visibleColumns[colIndex].ContentAlignment;

                            if (visibleColumns[colIndex].IsExpandableColumnForChildRows)
                            {
                                if (item?.HasChild == true)
                                {
                                    string togglebutton = "";
                                    if (RenderContext!.RowToggleDetails.ContainsKey(item.Item!))
                                    {
                                        var values = RenderContext!.RowToggleDetails[item.Item];
                                        values.x = currentX;
                                        values.y = currentY;
                                        values.width = GVColumnWidth;
                                        values.height = rowHeight;
                                        RenderContext!.RowToggleDetails[item.Item] = values;
                                    }
                                    var toggleButtonItem = "";
                                    // Check expand state from RenderContext (no ITreeItem dependency)
                                    bool isItemExpanded = false;
                                    if (RenderContext?.ExpandedItems != null)
                                        isItemExpanded = RenderContext.ExpandedItems.GetValueOrDefault(item.Item!, false);
                                    togglebutton = isItemExpanded
                                        ? visibleColumns[colIndex].ExpandIcon
                                        : visibleColumns[colIndex].CollapseIcon;

                                    Draw(canvas, colIndex, row, togglebutton, Foregroundcolor, UniCodeFont, BackgroundColor, BorderColor, GVColumnWidth, currentX, currentY, cellContentAlignment, rowHeight, HighlightSelected(item.Item), CurrentColumns.CellTemplate, item.Item);

                                }
                                else if (item?.IsChildRow == true)
                                {
                                    string childSymbol = "\u251C";
                                    if (Items.Count - 1 == row || Items[row + 1].IsChildRow == false)
                                        childSymbol = "\u2514";
                                    Draw(canvas, colIndex, row, childSymbol, Foregroundcolor, UniCodeFont, BackgroundColor, BorderColor, GVColumnWidth, currentX, currentY, cellContentAlignment, rowHeight, HighlightSelected(item.Item), CurrentColumns.CellTemplate, item.Item);
                                }
                                else
                                {
                                    Draw(canvas, colIndex, row, visibleColumns[colIndex].DataVisible ? val : "", Foregroundcolor, SymbolFont, BackgroundColor, BorderColor, GVColumnWidth, currentX, currentY, cellContentAlignment, rowHeight, HighlightSelected(item.Item), CurrentColumns.CellTemplate, item.Item);
                                }
                            }
                            else if (item?.IsChildRow == true)
                            {
                                SKCellTemplate cellTemplate = new SKCellTemplate()
                                {
                                    Setters = CurrentColumns?.CellTemplate?.Setters,
                                    Triggers = CurrentColumns?.CellTemplate?.Triggers,
                                };


                                Draw(canvas, colIndex, row, visibleColumns[colIndex].DataVisible ? val : "", Foregroundcolor, SymbolFont, BackgroundColor, BorderColor, GVColumnWidth, currentX, currentY, cellContentAlignment, rowHeight, HighlightSelected(item.Item), cellTemplate, item.Item);
                            }
                            else if (CurrentColumns.IsCheckboxColumn && item?.Item != null)
                            {
                                // Checkbox column: draw checkbox glyph and register hit-test bounds
                                var cbBounds = _cellRenderer.DrawCheckbox(canvas!, rowIndex: row, value: val,
                                    fontcolor: Foregroundcolor, textFont: UniCodeFont,
                                    backColor: BackgroundColor, borderColor: BorderColor,
                                    width: GVColumnWidth, x: currentX, y: currentY, rowHeight: rowHeight,
                                    isSelectedRow: HighlightSelected(item.Item), isWindowActive: IsWindowActive,
                                    selectedRowBg: SelectedRowBackgroundHighlighting, selectedRowText: SelectedRowTextColor,
                                    selectionStyle: SelectionStyle);

                                if (RenderContext?.CheckboxDetails != null && !string.IsNullOrEmpty(CurrentColumns.BindingPath))
                                {
                                    var cbKey = (item.Item, CurrentColumns.BindingPath);
                                    RenderContext.CheckboxDetails[cbKey] = cbBounds;
                                }
                            }
                            else
                                Draw(canvas, colIndex, row, visibleColumns[colIndex].DataVisible ? val : "", Foregroundcolor, SymbolFont, BackgroundColor, BorderColor, GVColumnWidth, currentX, currentY, cellContentAlignment, rowHeight, HighlightSelected(item.Item), CurrentColumns.CellTemplate, item.Item);

                            // Row-level border: only from RowTemplate setters/triggers (spans entire row)
                            // Cell-level border is handled inside Draw → DrawCell via cascaded BorderColor
                            if (rowBorderColor != null)
                            {
                                DrawBorder(canvas, rowBorderColor, (float)columnSum, currentX1, currentY, rowHeight);
                            }

                        }

                        if (ShowGridLines)
                        {
                            canvas?.DrawLine(currentX + GVColumnWidth, currentY, currentX + GVColumnWidth, currentY + rowHeight, GridLineColor);
                            canvas?.DrawLine(currentX, currentY + rowHeight, currentX + GVColumnWidth, currentY + rowHeight, GridLineColor);
                        }
                        currentX += GVColumnWidth;
                    }
                    // Border-style selection: draw outline around the entire row after all cells
                    if (SelectionStyle == SKSelectionStyle.Border && item != null && HighlightSelected(item.Item) && IsWindowActive)
                    {
                        DrawBorder(canvas!, SelectedRowBackgroundHighlighting, currentX - currentX1, currentX1, currentY, rowHeight);
                    }

                    items?.MoveNext();
                    currentY += rowHeight;
                }

                // Drag overlay LAST so the insertion line and the ghosted source rows sit on top
                // of the cells rather than under the next row's background.
                DrawRowDragOverlay(canvas, rowHeight, totalRows);
            }
            catch (Exception ex)
            {
                // B8 fix: log rendering errors instead of silently swallowing
                GridLogger.LogError(GridLogger.Render, "Draw failed", ex);
            }
        }

        /// <summary>
        /// Ghost the rows being dragged and draw the insertion line at the drop gap.
        /// Content coordinates — the canvas is already translated by the scroll offset.
        /// </summary>
        internal void DrawRowDragOverlay(SKCanvas canvas, float rowHeight, int totalRows)
        {
            if (RowDragInsertIndex < 0 || canvas == null || rowHeight <= 0) return;

            float width = 0;
            for (int i = 0; i < _visibleColumnsCache.Count; i++)
                width += (float)_visibleColumnsCache[i].Width;
            if (width <= 0) return;

            var indicator = SKPaintCache.Get(RowDragIndicatorColorHex);

            // Source rows, ghosted, so it is obvious WHAT is moving and not only where to.
            if (RowDragRowIndexes != null)
            {
                var ghost = SKPaintCache.Get(GhostHex(RowDragIndicatorColorHex));
                for (int i = 0; i < RowDragRowIndexes.Count; i++)
                {
                    int row = RowDragRowIndexes[i];
                    if (row < 0 || row >= totalRows) continue;
                    canvas.DrawRect(0, row * rowHeight, width, rowHeight, ghost);
                }
            }

            // Insertion line. Drawn as a filled rect, not a stroked line: cached paints are shared
            // and carry StrokeWidth 1, and mutating one to thicken this line would thicken every
            // grid line drawn with the same colour.
            float y = Math.Min(RowDragInsertIndex, totalRows) * rowHeight;
            float top = Math.Max(0f, y - RowDragIndicatorThickness / 2f);
            canvas.DrawRect(0, top, width, RowDragIndicatorThickness, indicator);
        }

        /// <summary>
        /// Same colour at ~33% alpha, as an 8-digit ARGB hex so it can come from SKPaintCache
        /// instead of allocating an SKPaint per frame. Any alpha already present is replaced.
        /// </summary>
        internal static string GhostHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return hex;
            var body = hex.TrimStart('#');
            if (body.Length == 8) body = body.Substring(2);   // drop an existing alpha
            else if (body.Length == 3) body = string.Concat(body[0], body[0], body[1], body[1], body[2], body[2]);
            if (body.Length != 6) return hex;
            return "#55" + body;
        }

        public void UpdateVisibleColumns()
        {
            _visibleColumnsCache.Clear();

            if (Columns == null) return;

            foreach (var col in Columns.OrderBy(x => x.DisplayIndex).ToList())
            {
                if (col.IsVisible)
                    _visibleColumnsCache.Add(col);
            }
        }
        // Delegates to CellRenderer (extracted in Step 5)
        private void Draw(SKCanvas canvas, int columnsIndex, int rowIndex, string value, SKPaint fontcolor, SKFont textFont, SKPaint backColor, SKPaint? borderColor, float width, float x, float y, CellContentAlignment cellContentAlignment, float rowHeight, bool isselectedrow, SKCellTemplate? sKCellTemplate = null, object? data = null)
            => _cellRenderer.DrawCell(canvas, columnsIndex, rowIndex, value, fontcolor, textFont, backColor, borderColor, width, x, y, cellContentAlignment, rowHeight, isselectedrow, IsWindowActive, SelectedRowBackgroundHighlighting, SelectedRowTextColor, sKCellTemplate, data, _buttonRenderer!, SelectionStyle);


        // DrawButton, DrawText, DrawRect, DrawBorder, DrawBorder2, HighlightSelected, _imageCache, GetOrCreateBitmap
        // All extracted to CellRenderer.cs and ButtonRenderer.cs (Step 5)
        private void DrawButton(SKCanvas canvas, SkButton skButton, object data, float CurrentX, float y, double width, float rowHeight, SKPaint rowBackColor, int columnsIndex, int rowIndex, bool isBorderDrawed)
            => _buttonRenderer!.DrawButton(canvas, skButton, data, CurrentX, y, width, rowHeight, rowBackColor, columnsIndex, rowIndex, isBorderDrawed);

        private void DrawText(SKCanvas canvas, int columnsIndex, int rowIndex, string value, SKPaint fontColor, SKFont textFont, float width, float x, float y, CellContentAlignment cellContentAlignment, float rowHeight = 0f)
            => _cellRenderer.DrawText(canvas, columnsIndex, rowIndex, value, fontColor, textFont, width, x, y, cellContentAlignment, rowHeight);

        private void DrawRect(SKCanvas canvas, int rowIndex, float x, float y, SKPaint backColor, float width, float rowHeight)
            => _cellRenderer.DrawRect(canvas, rowIndex, x, y, backColor, width, rowHeight);

        private static void DrawBorder(SKCanvas canvas, SKPaint? borderColor, float width, float x, float y, float rowHeight)
            => CellRenderer.DrawBorder(canvas, borderColor, width, x, y, rowHeight);

        private static void DrawBorder2(SKCanvas canvas, SKPaint? borderColor, float width, float x, float y, float rowHeight)
            => CellRenderer.DrawBorder2(canvas, borderColor, width, x, y, rowHeight);

        private bool HighlightSelected(object? item)
            => _cellRenderer.HighlightSelected(item, SelectedItems);

        /// <summary>
        /// On-demand tooltip text for a hovered cell: returns the full formatted cell
        /// value ONLY when it would be ellipsis-truncated at the column's width (mirrors
        /// CellRenderer.DrawText's "column width - 10" budget and the data font). Returns
        /// null when the cell fits, is empty, or the row/column is out of range — so the
        /// caller shows a tooltip only where it adds information. Called on cell-change
        /// (hover), never per frame.
        /// </summary>
        /// <summary>Visible column at a hit-test index, or null. Ordered as drawn.</summary>
        internal SKGridViewColumn? VisibleColumnAt(int index)
            => index >= 0 && index < _visibleColumnsCache.Count ? _visibleColumnsCache[index] : null;

        /// <summary>
        /// The text a cell DISPLAYS for this item/column — same read + same Format/negative/acronym
        /// handling the draw path uses, so a dragged-out value matches what the user saw.
        /// </summary>
        internal string? FormatCellText(object? item, SKGridViewColumn? col)
        {
            if (_disposed || item == null || col == null || string.IsNullOrEmpty(col.BindingPath)) return null;
            var (val, type) = reflectionHelper.ReadCurrentItemWithTypes(item, col.BindingPath);
            return Helper.ApplyFormat(type, val, col.Format, col.ShowBracketOnNegative, col.FormatWithAcronym);
        }

        internal string? GetTruncatedCellText(int rowIndex, int colIndex)
        {
            if (_disposed) return null;

            var cols = _visibleColumnsCache;
            if (cols == null || colIndex < 0 || colIndex >= cols.Count) return null;
            var col = cols[colIndex];
            if (string.IsNullOrEmpty(col.BindingPath)) return null;

            // Resolve the data item at this VISUAL row — flat (Items) or grouped
            // (visible group rows). Header/subtotal rows have a null Item → no tooltip.
            object? item;
            if (Group != null)
            {
                var visible = GroupItemSource.Where(g => g.IsGroupHeader || g.IsExpanded).ToList();
                if (rowIndex < 0 || rowIndex >= visible.Count) return null;
                item = visible[rowIndex].Item;
            }
            else
            {
                if (Items == null || rowIndex < 0 || rowIndex >= Items.Count) return null;
                item = Items[rowIndex].Item;
            }
            if (item == null) return null;

            var (val, type) = reflectionHelper.ReadCurrentItemWithTypes(item, col.BindingPath);
            var text = Helper.ApplyFormat(type, val, col.Format, col.ShowBracketOnNegative, col.FormatWithAcronym);
            if (string.IsNullOrEmpty(text)) return null;

            // Truncated? Same budget as DrawText (available = column width - 10) and the
            // same font data cells are drawn with.
            float available = (float)col.Width - 10f;
            if (available <= 0) return null;
            float measured = SymbolFont.MeasureText(text, out _);
            return measured > available ? text : null;
        }

        /// <summary>
        /// Resolve a data property path (dot-notation OK) to display text against an EXPLICIT
        /// data item. Backs the opt-in tooltip paths (<c>SkButton.TooltipPath</c> /
        /// <c>SKGridViewColumn.TooltipPath</c>) — independent of any column BindingPath or
        /// truncation. Returns null when the item/path is empty or the resolved value is
        /// null/empty. Called on hover-change only, never per frame.
        /// </summary>
        internal string? ResolveTextPath(object? item, string? path)
        {
            if (_disposed || item == null || string.IsNullOrEmpty(path)) return null;
            var (val, _) = reflectionHelper.ReadCurrentItemWithTypes(item, path!);
            return string.IsNullOrEmpty(val) ? null : val;
        }

        /// <summary>
        /// Tooltip text for a hovered cell (opt-in). Precedence:
        ///   (b) if the column defines <c>TooltipPath</c>, resolve it against the row's data
        ///       item and return it REGARDLESS of truncation;
        ///   (c) otherwise fall back to the existing truncated-bound-text behavior
        ///       (<see cref="GetTruncatedCellText"/>).
        /// Returns null when nothing applies. Called on hover-change only, never per frame.
        /// </summary>
        internal string? GetCellTooltip(int rowIndex, int colIndex)
        {
            if (_disposed) return null;

            var cols = _visibleColumnsCache;
            if (cols == null || colIndex < 0 || colIndex >= cols.Count) return null;
            var col = cols[colIndex];

            if (!string.IsNullOrEmpty(col.TooltipPath))
            {
                var item = GetVisualRowItem(rowIndex);
                var text = ResolveTextPath(item, col.TooltipPath);
                if (!string.IsNullOrEmpty(text)) return text;
            }

            // (c) Fallback — unchanged truncated-bound-text tooltip.
            return GetTruncatedCellText(rowIndex, colIndex);
        }

        /// <summary>
        /// Data item at a VISUAL row index — flat (Items) or grouped (visible group rows).
        /// Header/subtotal rows have a null Item. Mirrors the row resolution inside
        /// <see cref="GetTruncatedCellText"/>. Returns null when out of range.
        /// </summary>
        private object? GetVisualRowItem(int rowIndex)
        {
            if (Group != null)
            {
                var visible = GroupItemSource.Where(g => g.IsGroupHeader || g.IsExpanded).ToList();
                if (rowIndex < 0 || rowIndex >= visible.Count) return null;
                return visible[rowIndex].Item;
            }

            if (Items == null || rowIndex < 0 || rowIndex >= Items.Count) return null;
            return Items[rowIndex].Item;
        }

        // ── Color resolution for UI Automation ItemStatus ────────────────────────
        //
        // Replays the same 6-step style cascade the Draw loop uses, but for a single
        // (row, col) on demand. Used by AutomationDataProvider to expose live cell
        // colors via the UIA ItemStatus property — automation tests can read the
        // actual rendered colors of a cell without taking screenshots.
        //
        // Cascade matches Draw() (SkiaRenderer.cs around line 302+):
        //   1. RowTemplate.Setters
        //   2. RowTemplate.Triggers (first-match-wins)
        //   3. RenderContext.CellTemplate.Setters
        //   4. RenderContext.CellTemplate.Triggers
        //   5. Column.CellTemplate.Setters
        //   6. Column.CellTemplate.Triggers
        // (Border is reset between row and cell — matches renderer's behavior.)

        internal (SKColor Background, SKColor Foreground, SKColor? Border) ResolveCellColors(int rowIndex, int colIndex)
        {
            // Automation clients (Inspect / WinAppDriver) hold peer references and can query
            // ItemStatus after the grid is disposed — reading .Color off a freed SKPaint is
            // the same native-AV class as the setter crashes. Bail with safe defaults.
            if (_disposed) return (SKColors.Transparent, SKColors.Black, null);

            // Defaults if the renderer isn't ready yet.
            SKColor defaultBg = RowBackgroundColor?.Color ?? SKColors.Transparent;
            SKColor defaultFg = FontColor?.Color ?? SKColors.Black;

            if (Items == null || rowIndex < 0 || rowIndex >= Items.Count)
                return (defaultBg, defaultFg, null);

            var visibleCols = _visibleColumnsCache;
            if (visibleCols == null || colIndex < 0 || colIndex >= visibleCols.Count)
                return (defaultBg, defaultFg, null);

            var item = Items[rowIndex].Item;
            if (item == null) return (defaultBg, defaultFg, null);

            var col = visibleCols[colIndex];
            SKPaint rowcolor = (rowIndex % 2 == 0)
                ? RowBackgroundColor
                : (AlternatingRowBackground ?? RowBackgroundColor);

            // Step 1: RowTemplate.Setters
            var rowSetters = GetSetterValues(reflectionHelper, RenderContext?.RowTemplate?.Setters, item);
            SKPaint bg = rowSetters.BackgroundColor ?? rowcolor;
            SKPaint fg = rowSetters.Foregroundcolor ?? FontColor;
            SKPaint? border = rowSetters.BorderColor;

            // Step 2: RowTemplate.Triggers
            var rowTriggers = GetTriggerTemplate(item, reflectionHelper, RenderContext?.RowTemplate?.Triggers ?? Enumerable.Empty<SKTrigger>());
            if (rowTriggers.BackgroundColor != null) bg = rowTriggers.BackgroundColor;
            if (rowTriggers.Foregroundcolor != null) fg = rowTriggers.Foregroundcolor;
            if (rowTriggers.BorderColor != null)     border = rowTriggers.BorderColor;

            // Cell-level border starts fresh (matches Draw line 319: BorderColor = null)
            border = null;

            // Step 3: RenderContext.CellTemplate.Setters
            var ctxSetters = GetSetterValues(reflectionHelper, RenderContext?.CellTemplate?.Setters, item);
            if (ctxSetters.BackgroundColor != null) bg = ctxSetters.BackgroundColor;
            if (ctxSetters.Foregroundcolor != null) fg = ctxSetters.Foregroundcolor;
            if (ctxSetters.BorderColor != null)     border = ctxSetters.BorderColor;

            // Step 4: RenderContext.CellTemplate.Triggers
            var ctxTriggers = GetTriggerTemplate(item, reflectionHelper, RenderContext?.CellTemplate?.Triggers ?? Enumerable.Empty<SKTrigger>());
            if (ctxTriggers.BackgroundColor != null) bg = ctxTriggers.BackgroundColor;
            if (ctxTriggers.Foregroundcolor != null) fg = ctxTriggers.Foregroundcolor;
            if (ctxTriggers.BorderColor != null)     border = ctxTriggers.BorderColor;

            // Step 5: Column.CellTemplate.Setters
            var colSetters = GetSetterValues(reflectionHelper, col.CellTemplate?.Setters, item);
            if (colSetters.BackgroundColor != null) bg = colSetters.BackgroundColor;
            if (colSetters.Foregroundcolor != null) fg = colSetters.Foregroundcolor;
            if (colSetters.BorderColor != null)     border = colSetters.BorderColor;

            // Step 6: Column.CellTemplate.Triggers
            var colTriggers = GetTriggerTemplate(item, reflectionHelper, col.CellTemplate?.Triggers ?? Enumerable.Empty<SKTrigger>());
            if (colTriggers.BackgroundColor != null) bg = colTriggers.BackgroundColor;
            if (colTriggers.Foregroundcolor != null) fg = colTriggers.Foregroundcolor;
            if (colTriggers.BorderColor != null)     border = colTriggers.BorderColor;

            // Final overlay: selection-state colors. Matches the renderer's behavior at
            // CellRenderer.DrawCell:44 — when a row is selected, the window is active,
            // and SelectionStyle=Fill, the drawn colors swap to the selection paints.
            // ItemStatus must reflect what the user SEES, so include this overlay.
            if (IsRowSelectedByIndex(rowIndex) && IsWindowActive && SelectionStyle == SKSelectionStyle.Fill)
            {
                if (SelectedRowBackgroundHighlighting != null) bg = SelectedRowBackgroundHighlighting;
                // A transparent foreground is preserved through selection (matches
                // CellRenderer.DrawCell) — don't swap it to the selected-row text color.
                if (SelectedRowTextColor != null && (fg == null || fg.Color.Alpha != 0)) fg = SelectedRowTextColor;
                border = null;   // selection fill suppresses the cell border (renderer line 54)
            }

            return (bg?.Color ?? defaultBg, fg?.Color ?? defaultFg, border?.Color);
        }

        /// <summary>
        /// True if the row at <paramref name="rowIndex"/> is currently in SelectedItems.
        /// Used by color resolution to apply the selection-fill overlay. O(|selected|).
        /// </summary>
        private bool IsRowSelectedByIndex(int rowIndex)
        {
            if (SelectedItems == null || Items == null) return false;
            if (rowIndex < 0 || rowIndex >= Items.Count) return false;
            var item = Items[rowIndex].Item;
            if (item == null) return false;
            foreach (var sel in SelectedItems)
                if (ReferenceEquals(sel, item) || Equals(sel, item)) return true;
            return false;
        }

        /// <summary>Row-level colors (steps 1-2 only — RowTemplate setters + triggers).</summary>
        internal (SKColor Background, SKColor Foreground, SKColor? Border) ResolveRowColors(int rowIndex)
        {
            // Same post-dispose UIA-query guard as ResolveCellColors.
            if (_disposed) return (SKColors.Transparent, SKColors.Black, null);

            SKColor defaultBg = RowBackgroundColor?.Color ?? SKColors.Transparent;
            SKColor defaultFg = FontColor?.Color ?? SKColors.Black;

            if (Items == null || rowIndex < 0 || rowIndex >= Items.Count)
                return (defaultBg, defaultFg, null);

            var item = Items[rowIndex].Item;
            if (item == null) return (defaultBg, defaultFg, null);

            SKPaint rowcolor = (rowIndex % 2 == 0)
                ? RowBackgroundColor
                : (AlternatingRowBackground ?? RowBackgroundColor);

            var rowSetters = GetSetterValues(reflectionHelper, RenderContext?.RowTemplate?.Setters, item);
            SKPaint bg = rowSetters.BackgroundColor ?? rowcolor;
            SKPaint fg = rowSetters.Foregroundcolor ?? FontColor;
            SKPaint? border = rowSetters.BorderColor;

            var rowTriggers = GetTriggerTemplate(item, reflectionHelper, RenderContext?.RowTemplate?.Triggers ?? Enumerable.Empty<SKTrigger>());
            if (rowTriggers.BackgroundColor != null) bg = rowTriggers.BackgroundColor;
            if (rowTriggers.Foregroundcolor != null) fg = rowTriggers.Foregroundcolor;
            if (rowTriggers.BorderColor != null)     border = rowTriggers.BorderColor;

            // Selection overlay — match what the user sees on screen.
            if (IsRowSelectedByIndex(rowIndex) && IsWindowActive && SelectionStyle == SKSelectionStyle.Fill)
            {
                if (SelectedRowBackgroundHighlighting != null) bg = SelectedRowBackgroundHighlighting;
                // Transparent foreground survives selection (matches CellRenderer.DrawCell).
                if (SelectedRowTextColor != null && (fg == null || fg.Color.Alpha != 0)) fg = SelectedRowTextColor;
                border = null;
            }

            return (bg?.Color ?? defaultBg, fg?.Color ?? defaultFg, border?.Color);
        }

        // Delegates to TriggerEvaluator (extracted in Step 4)
        private static (SKPaint BackgroundColor, SKPaint Foregroundcolor, SKPaint BorderColor) GetTriggerTemplate(
    object item, ReflectionHelper reflection, IEnumerable<SKTrigger> triggers)
            => TriggerEvaluator.GetTriggerTemplate(item, reflection, triggers);

        // Delegates to TriggerEvaluator (extracted in Step 4)
        private (SKPaint BackgroundColor, SKPaint ForegroundColor, SKPaint BorderColor) GetGroupTriggerTemplate(
        ReflectionHelper reflectionHelper,
        IEnumerable<GroupModel> groupItems,
        IEnumerable<SKGroupTrigger>? triggers)
            => _triggerEvaluator.GetGroupTriggerTemplate(reflectionHelper, groupItems, triggers);

        // EvaluateGroupDataTriggerInternal, EvaluateGroupMultiTriggerInternal, EvaluateGroupConditionInternal,
        // CompareValues, GetSetterValues, CalculateGroupAggregation — logic extracted to
        // TriggerEvaluator.cs and SetterResolver.cs (Step 4). These bodies removed, delegates above handle all calls.

        private static (SKPaint BackgroundColor, SKPaint Foregroundcolor, SKPaint BorderColor) GetSetterValues(ReflectionHelper reflection, IEnumerable<SKSetter>? setters, object Item)
            => SetterResolver.GetSetterValues(reflection, setters, Item);

        private object? CalculateGroupAggregation(
    IEnumerable<GroupModel> groupItems,
    ReflectionHelper reflectionHelper,
    string bindingPath,
    SkAggregation aggregation)
            => _setterResolver.CalculateGroupAggregation(groupItems, reflectionHelper, bindingPath, aggregation);

        // CompareValues delegated to TriggerEvaluator.CompareValues (internal static)
        private bool CompareValues(object left, object right, SKOperation op)
            => TriggerEvaluator.CompareValues(left, right, op);

        // Extracted in Steps 2-6: ExportData → GridExportService, Triggers → TriggerEvaluator,
        // Setters → SetterResolver, Cells → CellRenderer, Buttons → ButtonRenderer, Groups → GroupRowRenderer

        // B28 fix: dispose all native SKPaint, SKFont resources + clear caches.
        // Multi-window fix: only clear PROCESS-WIDE caches when the LAST renderer
        // is disposed — otherwise paints belonging to still-live grids get freed
        // out from under them, causing severe flicker/visual corruption.
        public void Dispose()
        {
            // Idempotent: a second Dispose() must not run again. Besides the obvious
            // double-free, an unguarded re-entry would decrement _activeInstanceCount
            // twice and clear the process-wide caches while other grids are still live.
            if (_disposed) return;
            _disposed = true;

            int remaining = System.Threading.Interlocked.Decrement(ref _activeInstanceCount);
            if (remaining <= 0)
            {
                SKPaintCache.Clear();
                SKTriggerStateCache.Clear();
            }
            SelectedRowBackgroundHighlighting?.Dispose();
            SelectedRowTextColor?.Dispose();
            GridLineColor?.Dispose();
            FontColor?.Dispose();
            RowBackgroundColor?.Dispose();
            AlternatingRowBackground?.Dispose();
            GroupRowBackgroundColor?.Dispose();
            GroupFontColor?.Dispose();
            ButtonBackgroundcolor?.Dispose();
            ButtonForegroundcolor?.Dispose();
            ButtonBordercolor?.Dispose();
            CellBackgroundColor?.Dispose();
            CellBorderColor?.Dispose();
            SymbolFont?.Dispose();
            UniCodeFont?.Dispose();
            _buttonRenderer?.Dispose();
        }
    }
}

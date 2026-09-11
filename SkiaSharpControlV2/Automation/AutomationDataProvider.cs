using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;
using System.Windows;
using System.Windows.Media;

namespace SkiaSharpControlV2.Automation
{
    /// <summary>
    /// Read-only bridge between automation peers and grid internals.
    /// No caching — reads live state every call. Zero render impact.
    /// </summary>
    internal class AutomationDataProvider
    {
        private readonly SkiaGridViewV2 _control;
        private readonly ReflectionHelper _reflectionHelper = new();

        public AutomationDataProvider(SkiaGridViewV2 control)
        {
            _control = control;
        }

        // ── Dimensions ──────────────────────────────────────────────────

        public int RowCount
        {
            get
            {
                if (_control.GroupSettings != null)
                    return GetVisibleGroupItems().Count;
                return _control.SkiaRenderer?.Items?.Count ?? 0;
            }
        }

        public int ColumnCount => VisibleColumns.Count;

        // Memoized — this property is read per row peer AND per cell peer during a
        // client tree walk, and the fresh Where/OrderBy/ToList per access showed as a
        // hot frame in the 30k-row freeze trace. Invalidated by the grid peer's
        // InvalidateRowCache (which the control already calls on every data drain and
        // on column visibility / order / rebuild changes), so it is never stale for
        // more than one already-notified structural change.
        private IReadOnlyList<SKGridViewColumn>? _visibleColumnsCache;

        public IReadOnlyList<SKGridViewColumn> VisibleColumns
        {
            get
            {
                var cached = _visibleColumnsCache;
                if (cached != null) return cached;

                if (_control.Columns == null) return Array.Empty<SKGridViewColumn>();
                cached = _control.Columns
                    .Where(c => c.IsVisible)
                    .OrderBy(c => c.DisplayIndex)
                    .ToList();
                _visibleColumnsCache = cached;
                return cached;
            }
        }

        /// <summary>Drop the memoized visible-column list (called on structural changes).</summary>
        internal void InvalidateColumnCache() => _visibleColumnsCache = null;

        /// <summary>
        /// The row range the (virtualized) automation tree should expose: rows
        /// intersecting the viewport ± <paramref name="overscan"/>, clamped to the data.
        /// When layout hasn't completed yet (viewport height 0 — e.g. a UIA client
        /// attaching during startup), exposes a bounded slice from the top instead of
        /// either nothing or the full O(N) set.
        /// </summary>
        public (int FirstRow, int Count) GetViewportRowRange(int overscan)
        {
            int rowCount = RowCount;
            if (rowCount <= 0) return (0, 0);

            float rowHeight = RowHeight;
            double viewportH = ViewportHeight;

            int first;
            int visible;
            if (rowHeight <= 0 || viewportH <= 0)
            {
                // Pre-layout fallback: a bounded slice from the top.
                first = 0;
                visible = Math.Min(rowCount, 50);
            }
            else
            {
                first = Math.Max(0, (int)(ScrollOffsetY / rowHeight) - overscan);
                visible = (int)Math.Ceiling(viewportH / rowHeight) + overscan * 2;
            }

            if (first >= rowCount) first = Math.Max(0, rowCount - visible);
            int count = Math.Max(0, Math.Min(visible, rowCount - first));
            return (first, count);
        }

        public float RowHeight => _control.RowHeight;

        // ── Data Access ─────────────────────────────────────────────────

        private List<GroupModel> GetVisibleGroupItems()
        {
            var groupItems = _control.SkiaRenderer?.GroupItemSource;
            if (groupItems == null) return new();
            return groupItems.Where(g => g.IsGroupHeader || g.IsExpanded).ToList();
        }

        /// <summary>Get the underlying data item at a flattened row index.</summary>
        public object? GetDataItem(int rowIndex)
        {
            if (_control.GroupSettings != null)
            {
                var visible = GetVisibleGroupItems();
                if (rowIndex < 0 || rowIndex >= visible.Count) return null;
                // For group headers, Item is null — return the GroupModel itself
                return visible[rowIndex].Item ?? visible[rowIndex];
            }
            else
            {
                var items = _control.SkiaRenderer?.Items;
                if (items == null || rowIndex < 0 || rowIndex >= items.Count) return null;
                return items[rowIndex].Item;
            }
        }

        /// <summary>Get the GroupModel at a row index (grouped mode only).</summary>
        public GroupModel? GetGroupModel(int rowIndex)
        {
            if (_control.GroupSettings == null) return null;
            var visible = GetVisibleGroupItems();
            return rowIndex >= 0 && rowIndex < visible.Count ? visible[rowIndex] : null;
        }

        /// <summary>Get formatted cell value — same pipeline as the renderer uses.</summary>
        public string GetCellValue(int rowIndex, int colIndex)
        {
            var cols = VisibleColumns;
            if (colIndex < 0 || colIndex >= cols.Count) return "";

            var col = cols[colIndex];

            // Group header: show group name in the target column
            if (IsGroupHeader(rowIndex))
            {
                var gm = GetGroupModel(rowIndex);
                return gm?.GroupName ?? "";
            }

            var item = GetDataItem(rowIndex);
            if (item == null || string.IsNullOrEmpty(col.BindingPath)) return "";

            var (value, type) = _reflectionHelper.ReadCurrentItemWithTypes(item, col.BindingPath);
            return Helper.ApplyFormat(type, value, col.Format, col.ShowBracketOnNegative, col.FormatWithAcronym);
        }

        // ── Selection ───────────────────────────────────────────────────

        public bool IsRowSelected(int rowIndex)
        {
            var item = GetDataItem(rowIndex);
            if (item == null) return false;
            return _control.SelectedItems?.Contains(item) == true;
        }

        /// <summary>
        /// Flattened (visible) row index of a data item, or -1. Used to locate the row peer
        /// for an item that changed selection so a per-row ElementSelected event can be
        /// raised. Matches the same index space GetDataItem uses.
        /// </summary>
        public int GetRowIndexOfItem(object? item)
        {
            if (item == null) return -1;

            if (_control.GroupSettings != null)
            {
                var visible = GetVisibleGroupItems();
                for (int i = 0; i < visible.Count; i++)
                {
                    var it = visible[i].Item ?? visible[i];
                    if (Equals(it, item)) return i;
                }
                return -1;
            }

            var items = _control.SkiaRenderer?.Items;
            if (items == null) return -1;
            for (int i = 0; i < items.Count; i++)
                if (Equals(items[i].Item, item)) return i;
            return -1;
        }

        // ── Group State ─────────────────────────────────────────────────

        public bool IsGroupHeader(int rowIndex)
        {
            return GetGroupModel(rowIndex)?.IsGroupHeader == true;
        }

        public bool IsGroupExpanded(int rowIndex)
        {
            var gm = GetGroupModel(rowIndex);
            if (gm == null || !gm.IsGroupHeader) return false;
            var name = gm.GroupName ?? "";
            return _control.GroupToggleDetails.TryGetValue(name, out var details) && details.IsExpended;
        }

        public string? GetGroupName(int rowIndex) => GetGroupModel(rowIndex)?.GroupName;

        // ── Bounds (Screen Coordinates) ─────────────────────────────────

        /// <summary>Get the bounding rectangle of a row in screen coordinates.</summary>
        public Rect GetRowBounds(int rowIndex)
        {
            var y = rowIndex * RowHeight - _control.ScrollOffsetY;
            var totalWidth = VisibleColumns.Sum(c => c.Width);
            var localRect = new Rect(0, y, totalWidth, RowHeight);
            return TransformToScreen(localRect);
        }

        /// <summary>Get the bounding rectangle of a cell in screen coordinates.</summary>
        public Rect GetCellBounds(int rowIndex, int colIndex)
        {
            var cols = VisibleColumns;
            if (colIndex < 0 || colIndex >= cols.Count) return Rect.Empty;

            double x = 0;
            for (int i = 0; i < colIndex; i++)
                x += cols[i].Width;
            x -= _control.ScrollOffsetX;

            var y = rowIndex * RowHeight - _control.ScrollOffsetY;
            var localRect = new Rect(x, y, cols[colIndex].Width, RowHeight);
            return TransformToScreen(localRect);
        }

        /// <summary>
        /// Bounding rectangle of a column header in screen coordinates. The WPF DataGrid
        /// ("DataListView") renders the headers; we derive each header's position from the
        /// actual column widths + display order and convert through the DataGrid's own
        /// PointToScreen. Without this, header peers report Rect.Empty and AI can't locate
        /// them at any screen position — hovering selects the whole grid instead.
        /// </summary>
        public Rect GetColumnHeaderBounds(int colIndex)
        {
            var cols = VisibleColumns;
            if (colIndex < 0 || colIndex >= cols.Count) return Rect.Empty;

            var dataGrid = _control.DataListView;
            if (dataGrid == null || !dataGrid.IsLoaded) return Rect.Empty;

            // Match our SKGridViewColumn to the corresponding WPF DataGridColumn by Header
            // (WPF column.Header is set to SKGridViewColumn.DisplayHeader ?? Header in
            // UpdateColumnsInDataGrid).
            var ours = cols[colIndex];
            var headerText = ours.DisplayHeader ?? ours.Header;
            System.Windows.Controls.DataGridColumn? wpfCol = null;
            foreach (var c in dataGrid.Columns)
            {
                if ((c.Header as string) == headerText) { wpfCol = c; break; }
            }
            if (wpfCol == null) return Rect.Empty;

            // Local X = sum of preceding VISIBLE columns' ActualWidth (in DisplayIndex order),
            // minus the horizontal scroll offset (headers scroll with the canvas).
            //
            // CRITICAL: filter out hidden columns. WPF DataGridColumn.ActualWidth keeps the
            // configured width even when Visibility=Collapsed, so a naive sum over all
            // columns counts hidden ones too — which produced bounds at the wrong x after
            // toggling column visibility at runtime (e.g. "View -> Settings -> uncheck a
            // column -> OK" left every following header reporting bounds shifted right by
            // the hidden column's width, even though it had collapsed in the visible layout).
            double x = 0;
            foreach (var c in dataGrid.Columns.OrderBy(c => c.DisplayIndex))
            {
                if (ReferenceEquals(c, wpfCol)) break;
                if (c.Visibility != Visibility.Visible) continue;
                x += c.ActualWidth;
            }
            x -= _control.ScrollOffsetX;

            double width = wpfCol.ActualWidth;
            double height = !double.IsNaN(dataGrid.ColumnHeaderHeight) && dataGrid.ColumnHeaderHeight > 0
                ? dataGrid.ColumnHeaderHeight
                : 25; // WPF DataGrid header default ~22-25 px when Auto

            var localRect = new Rect(x, 0, width, height);
            try
            {
                var topLeft = dataGrid.PointToScreen(new Point(localRect.X, localRect.Y));
                var bottomRight = dataGrid.PointToScreen(new Point(localRect.Right, localRect.Bottom));
                return new Rect(topLeft, bottomRight);
            }
            catch
            {
                return localRect;
            }
        }

        /// <summary>
        /// Transform a local canvas rect to screen coordinates for Inspect.exe / FlaUI.
        ///
        /// The local rect is deterministic math (row index → y via row height − scroll,
        /// x/width from the configured column widths), so a row's bounds are correct and
        /// STABLE the instant its peer exists — they do NOT depend on a render tick and do
        /// not shift for a frame or two after a row is streamed in.
        ///
        /// The only pre-layout window is before the canvas is connected to a rendering
        /// surface (HWND). In that window we return <see cref="Rect.Empty"/> rather than the
        /// raw LOCAL rect: a local rect interpreted as screen coordinates anchors the element
        /// near (0,0), which is exactly the "rect reports 0 / shifts for a tick" symptom.
        /// Empty is the honest "not placeable yet" answer; once laid out (the normal case
        /// when a test runs) this always returns the correct non-empty screen rect.
        /// </summary>
        private Rect TransformToScreen(Rect localRect)
        {
            var canvas = _control.SkiaCanvas;
            if (canvas == null || !canvas.IsLoaded || PresentationSource.FromVisual(canvas) == null)
                return Rect.Empty;

            try
            {
                var topLeft = canvas.PointToScreen(new Point(localRect.X, localRect.Y));
                var bottomRight = canvas.PointToScreen(new Point(localRect.Right, localRect.Bottom));
                return new Rect(topLeft, bottomRight);
            }
            catch
            {
                return Rect.Empty;
            }
        }

        // ── UI Automation ItemStatus (live color exposure) ──────────────────────────
        //
        // Format: standard CSS hex — "BG=#RRGGBB;FG=#RRGGBB;BR=#RRGGBB" for opaque colors,
        // "#RRGGBBAA" when alpha is not 0xFF. This is the format every web color tool
        // (Google, CSS, Inspect, Figma, screenshots, design specs) understands. Border
        // (BR) is omitted when no border color is resolved.
        //
        // Why not WPF's #AARRGGBB convention: that order can't round-trip through web
        // hex pickers — pasting "#FF00CC00" into Google reads the first 6 chars as RGB
        // and reports it as magenta, when the actual color is green (#00CC00).
        //
        // Automation tests read the cell's actual rendered colors via:
        //   driver.FindElement(By.AccessibilityId("Cell_3_2")).GetAttribute("ItemStatus");

        /// <summary>
        /// Compose the ItemStatus string for a specific cell from its current resolved
        /// background / foreground / border colors. Runs the same 6-step style cascade
        /// the renderer uses (RowTemplate setters/triggers → CellTemplate setters/triggers
        /// → Column.CellTemplate setters/triggers).
        /// </summary>
        public string GetCellItemStatus(int rowIndex, int colIndex)
        {
            var renderer = _control.SkiaRenderer;
            if (renderer == null) return string.Empty;
            var (bg, fg, br) = renderer.ResolveCellColors(rowIndex, colIndex);
            return FormatItemStatus(bg, fg, br);
        }

        /// <summary>Compose the ItemStatus string for a row (RowTemplate setters + triggers).</summary>
        public string GetRowItemStatus(int rowIndex)
        {
            var renderer = _control.SkiaRenderer;
            if (renderer == null) return string.Empty;
            var (bg, fg, br) = renderer.ResolveRowColors(rowIndex);
            return FormatItemStatus(bg, fg, br);
        }

        private static string FormatItemStatus(SkiaSharp.SKColor bg, SkiaSharp.SKColor fg, SkiaSharp.SKColor? border)
        {
            var s = $"BG={ToHex(bg)};FG={ToHex(fg)}";
            if (border.HasValue) s += $";BR={ToHex(border.Value)}";
            return s;
        }

        /// <summary>
        /// CSS-standard hex: <c>#RRGGBB</c> for fully opaque colors, <c>#RRGGBBAA</c>
        /// when alpha is anything other than 0xFF. Round-trips through every web hex
        /// tool (Google, CSS, Figma, color pickers) and matches the format your designers
        /// hand you. Do NOT switch this to <c>#AARRGGBB</c> — it looks superficially
        /// similar but breaks every web tool that parses left-to-right as RGB.
        /// </summary>
        private static string ToHex(SkiaSharp.SKColor c)
        {
            return c.Alpha == 0xFF
                ? $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}"
                : $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}{c.Alpha:X2}";
        }

        /// <summary>
        /// True if the given row is outside the viewport. Computed entirely in LOCAL
        /// canvas coordinates so the result is correct regardless of where the window
        /// sits on screen (the previous implementation mixed screen-Y with local
        /// ViewportHeight and only worked when the window was at screen origin).
        ///
        /// When ViewportHeight is 0 (layout not yet completed — common during the
        /// initial AI tree-walk before the window has been measured), all rows are
        /// reported as on-screen. Otherwise treating them as off-screen would hide
        /// every row except row 0 from automation tools.
        /// </summary>
        public bool IsRowOffscreen(int rowIndex)
        {
            var viewportHeight = ViewportHeight;
            if (viewportHeight <= 0) return false;                 // layout not done — be permissive
            var localY = rowIndex * RowHeight - _control.ScrollOffsetY;
            var localBottom = localY + RowHeight;
            return localBottom < 0 || localY > viewportHeight;
        }

        // ── Scroll ──────────────────────────────────────────────────────

        public double ViewportHeight => _control.MainGrid.ActualHeight;
        public double ViewportWidth => _control.MainGrid.ActualWidth;
        public double TotalHeight => RowCount * RowHeight;
        public double TotalWidth => VisibleColumns.Sum(c => c.Width);
        public double ScrollOffsetX => _control.ScrollOffsetX;
        public double ScrollOffsetY => _control.ScrollOffsetY;

        // ── Buttons ─────────────────────────────────────────────────────

        /// <summary>Get buttons for a cell (from CellTemplate or DrawButton factory).</summary>
        public List<SkButton>? GetCellButtons(int rowIndex, int colIndex)
        {
            var cols = VisibleColumns;
            if (colIndex < 0 || colIndex >= cols.Count) return null;

            var col = cols[colIndex];
            var template = col.CellTemplate;
            if (template == null) return null;

            // Dynamic buttons via factory
            var item = GetDataItem(rowIndex);
            if (template.DrawButton != null && item != null)
            {
                try { return template.DrawButton(item); }
                catch { return null; }
            }

            // Static button
            if (template.SkButton != null)
                return new List<SkButton> { template.SkButton };

            // Static buttons collection
            if (template.SkButtons?.Count > 0)
                return template.SkButtons.ToList();

            return null;
        }
    }
}

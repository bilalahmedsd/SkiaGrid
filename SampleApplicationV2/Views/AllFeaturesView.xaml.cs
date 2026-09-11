using SkiaSharpControlV2;
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SampleApplicationV2.Views
{
    /// <summary>
    /// One view that drives every public property, method, event and command of
    /// SkiaGridViewV2 - in particular the ones that had no coverage anywhere else in the
    /// sample application (context menus, header theming, exact row/header heights,
    /// declarative XAML SkButtons, bulk InsertRange/RemoveRange, CoalesceRowUpdates,
    /// indicator-only sorting, the MVVM command surface, the expand-state read APIs,
    /// targeted ToggleGroup/ToggleRow, per-grid automation, ExportData(All) and the
    /// scroll-bar metric callbacks).
    /// </summary>
    public partial class AllFeaturesView : UserControl
    {
        private readonly ObservableCollection<MyData> _items = new();
        private int _nextId = 1000;
        private int _logLines;

        public AllFeaturesView()
        {
            InitializeComponent();

            foreach (var d in RandomDataGenerator.Generate(60))
                _items.Add(d);
            // give every 4th row children so ChildProperty="Details" has something to expand
            for (int i = 0; i < _items.Count; i += 4)
                _items[i].Details = RandomDataGenerator.Generate(2);

            skiaGrid.ItemsSource = _items;

            WireCallbacks();
            BuildContextMenus();

            txtFont.Text = "12";
            txtRowH.Text = "auto";
            txtHdrH.Text = "auto";
            txtVBar.Text = "system";
            txtHBar.Text = "20";

            Loaded += (s, e) => UpdateDiagnostics();
            Log("ready - 60 rows, every 4th row has 2 child rows");
        }

        // ── every callback surface the control exposes ────────────────────
        private void WireCallbacks()
        {
            // Action-style delegates (dependency properties)
            skiaGrid.OnRowClicked = o => Log($"OnRowClicked        {Describe(o)}");
            skiaGrid.OnRowDoubleClicked = o => Log($"OnRowDoubleClicked  {Describe(o)}");
            skiaGrid.OnRowRightClicked = o => Log($"OnRowRightClicked   {Describe(o)}");
            skiaGrid.OnCellClicked = (o, c) => Log($"OnCellClicked       {Describe(o)} col={c?.Header}");
            skiaGrid.OnSkGridDoubleClicked = () => Log("OnSkGridDoubleClicked (grid surface)");
            skiaGrid.OnPreviewKeyDownEvent = k => Log($"OnPreviewKeyDown    {k}");
            skiaGrid.ColumnLeftClick = c => Log($"ColumnLeftClick     {c?.Header}");
            skiaGrid.ColumnRightClick = c => Log($"ColumnRightClick    {c?.Header}");
            skiaGrid.SortChanging = () => Log("SortChanging        (before the sort runs)");
            skiaGrid.SortChanged = (col, dir) => Log($"SortChanged         {col} -> {dir}");

            // Scroll metric callbacks (the v2.17.0 column-fitting contract)
            skiaGrid.VerticalScrollBarVisibilityChanged = shown =>
                Log($"VScrollVisibility   shown={shown} actualWidth={skiaGrid.VerticalScrollBarActualWidth:F0}px");
            skiaGrid.VerticalScrollBarPositionChanged = v => UpdateDiagnostics();
            skiaGrid.HorizontalScrollBarPositionChanged = v => UpdateDiagnostics();

            // ICommand surface
            skiaGrid.SelectionChangedCommand = new RelayCommand<object>(p =>
            {
                var n = (p as System.Collections.ICollection)?.Count ?? 0;
                Log($"SelectionChangedCmd {n} row(s) selected");
                UpdateDiagnostics();
            });
            skiaGrid.CellClickedCommand = new RelayCommand<object>(p => Log($"CellClickedCommand  {p}"));
            skiaGrid.CheckboxToggledCommand = new RelayCommand<object>(p => Log($"CheckboxToggledCmd  {p}"));
            skiaGrid.RowRightClickedCommand = new RelayCommand<object>(p => Log($"RowRightClickedCmd  {Describe(p)}"));
            skiaGrid.SortChangedCommand = new RelayCommand<object>(p => Log($"SortChangedCommand  {p}"));
            skiaGrid.ExpandChangedCommand = new RelayCommand<object>(p =>
            {
                if (p is SkExpandChangedEventArgs a)
                    Log($"ExpandChangedCmd    reason={a.Reason} affected={a.AffectedItems?.Count ?? 0}");
            });
            skiaGrid.RowDroppedCommand = new RelayCommand<object>(p =>
            {
                if (p is SKRowDroppedEventArgs a)
                    Log($"RowDroppedCommand   {a.Items.Count} row(s) -> insertIndex={a.InsertIndex}");
            });
            skiaGrid.RowDragCompletedCommand = new RelayCommand<object>(p =>
            {
                if (p is SKRowDragCompletedEventArgs a)
                    Log($"RowDragCompletedCmd effects={a.Effects} items={a.Items.Count}");
            });

            // CLR events
            skiaGrid.ExpandChanged += (s, a) =>
                Log($"ExpandChanged event reason={a.Reason} affected={a.AffectedItems?.Count ?? 0}");

            // the two XAML SkButtons declared in the Actions column
            foreach (var col in skiaGrid.Columns ?? new SkGridColumnCollection())
            {
                if (col is SKGridViewColumn c && c.Name == "Actions" && c.CellTemplate != null)
                {
                    foreach (var b in c.CellTemplate.SkButtons)
                    {
                        var name = b.Name;
                        b.OnClicked = (btn, data) => Log($"SkButton '{name}' clicked on {Describe(data)}");
                    }
                }
            }
        }

        private void BuildContextMenus()
        {
            // ItemsContextMenu - right-click over a data row (also the UIA IInvokeProvider path)
            var rowMenu = new ContextMenu();
            foreach (var (text, act) in new (string, Action)[]
            {
                ("Copy selected rows (TSV)", () => CopyToClipboard(SKExportType.Selected)),
                ("Select all rows", () => skiaGrid.SelectAllRows()),
                ("Scroll selected into view", () =>
                {
                    if (skiaGrid.SelectedItems.Count > 0) skiaGrid.ScrollIntoView(skiaGrid.SelectedItems[0]);
                }),
            })
            {
                var mi = new MenuItem { Header = text };
                mi.Click += (s, e) => { Log($"ItemsContextMenu    '{text}'"); act(); };
                rowMenu.Items.Add(mi);
            }
            skiaGrid.ItemsContextMenu = rowMenu;

            // HeaderContextMenu - right-click over a column header
            var hdrMenu = new ContextMenu();
            var miHide = new MenuItem { Header = "Hide the Category column" };
            miHide.Click += (s, e) => { ChkHideCategory.IsChecked = true; Log("HeaderContextMenu   hide Category"); };
            var miClear = new MenuItem { Header = "Clear sort" };
            miClear.Click += (s, e) => { skiaGrid.ClearSort(); Log("HeaderContextMenu   ClearSort()"); };
            hdrMenu.Items.Add(miHide);
            hdrMenu.Items.Add(miClear);
            skiaGrid.HeaderContextMenu = hdrMenu;

            // ContextMenu - right-click over the grid surface (outside any row)
            var surfaceMenu = new ContextMenu();
            var miAll = new MenuItem { Header = "Export whole grid to clipboard" };
            miAll.Click += (s, e) => { Log("ContextMenu         export all"); CopyToClipboard(SKExportType.All); };
            surfaceMenu.Items.Add(miAll);
            skiaGrid.ContextMenu = surfaceMenu;
        }

        // ── appearance ────────────────────────────────────────────────────
        private void Density_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (skiaGrid == null) return;
            skiaGrid.Density = Content(DensityBox) == "Normal"
                ? SKGridDensity.Normal : SKGridDensity.Compact;
            Log($"Density             {skiaGrid.Density}");
            UpdateDiagnostics();
        }

        private void FontSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (skiaGrid == null) return;
            skiaGrid.SKFontSize = (float)e.NewValue;
            txtFont.Text = ((int)e.NewValue).ToString();
            UpdateDiagnostics();
        }

        private void RowHeight_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (skiaGrid == null) return;
            skiaGrid.SKRowHeight = e.NewValue;
            txtRowH.Text = e.NewValue <= 0 ? "auto" : ((int)e.NewValue) + "px";
            UpdateDiagnostics();
        }

        private void HdrHeight_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (skiaGrid == null) return;
            skiaGrid.SKColumnHeaderHeight = e.NewValue;
            txtHdrH.Text = e.NewValue <= 0 ? "auto" : ((int)e.NewValue) + "px";
        }

        private void Appearance_Changed(object sender, RoutedEventArgs e)
        {
            if (skiaGrid == null) return;
            skiaGrid.ShowGridLines = ChkGridLines.IsChecked == true;
            skiaGrid.AlternatingRowBackground = ChkAltRows.IsChecked == true ? "#141425" : null;
            skiaGrid.SelectionStyle = ChkBorderSel.IsChecked == true
                ? SKSelectionStyle.Border : SKSelectionStyle.Fill;
            skiaGrid.IsDeferredScrollingEnabled = ChkDeferScroll.IsChecked == true;
        }

        private void Header_Changed(object sender, RoutedEventArgs e)
        {
            if (skiaGrid == null) return;
            skiaGrid.ColumnHeaderVisible = ChkHeaderVisible.IsChecked == true;
            bool themed = ChkHeaderTheme.IsChecked == true;
            skiaGrid.ColumnHeaderBackground = themed ? "#1F3864" : null;
            skiaGrid.ColumnHeaderForeground = themed ? "#FFD966" : null;
            skiaGrid.ColumnHeaderSeparatorColor = themed ? "#7FD1B9" : null;
            Log($"Header              visible={skiaGrid.ColumnHeaderVisible} themed={themed}");
            UpdateDiagnostics();
        }

        // ── columns ───────────────────────────────────────────────────────
        private SKGridViewColumn? Col(string name) =>
            skiaGrid.Columns?.OfType<SKGridViewColumn>().FirstOrDefault(c => c.Name == name);

        private void Columns_Changed(object sender, RoutedEventArgs e)
        {
            if (skiaGrid?.Columns == null) return;
            var price = Col("Price");
            if (price != null) price.ShowBracketOnNegative = ChkBrackets.IsChecked == true;
            var views = Col("Views");
            if (views != null) views.FormatWithAcronym = ChkAcronym.IsChecked == true;
            var qty = Col("Qty");
            if (qty != null) qty.ShowGroupAggregateData = ChkNoAggregate.IsChecked != true;
            var barcode = Col("Barcode");
            if (barcode != null) barcode.DataVisible = ChkDataVisible.IsChecked == true;
            var cat = Col("Category");
            if (cat != null) cat.IsVisible = ChkHideCategory.IsChecked != true;
            var nm = Col("Name");
            if (nm != null) nm.DisplayHeader = ChkDisplayHeader.IsChecked == true ? "PRODUCT NAME" : null;
            skiaGrid.Refresh();
        }

        // ── sorting ───────────────────────────────────────────────────────
        private void Sorting_Changed(object sender, RoutedEventArgs e)
        {
            if (skiaGrid == null) return;
            skiaGrid.UseCollectionViewSort = ChkIndicatorOnly.IsChecked != true;
            skiaGrid.SortEvery = ChkTimerSort.IsChecked == true ? 3 : (int?)null;
            Log($"UseCollectionViewSort={skiaGrid.UseCollectionViewSort}  SortEvery={skiaGrid.SortEvery}");
        }

        private void ClearSort_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ClearSort();
            Log("ClearSort()");
        }

        // ── filtering ─────────────────────────────────────────────────────
        private void FilterValue_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.AddValueFilter("Price", ">", "0", typeof(double));
            Log("AddValueFilter      Price > 0");
            UpdateDiagnostics();
        }

        private void FilterText_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.AddTextFilter("Name", "Item 1*");
            Log("AddTextFilter       Name matches 'Item 1*'");
            UpdateDiagnostics();
        }

        private void FilterList_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.AddListFilter("Category", new List<string> { "Category 1", "Category 2" });
            Log("AddListFilter       Category in [Category 1, Category 2]");
            UpdateDiagnostics();
        }

        private void FilterClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var c in new[] { "Price", "Name", "Category" })
                skiaGrid.RemoveFilter(c);
            Log("RemoveFilter        Price, Name, Category");
            UpdateDiagnostics();
        }

        // ── grouping ──────────────────────────────────────────────────────
        private void Group_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ApplyGroup("Category", new List<SKGroupField>
            {
                new SKGroupField { BindingPath = "Price",        Aggregation = SkAggregation.Sum,      TargetColumns = "Price" },
                new SKGroupField { BindingPath = "Quantity",     Aggregation = SkAggregation.Min,      TargetColumns = "Qty" },
                new SKGroupField { BindingPath = "Rating",       Aggregation = SkAggregation.Max,      TargetColumns = "Rating" },
                new SKGroupField { BindingPath = "Views",        Aggregation = SkAggregation.Avg,      TargetColumns = "Views" },
                new SKGroupField { BindingPath = "Id",           Aggregation = SkAggregation.Count,    TargetColumns = "Id" },
                new SKGroupField { BindingPath = "SupplierName", Aggregation = SkAggregation.Distinct, TargetColumns = "Supplier" },
            });
            Log("ApplyGroup          Category + Sum/Min/Max/Avg/Count/Distinct");
            UpdateDiagnostics();
        }

        private void GroupClear_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ClearGroup();
            Log("ClearGroup()");
            UpdateDiagnostics();
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e) { skiaGrid.ExpandAll(); Log("ExpandAll()"); }
        private void CollapseAll_Click(object sender, RoutedEventArgs e) { skiaGrid.CollapseAll(); Log("CollapseAll()"); }

        private void ToggleGroup_Click(object sender, RoutedEventArgs e)
        {
            var states = skiaGrid.GetGroupExpandStates();
            if (states.Count == 0) { Log("ToggleGroup         no groups - press ApplyGroup first"); return; }
            var first = states.Keys.First();
            bool now = skiaGrid.IsGroupExpanded(first);
            skiaGrid.ToggleGroup(first, !now);
            Log($"ToggleGroup('{first}', {!now})   IsGroupExpanded was {now}");
        }

        private void ReadGroupState_Click(object sender, RoutedEventArgs e)
        {
            var states = skiaGrid.GetGroupExpandStates();
            if (states.Count == 0) { Log("GetGroupExpandStates -> empty (not grouped)"); return; }
            Log("GetGroupExpandStates -> " +
                string.Join(", ", states.Select(kv => $"{kv.Key}={(kv.Value ? "open" : "closed")}")));
            Log($"GetVisibleDataItems -> {skiaGrid.GetVisibleDataItems().Count} data rows visible");
        }

        // ── tree rows ─────────────────────────────────────────────────────
        private void ExpandAllRows_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ExpandAllRows();
            Log($"ExpandAllRows()     {skiaGrid.GetExpandedRowItems().Count} parent rows open");
        }

        private void CollapseAllRows_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.CollapseAllRows();
            Log("CollapseAllRows()");
        }

        private void ToggleRow_Click(object sender, RoutedEventArgs e)
        {
            var item = skiaGrid.SelectedItems.FirstOrDefault() as MyData
                       ?? _items.FirstOrDefault(i => i.Details != null);
            if (item == null) { Log("ToggleRow           no expandable row found"); return; }
            bool now = skiaGrid.IsRowExpanded(item);
            skiaGrid.ToggleRow(item, !now);
            Log($"ToggleRow(Id {item.Id}, {!now})   IsRowExpanded was {now}");
        }

        private void ReadRowState_Click(object sender, RoutedEventArgs e)
        {
            var open = skiaGrid.GetExpandedRowItems();
            Log($"GetExpandedRowItems -> {open.Count} row(s): " +
                string.Join(", ", open.OfType<MyData>().Take(8).Select(m => "Id " + m.Id)));
        }

        // ── bulk row operations ───────────────────────────────────────────
        private void Coalesce_Changed(object sender, RoutedEventArgs e)
        {
            if (skiaGrid == null) return;
            skiaGrid.CoalesceRowUpdates = ChkCoalesce.IsChecked == true;
            Log($"CoalesceRowUpdates  {skiaGrid.CoalesceRowUpdates}");
        }

        private void InsertRange_Click(object sender, RoutedEventArgs e)
        {
            var batch = RandomDataGenerator.Generate(5);
            foreach (var d in batch) d.Id = _nextId++;
            bool ok = skiaGrid.InsertRange(2, batch.Cast<object>().ToList());
            Log($"InsertRange(2, 5)   -> {ok}   (one collection-changed raise, not five)");
            UpdateDiagnostics();
        }

        private void RemoveRange_Click(object sender, RoutedEventArgs e)
        {
            var sel = skiaGrid.SelectedItems.ToList();
            if (sel.Count == 0) { Log("RemoveRange         select some rows first"); return; }
            bool ok = skiaGrid.RemoveRange(sel);
            Log($"RemoveRange({sel.Count})    -> {ok}");
            UpdateDiagnostics();
        }

        private void InsertAtView_Click(object sender, RoutedEventArgs e)
        {
            var d = RandomDataGenerator.Generate(1)[0];
            d.Id = _nextId++;
            d.Name = "PINNED";
            bool ok = skiaGrid.InsertAtViewIndex(0, d);
            Log($"InsertAtViewIndex(0) -> {ok}  (bypasses sort and filter)");
            UpdateDiagnostics();
        }

        private void IndexOf_Click(object sender, RoutedEventArgs e)
        {
            var item = skiaGrid.SelectedItems.FirstOrDefault();
            if (item == null) { Log("IndexOfInView       select a row first"); return; }
            Log($"IndexOfInView       visual row index = {skiaGrid.IndexOfInView(item)}");
        }

        private void ScrollIntoView_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;
            bool ok = skiaGrid.ScrollIntoView(_items[_items.Count - 1]);
            Log($"ScrollIntoView(last) -> {ok}");
        }

        // ── scroll bars ───────────────────────────────────────────────────
        private void ScrollBars_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (skiaGrid == null) return;
            skiaGrid.VerticalScrollBarWidth = VBarSlider.Value;
            skiaGrid.HorizontalScrollBarHeight = HBarSlider.Value;
            txtVBar.Text = VBarSlider.Value <= 0 ? "system" : ((int)VBarSlider.Value) + "px";
            txtHBar.Text = HBarSlider.Value <= 0 ? "20" : ((int)HBarSlider.Value) + "px";
            UpdateDiagnostics();
        }

        private void ScrollV_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ScrollToVerticalOffset(25);
            Log("ScrollToVerticalOffset(25)");
        }

        private void ScrollH_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ScrollToHorizontalOffset(400);
            Log("ScrollToHorizontalOffset(400)");
        }

        // ── row drag ──────────────────────────────────────────────────────
        private void Drag_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (skiaGrid == null) return;
            skiaGrid.RowDragMode = Content(DragBox) switch
            {
                "Reorder" => SKRowDragMode.Reorder,
                "Notify" => SKRowDragMode.Notify,
                "DragOut" => SKRowDragMode.DragOut,
                _ => SKRowDragMode.None,
            };
            skiaGrid.RowDragIndicatorColor = "#FF6E00";
            Log($"RowDragMode         {skiaGrid.RowDragMode}   CanReorderRows={skiaGrid.CanReorderRows}");
        }

        // ── automation ────────────────────────────────────────────────────
        private void Automation_Changed(object sender, RoutedEventArgs e)
        {
            if (skiaGrid == null) return;
            skiaGrid.IsAutomationEnabled = ChkGridAutomation.IsChecked == true;
            SkiaGridViewV2.GlobalAutomationEnabled = ChkGlobalAutomation.IsChecked == true;
            Log($"Automation          perGrid={skiaGrid.IsAutomationEnabled} " +
                $"global={SkiaGridViewV2.GlobalAutomationEnabled}");
        }

        // ── export & diagnostics ──────────────────────────────────────────
        private void CopyToClipboard(SKExportType type)
        {
            var tsv = skiaGrid.ExportData(type);
            if (!string.IsNullOrEmpty(tsv))
            {
                try { Clipboard.SetText(tsv); } catch { /* clipboard can be locked */ }
            }
            var lines = string.IsNullOrEmpty(tsv) ? 0 : tsv.Split('\n').Length;
            Log($"ExportData({type})  {lines} line(s) on the clipboard");
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e) => CopyToClipboard(SKExportType.All);
        private void ExportSel_Click(object sender, RoutedEventArgs e) => CopyToClipboard(SKExportType.Selected);

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.SelectAllRows();
            Log($"SelectAllRows()     {skiaGrid.SelectedItems.Count} selected");
        }

        private void Percentiles_Click(object sender, RoutedEventArgs e)
        {
            GridMetrics.IsEnabled = true;
            GridMetrics.PercentilesEnabled = true;
            var all = GridMetrics.GetAllPercentiles();
            if (all.Count == 0) { Log("GetAllPercentiles   no samples yet - interact with the grid first"); return; }
            foreach (var kv in all.OrderByDescending(k => k.Value.P95Ms).Take(6))
                Log($"  {kv.Key,-28} p50={kv.Value.P50Ms:F2} p95={kv.Value.P95Ms:F2} " +
                    $"p99={kv.Value.P99Ms:F2} ms (n={kv.Value.SampleCount})");
        }

        private void OpenMainWindow_Click(object sender, RoutedEventArgs e)
        {
            var w = new MainWindow { Title = "MainWindow - bound column layout persistence" };
            w.Show();
            Log("opened MainWindow   (two-way bound Width / DisplayIndex / IsVisible / sort)");
        }

        // ── helpers ───────────────────────────────────────────────────────
        private static string Content(ComboBox cb) =>
            (cb.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        private static string Describe(object? o) =>
            o is MyData m ? $"Id {m.Id} '{m.Name}'" : (o?.ToString() ?? "null");

        private void UpdateDiagnostics()
        {
            txtDiag.Text =
                $"VisibleItemCount={skiaGrid.VisibleItemCount}   FilterCount={skiaGrid.FilterCount}   " +
                $"GroupCount={skiaGrid.GroupCount}   CanReorderRows={skiaGrid.CanReorderRows}   " +
                $"VerticalScrollBarActualWidth={skiaGrid.VerticalScrollBarActualWidth:F0}px";
        }

        private void Log(string line)
        {
            if (_logLines++ > 400) { LogText.Text = ""; _logLines = 0; }
            LogText.Text += $"{DateTime.Now:HH:mm:ss}  {line}\n";
            LogScroll.ScrollToEnd();
        }
    }
}

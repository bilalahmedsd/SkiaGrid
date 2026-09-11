using SkiaSharp;
using SkiaSharpControlV2;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class CollectionViewDemo : UserControl
    {
        private readonly CollectionViewDemoVM _vm = new();
        private readonly DispatcherTimer _refreshTimer;

        public CollectionViewDemo()
        {
            DataContext = _vm;
            InitializeComponent();

            // Load initial dataset
            foreach (var item in RandomDataGenerator.Generate(100))
                _vm.Items.Add(item);

            // Refresh timer keeps grid visual in sync + flushes any dirty incremental updates
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _refreshTimer.Tick += (s, e) => { skiaGrid.Refresh(); UpdateStatus(); };
            _refreshTimer.Start();

            UpdateStatus();

            // FIX #1 test (binding-set default sort): set the bound sort AFTER the grid has
            // loaded — the timing that used to drop it (restored/persisted settings arriving
            // late). With the fix the grid opens sorted by Name Asc.
            Dispatcher.BeginInvoke(new Action(() => _vm.DefaultNameSort = SkGridViewColumnSort.Ascending),
                DispatcherPriority.Loaded);
        }

        // ── Filter handlers ────────────────────────────────────────────

        private void TextFilter_Click(object sender, RoutedEventArgs e)
        {
            var pattern = txtTextFilter.Text?.Trim();
            if (string.IsNullOrEmpty(pattern)) return;
            skiaGrid.AddTextFilter("Name", pattern);
            skiaGrid.RefreshCollection(); // Force collection refresh + visual invalidate
            UpdateStatus();
            _vm.Status = $"Applied text filter: Name matches '{pattern}' | Visible: {skiaGrid.VisibleItemCount}/{_vm.Items.Count}";
        }

        private void ValueFilter_Click(object sender, RoutedEventArgs e)
        {
            var op = (cmbValueOp.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? ">";
            var val = txtValueFilter.Text?.Trim() ?? "0";
            skiaGrid.AddValueFilter("Price", op, val, typeof(double));
            skiaGrid.RefreshCollection();
            UpdateStatus();
            _vm.Status = $"Applied value filter: Price {op} {val} | Visible: {skiaGrid.VisibleItemCount}/{_vm.Items.Count}";
        }

        private void ListFilter_Click(object sender, RoutedEventArgs e)
        {
            var csv = txtListFilter.Text?.Trim();
            if (string.IsNullOrEmpty(csv)) return;
            var list = csv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            skiaGrid.AddListFilter("Category", list);
            skiaGrid.RefreshCollection();
            UpdateStatus();
            _vm.Status = $"Applied list filter: Category in [{string.Join(",", list)}] | Visible: {skiaGrid.VisibleItemCount}/{_vm.Items.Count}";
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.RemoveFilter("Name");
            skiaGrid.RemoveFilter("Price");
            skiaGrid.RemoveFilter("Category");
            skiaGrid.RefreshCollection();
            UpdateStatus();
            _vm.Status = $"All filters cleared | Visible: {skiaGrid.VisibleItemCount}/{_vm.Items.Count}";
        }

        // ── Sort handlers ──────────────────────────────────────────────

        private void AddSort_Click(object sender, RoutedEventArgs e)
        {
            var col = (cmbSortColumn.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Name";
            var dir = (cmbSortDir.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Asc";

            foreach (var gridCol in skiaGrid.Columns)
                gridCol.GridViewColumnSort = SkGridViewColumnSort.None;

            var match = skiaGrid.Columns.FirstOrDefault(c => c.BindingPath == col);
            if (match != null)
            {
                match.GridViewColumnSort = dir == "Asc"
                    ? SkGridViewColumnSort.Ascending
                    : SkGridViewColumnSort.Descending;
            }
            UpdateStatus();
        }

        private void ClearSort_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in skiaGrid.Columns)
                col.GridViewColumnSort = SkGridViewColumnSort.None;
            UpdateStatus();
        }

        private void LiveSort_Changed(object sender, RoutedEventArgs e)
        {
            _vm.IsLiveSort = chkLiveSort.IsChecked == true;
        }

        /// <summary>
        /// PS-scenario demonstration. Sets up the exact conditions of the PS-window bug
        /// the fix targets, then adds 8 deliberately scrambled-name rows so the user
        /// can see they all land at alphabetical positions despite Live Sort being OFF.
        ///
        /// Sequence:
        ///   1. Clear grid + reset scroll (fresh, predictable state).
        ///   2. Apply Name Asc sort (mirrors a binding-set GridViewColumnSort at startup).
        ///   3. Live Sort OFF (the worst-case PS scenario).
        ///   4. Add 8 rows with scrambled names in one synchronous burst.
        ///
        /// Expected result on screen: 8 rows in alphabetical order
        ///   Alpha, Bravo, Charlie, Delta, Echo, Foxtrot, Golf, Hotel
        /// — NOT in arrival order (Echo, Alpha, Foxtrot, ...).
        /// </summary>
        private void SortedInsertDemo_Click(object sender, RoutedEventArgs e)
        {
            // 1. Fresh state. Reset scroll explicitly so the first row is visible
            //    regardless of where the user had scrolled.
            _vm.Items.Clear();
            skiaGrid.ScrollToVerticalOffset(0);

            // 2. Sort by Name Asc — set Name column directly. The column's
            //    Column_PropertyChanged handler applies the sort to the CollectionView.
            foreach (var col in skiaGrid.Columns)
                col.GridViewColumnSort = SkGridViewColumnSort.None;
            var nameCol = skiaGrid.Columns.FirstOrDefault(c => c.BindingPath == "Name");
            if (nameCol != null) nameCol.GridViewColumnSort = SkGridViewColumnSort.Ascending;

            // 3. Live Sort OFF.
            chkLiveSort.IsChecked = false;
            _vm.IsLiveSort = false;

            // 4. Synchronously add 8 scrambled-name rows. Each Add goes through
            //    Source_CollectionChanged → InsertNewItem with sort active, so it lands
            //    at the right sort position — proves the IsLiveSort decoupling.
            string[] names = { "Echo", "Alpha", "Foxtrot", "Bravo", "Delta", "Charlie", "Golf", "Hotel" };
            foreach (var name in names)
            {
                var item = RandomDataGenerator.Generate(1)[0];
                item.Name = name;
                _vm.Items.Add(item);
            }

            // Force the grid to repaint immediately so the user doesn't have to wait for
            // the 250 ms refresh timer to flush.
            skiaGrid.Refresh();

            _vm.Status = $"Pushed 8 scrambled-name rows synchronously with Live Sort OFF. " +
                         $"Expected order on screen: Alpha, Bravo, Charlie, Delta, Echo, Foxtrot, Golf, Hotel.";
        }

        // ── GPS window launcher ────────────────────────────────────────

        /// <summary>
        /// Opens the dedicated standalone Global Position Summary demo window. Unlike the inline
        /// grouping on this tab (which starts ungrouped until "Apply Group" is clicked), that window
        /// opens ALREADY grouped by Account and sorted by Inst, so the grand-totals-at-top +
        /// one-subtotal-per-group behavior is visible immediately with no extra clicks.
        /// </summary>
        private void OpenGpsWindow_Click(object sender, RoutedEventArgs e)
        {
            var win = new GpsDemoWindow { Owner = Window.GetWindow(this) };
            win.Show();
        }

        // Drive selection the SAME way a FlaUI / UIA client does — NO coordinate mouse.
        // Creates the grid's automation peer, finds the Row_0 peer, and invokes its
        // ISelectionItemProvider.Select(). Proves a pure-UIA Select() (a) highlights the
        // row and (b) pushes synchronously into the TwoWay-bound VM SelectedItems (watch
        // the status bar's "Selected" count jump to 1), exactly like the P1 Basket flow.
        private void UiaSelectRow0_Click(object sender, RoutedEventArgs e)
        {
            var gridPeer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(skiaGrid);
            var children = gridPeer?.GetChildren();
            if (children == null) { _vm.Status = "UIA: no automation peer available"; return; }

            foreach (var child in children)
            {
                if (child.GetAutomationId() != "Row_0") continue;

                var provider = child.GetPattern(System.Windows.Automation.Peers.PatternInterface.SelectionItem)
                                   as System.Windows.Automation.Provider.ISelectionItemProvider;
                provider?.Select();
                _vm.Status = $"UIA Select(Row_0): peer.IsSelected={provider?.IsSelected}  |  " +
                             $"VM SelectedItems.Count={_vm.SelectedItems.Count}  (no coordinate mouse used)";
                skiaGrid.Refresh();
                return;
            }
            _vm.Status = "UIA: Row_0 peer not found (is the grid scrolled to the top?)";
        }

        // Flip the process-wide master switch. When false, EVERY grid's automation gates
        // short-circuit (EffectiveAutomationEnabled = IsAutomationEnabled && GlobalAutomationEnabled),
        // so a grid with a cached peer exposes an empty child list and an un-queried grid
        // returns no peer at all. Click "UIA Select Row 0" after toggling OFF to see the
        // peer tree disappear; toggle back ON to restore. Normally set once at startup.
        private void ToggleGlobalAutomation_Click(object sender, RoutedEventArgs e)
        {
            SkiaGridViewV2.GlobalAutomationEnabled = !SkiaGridViewV2.GlobalAutomationEnabled;
            _vm.Status = $"GlobalAutomationEnabled = {SkiaGridViewV2.GlobalAutomationEnabled} " +
                         "(process-wide UIA master switch; per-grid IsAutomationEnabled still applies when true)";
        }

        // ── Group handlers ─────────────────────────────────────────────

        private void ApplyGroup_Click(object sender, RoutedEventArgs e)
        {
            var col = (cmbGroupColumn.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Category";

            // Use the public overload that takes aggregations — no direct CV access needed.
            skiaGrid.ApplyGroup(col, new[]
            {
                new SKGroupField { BindingPath = "Price",        Aggregation = SkAggregation.Sum,      TargetColumns = "Price" },
                new SKGroupField { BindingPath = "Price",        Aggregation = SkAggregation.Avg,      TargetColumns = "Discount" },
                new SKGroupField { BindingPath = "Price",        Aggregation = SkAggregation.Min,      TargetColumns = "Quantity" },
                new SKGroupField { BindingPath = "Price",        Aggregation = SkAggregation.Max,      TargetColumns = "Rating" },
                new SKGroupField { BindingPath = "Id",           Aggregation = SkAggregation.Count,    TargetColumns = "Id" },
                new SKGroupField { BindingPath = "SupplierName", Aggregation = SkAggregation.Distinct, TargetColumns = "Supplier" },
            });
            UpdateStatus();
        }

        private void ClearGroup_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ClearGroup();
            UpdateStatus();
        }

        /// <summary>
        /// Reproduces the P1 Global Position Summary "double subtotal in group 2" scenario:
        /// group by Category with a Distinct HeaderField on SupplierName, then sort by the
        /// Supplier column (which has ShowSubTotalOnSort=True). The grand-total (header)
        /// subtotal rows must appear ONCE at the very top; each group then shows exactly one
        /// subtotal row per distinct supplier immediately after its header. Before the fix the
        /// grand-total block was re-emitted at the top of every group, so groups after the
        /// first showed a duplicated subtotal at their top (visually the bottom of the prior
        /// group). After the fix each group has a single, correctly-placed subtotal set.
        /// </summary>
        private void GpsSubtotalDemo_Click(object sender, RoutedEventArgs e)
        {
            // Clear any active filters so the grouped structure is unobstructed.
            skiaGrid.RemoveFilter("Name");
            skiaGrid.RemoveFilter("Price");
            skiaGrid.RemoveFilter("Category");

            // Group by Category with aggregations, including the Distinct on SupplierName that
            // (together with ShowSubTotalOnSort on the Supplier column) drives distinct subtotals.
            skiaGrid.ApplyGroup("Category", new[]
            {
                new SKGroupField { BindingPath = "Price",        Aggregation = SkAggregation.Sum,      TargetColumns = "Price" },
                new SKGroupField { BindingPath = "Id",           Aggregation = SkAggregation.Count,    TargetColumns = "Id" },
                new SKGroupField { BindingPath = "SupplierName", Aggregation = SkAggregation.Distinct, TargetColumns = "Supplier" },
            });

            // Sort by the Supplier column → triggers the distinct-subtotal flatten path.
            foreach (var col in skiaGrid.Columns)
                col.GridViewColumnSort = SkGridViewColumnSort.None;
            var supplierCol = skiaGrid.Columns.FirstOrDefault(c => c.BindingPath == "SupplierName");
            if (supplierCol != null) supplierCol.GridViewColumnSort = SkGridViewColumnSort.Ascending;

            skiaGrid.Refresh();
            UpdateStatus();
            _vm.Status = "GPS repro: grouped by Category + sorted by Supplier (ShowSubTotalOnSort). " +
                         "Grand totals appear once at the top; each group shows one subtotal per supplier.";
        }

        private void FilterByGroup_Changed(object sender, RoutedEventArgs e)
        {
            // The DP callback (FilterByGroupChanged on the grid) syncs to the CV
            // and triggers a refresh, so just setting the DP is enough.
            skiaGrid.FilterByGroup = chkFilterByGroup.IsChecked == true;
            UpdateStatus();
        }

        private void BottomInsert_Changed(object sender, RoutedEventArgs e)
        {
            // AddNewRowAtBottomInGroupChanged callback syncs to CV automatically.
            skiaGrid.AddNewRowAtBottomInGroup = chkBottomInsert.IsChecked == true;
            UpdateStatus();
        }

        // ── Row operations (via source collection) ─────────────────────

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var items = RandomDataGenerator.Generate(1);
            items[0].Name = $"** NEW {DateTime.Now:HH:mm:ss} **";
            _vm.Items.Add(items[0]);
            _vm.Status = $"Added item: {items[0].Name}";
        }

        private void InsertRow_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedItems.Count != 1) return;
            var selected = (MyData)_vm.SelectedItems[0];

            // Find where the selected row appears on screen. With sort/filter active this
            // does NOT equal its index in the source collection — we need the visual index.
            int targetViewIndex = skiaGrid.IndexOfInView(selected);
            if (targetViewIndex < 0) return;

            var newItem = RandomDataGenerator.Generate(1)[0];
            newItem.Name = "** Inserted **";

            // Use the grid-level InsertAtViewIndex API: the new row is placed at the exact
            // visual row position requested (above the selected row), bypassing both the
            // active sort and any active filter for this insert. Source collection stays
            // consistent — the grid adds the item to it.
            bool ok = skiaGrid.InsertAtViewIndex(targetViewIndex, newItem);
            if (!ok)
            {
                _vm.Status = $"InsertAtViewIndex failed (grouping active or ItemsSource not IList).";
                return;
            }

            skiaGrid.SelectedItems.Clear();
            skiaGrid.SelectedItems.Add(newItem);
            skiaGrid.ScrollIntoView(newItem);
            _vm.Status = $"Inserted '{newItem.Name}' at visual row {targetViewIndex} (above selected). " +
                         $"Sort/filter were bypassed for this insert — the row appears exactly where you clicked.";
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _vm.SelectedItems.ToList())
                _vm.Items.Remove((MyData)item);
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedItems.Count != 1) return;
            var idx = _vm.Items.IndexOf((MyData)_vm.SelectedItems[0]);
            if (idx > 0) _vm.Items.Move(idx, idx - 1);
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedItems.Count != 1) return;
            var idx = _vm.Items.IndexOf((MyData)_vm.SelectedItems[0]);
            if (idx >= 0 && idx < _vm.Items.Count - 1) _vm.Items.Move(idx, idx + 1);
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var text = skiaGrid.ExportData(SKExportType.Selected);
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        // ── Status bar ─────────────────────────────────────────────────

        private void UpdateStatus()
        {
            int total = _vm.Items.Count;
            int visible = skiaGrid.VisibleItemCount;
            int filters = skiaGrid.FilterCount;
            int groups = skiaGrid.GroupCount;

            _vm.Status = $"Source: {total}  |  Visible: {visible}  |  Filters: {filters}  |  Groups: {groups}  " +
                         $"|  Selected: {_vm.SelectedItems.Count}  |  LiveSort: {_vm.IsLiveSort}  " +
                         $"|  FilterByGroup: {chkFilterByGroup?.IsChecked == true}  |  BottomInsert: {chkBottomInsert?.IsChecked == true}";
        }
    }

    public class CollectionViewDemoVM : INotifyPropertyChanged
    {
        public ObservableCollection<MyData> Items { get; set; } = new();
        public ObservableCollection<object> SelectedItems { get; set; } = new();

        // ── Image rendering demo ──────────────────────────────────────
        // Load logo images once and reuse (ButtonRenderer caches SKBitmap internally)
        private static readonly ImageSource LogoImage = new BitmapImage(new Uri("P1_logo_PNG.png", UriKind.Relative));
        private static readonly ImageSource LogoImageAlt = new BitmapImage(new Uri("logop1.png", UriKind.Relative));
        private static readonly ImageSource CheckIcon = new BitmapImage(new Uri("P1_logo_low.jpg", UriKind.Relative));

        /// <summary>Factory: returns a logo image per row. Alternates between two logos based on Id parity.</summary>
        public Func<object, List<SkButton>> LogoFactory => (object data) =>
        {
            if (data is not MyData d) return new List<SkButton>();

            var logo = d.Id % 2 == 0 ? LogoImage : LogoImageAlt;
            return new List<SkButton>
            {
                new SkButton
                {
                    Name = "Logo",
                    Text = "",
                    Width = 50,
                    ImageSource = logo,
                    BackgroundColor = "#0F0F1A",
                    BorderColor = "#4A4A6A"
                }
            };
        };

        /// <summary>
        /// Factory: action buttons. Demonstrates the new <c>SkButton.IsVisible</c> property —
        /// the Edit button is always created but its visibility is driven by the row's
        /// <c>IsActive</c> state. When invisible, the Delete button collapses left into
        /// its slot (no gap).
        /// </summary>
        public Func<object, List<SkButton>> ActionButtonFactory => (object data) =>
        {
            if (data is not MyData d) return new List<SkButton>();

            return new List<SkButton>
            {
                new SkButton
                {
                    Name = "EditBtn",
                    Text = "✎",
                    Width = 28,
                    MarginLeft = 2,
                    ImageSource = CheckIcon,
                    ForegroundColor = "#4A90D9",
                    BackgroundColor = "#0F0F1A",
                    HoverBackgroundColor = "#4A90D9",   // ← hover: fill blue,
                    HoverForegroundColor = "#FFFFFF",   //           text white
                    IsVisible = d.IsActive,    // ← per-row visibility via IsVisible
                    // Per-row, per-button tooltip via property path (needs grid IsTooltipEnabled).
                    TooltipPath = "Name",
                },
                new SkButton
                {
                    Name = "DeleteBtn",
                    Text = "✕",
                    Width = 28,
                    MarginLeft = 2,
                    ForegroundColor = "#FF4444",
                    BackgroundColor = "#1A0A0A",
                    HoverBackgroundColor = "#FF4444",   // ← hover: fill red,
                    HoverForegroundColor = "#FFFFFF",   //           text white
                    // Per-row, per-button tooltip via code callback — takes the row data item.
                    // Moving between the two buttons in this cell updates the tooltip.
                    TooltipProvider = o => o is MyData md ? $"Delete ‘{md.Name}’ (#{md.Id})" : null,
                },
            };
        };

        // ── Custom draw demo: 10 red/green trend boxes per row ───────────
        //
        // Paints are cached statically — created once, reused for every cell every frame.
        // Don't allocate inside the drawer; per-cell allocations show up in CounterCellsDrawn
        // very quickly with a streaming grid.
        private static readonly SKPaint _trendUp    = new() { Color = SKColor.Parse("#1ED760"), Style = SKPaintStyle.Fill, IsAntialias = false };
        private static readonly SKPaint _trendDown  = new() { Color = SKColor.Parse("#FF4444"), Style = SKPaintStyle.Fill, IsAntialias = false };
        private static readonly SKPaint _trendEmpty = new() { Color = SKColor.Parse("#2A2A40"), Style = SKPaintStyle.Fill, IsAntialias = false };

        /// <summary>
        /// CustomDraw delegate for the Trend column. Renders the row's last 10 ticks as
        /// small red/green squares from left (oldest) to right (newest). Empty slots
        /// (when fewer than 10 ticks exist) show as muted grey.
        /// </summary>
        public Action<SkCellDrawContext> TrendBoxes => ctx =>
        {
            if (ctx.Data is not MyData d) return;

            const int boxCount = 10;
            const float pad = 1f;
            const float gap = 2f;

            float totalGap = gap * (boxCount - 1);
            float boxW = (ctx.Bounds.Width - pad * 2 - totalGap) / boxCount;
            float boxH = ctx.Bounds.Height - pad * 2;
            float y    = ctx.Bounds.Top + pad;

            var series = d.Trend;
            int start = Math.Max(0, series.Count - boxCount);

            for (int i = 0; i < boxCount; i++)
            {
                float x = ctx.Bounds.Left + pad + i * (boxW + gap);
                int srcIdx = start + i;
                SKPaint paint = srcIdx < series.Count
                    ? (series[srcIdx] ? _trendUp : _trendDown)
                    : _trendEmpty;
                ctx.Canvas.DrawRect(x, y, boxW, boxH, paint);
            }
        };

        private bool _isLiveSort = true;
        public bool IsLiveSort
        {
            get => _isLiveSort;
            set { _isLiveSort = value; OnPropertyChanged(); }
        }

        // FIX #1 test: bound sort direction for the Name column. Starts None; the view sets
        // it to Ascending at DispatcherPriority.Loaded to reproduce late-arriving persisted
        // settings. Typed as SkGridViewColumnSort so the GridViewColumnSort binding matches.
        private SkGridViewColumnSort _defaultNameSort = SkGridViewColumnSort.None;
        public SkGridViewColumnSort DefaultNameSort
        {
            get => _defaultNameSort;
            set { _defaultNameSort = value; OnPropertyChanged(); }
        }

        private string _status = "";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
    }
}

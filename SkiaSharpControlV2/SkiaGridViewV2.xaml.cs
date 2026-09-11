
using SkiaSharp;


using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;
using SkiaSharpControlV2.Renderer;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;


namespace SkiaSharpControlV2
{
    /// <summary>
    /// Interaction logic for SkiaGridViewV2.xaml
    /// </summary>

    public partial class SkiaGridViewV2 : UserControl, IDisposable
    {
        internal SkiaRenderer SkiaRenderer;
        private Automation.SkiaGridViewV2AutomationPeer? _automationPeer;
        private ReflectionHelper reflectionHelper;
        private Services.GridAppearanceConfig _appearanceConfig;
        private Managers.FilterManager _filterManager;
        internal Managers.SortingManager _sortingManager;
        internal Managers.SelectionManager _selectionManager;
        private Input.KeyboardHandler _keyboardHandler;
        private Input.MouseHandler _mouseHandler;
        internal Input.RowDragController _rowDragController;
        internal Managers.ScrollManager _scrollManager;
        internal Managers.GroupingManager _groupingManager;
        internal Managers.GridColumnManager _columnManager;
        internal Managers.DataFlatteningService _flatteningService;
        // SortTimer moved to SortingManager (Step 8)
        #region Properties
        // ScrollOffset state delegated to ScrollManager (Step 11) — thin accessors for backward compat
        internal float ScrollOffsetX
        {
            get => _scrollManager?.ScrollOffsetX ?? 0;
            set { if (_scrollManager != null) _scrollManager.ScrollOffsetX = value; }
        }
        internal float ScrollOffsetY
        {
            get => _scrollManager?.ScrollOffsetY ?? 0;
            set { if (_scrollManager != null) _scrollManager.ScrollOffsetY = value; }
        }
        internal float RowHeight => _appearanceConfig?.RowHeight ?? 14f;
        internal int TotalRows;
        // Selection state delegated to SelectionManager (Step 9) — these are thin accessors for backward compat until Step 10 migrates keyboard handler
        private int lastSelectedRowIndex
        {
            get => _selectionManager?.LastSelectedRowIndex ?? 0;
            set { if (_selectionManager != null) _selectionManager.LastSelectedRowIndex = value; }
        }
        private int? _selectionAnchorIndex
        {
            get => _selectionManager?.SelectionAnchorIndex;
            set { if (_selectionManager != null) _selectionManager.SelectionAnchorIndex = value; }
        }

        const string TIMERBASED_SORTING_COLOR = "#008040";
        const string NORMAL_GRID_COLUMN_COLOR = "#FF343434";
        const string FILTER_COLOR = "#0072C6";
        private ScrollViewer DataListViewScroll { get => Helper.FindScrollViewer(DataListView); }
        internal ICustomCollectionView? _collectionView;
        // Main IsBusy moved to ScrollManager (Step 11). This is a local guard for column width updates.
        private bool IsBusy { get; set; } = false;
        private float Scale { get; set; }
        // B4 fix: _isDirty removed (was never read, only written)

        // Toggle state delegated to GroupingManager (Step 12)
        internal Dictionary<string, (bool IsExpended, float x, float y, float height, float width)> GroupToggleDetails
            => _groupingManager?.GroupToggleDetails ?? new();
        internal Dictionary<object, (float x, float y, float height, float width)> RowToggleDetails
            => _groupingManager?.RowToggleDetails ?? new();
        internal Dictionary<(object, string name), (float x, float y, float height, float width, SkButton btn)> ButtonDetails = new();
        #endregion Properties
        public SkiaGridViewV2()
        {
            InitializeComponent();
            DataListView.ItemsSource = new List<string> { "" };
            Columns = new();
            reflectionHelper = new();
            SkiaRenderer = new(reflectionHelper);
            _appearanceConfig = new(SkiaRenderer);
            _columnManager = new(this);
            _flatteningService = new(reflectionHelper);
            _groupingManager = new(
                _flatteningService,
                () => _collectionView,
                () => GroupSettings,
                () => Columns,
                () => SkiaRenderer,
                () => ChildProperty,
                () => UpdateTotalRows(),
                () => Refresh());
            _groupingManager.ExpandChangedCallback = OnExpandChanged;
            _groupingManager.ExpandListenersActive = () => HasExpandListeners;
            _scrollManager = new(
                () => HorizontalScrollViewer,
                () => VerticalScrollViewer,
                () => MainGrid,
                () => RowBandHeight(),
                () => GetVisibleColumnsWidth(),
                () => TotalRows,
                () => RowHeight,
                () => DataListViewScroll,
                () => HorizontalScrollBarVisible,
                () => VerticalScrollBarVisible,
                () => IsDeferredScrollingEnabled,
                () => HorizontalScrollBarPositionChanged,
                () => VerticalScrollBarPositionChanged,
                () => VerticalScrollBarVisibilityChanged,
                () => SkiaCanvas.InvalidateVisual());
            _selectionManager = new(
                () => SkiaCanvas.InvalidateVisual(),
                (items) => SkiaRenderer.UpdateSelectedItems(items));
            _mouseHandler = new(this, _selectionManager);
            _rowDragController = new(
                () => RowDragMode,
                () => RowDragViewItems(),
                () => RowHeight,
                () => ScrollOffsetY,
                () => RowBandHeight(),
                () => SelectedItems,
                item => IsRowDraggable(item),
                direction => ScrollRowsForDrag(direction),
                () => SkiaCanvas.InvalidateVisual(),
                args => RaiseRowDragStarting(args),
                args => RaiseRowDropped(args),
                (insertIndex, rowIndexes) => PublishRowDragVisual(insertIndex, rowIndexes),
                (items, rowIndex, column) => BeginExternalRowDrag(items, rowIndex, column));
            _keyboardHandler = new(
                // Use flattened items (includes expanded children) for keyboard navigation, not raw collection view
                () => GroupSettings == null
                    ? SkiaRenderer.Items?.Select(x => x.Item)?.Cast<object>().ToList() ?? new()
                    : SkiaRenderer.GroupItemSource?.Where(x => x.IsExpanded || x.IsGroupHeader).Select(x => (object)(x.Item ?? x)).ToList() ?? new(),
                () => SelectedItems,
                _selectionManager,
                () => ScrollOffsetY,
                () => RowHeight,
                () => TotalRows,
                () => VerticalScrollViewer?.ViewportSize,
                (offset) => ScrollToVerticalOffset(offset),
                () => SelectAllRows(),
                () => ExportData(SKExportType.Selected),
                () => SkiaCanvas.InvalidateVisual(),
                () => SkiaCanvas,
                () => OnPreviewKeyDownEvent,
                () => RaiseAutomationSelectionChanged());
            _sortingManager = new(
                () => _collectionView,
                () => Columns,
                () => DataListView,
                () => ResetGroupToggleValues(),
                () => SortChanged,
                () => SortChanging,
                () => SortChangedCommand,
                () => UseCollectionViewSort);
            _filterManager = new(
                () => _collectionView,
                () => Columns,
                () => DataListView,
                () => ScrollToVerticalOffset(0),
                () => SortEvery,
                (color, col) => _sortingManager.UpdateTimerbaseSortingColumnColor(color, col),
                () => ColumnHeaderBackground);

            Loaded += (s, e) =>
            {
                SetScale();
                UpdateColumnsInDataGrid();
                // Scroll values FIRST, then the canvas — same order as QueueGridMetricsRecalculation().
                // UpdateScrollValues() settles each bar's auto-visibility, and GetSkiaWidth() sizes the
                // canvas by subtracting the vertical bar. Sizing first meant that when the bar turned
                // visible here the canvas kept the full width and the bar drew over the last column.
                UpdateScrollValues();
                UpdateSkiaGrid();
                if (Columns == null || Columns?.Count == 0)
                {
                    TryGenerateColumns(ItemsSource);
                }
                else
                {
                    foreach (var item in Columns!)
                    {
                        if (item?.CellTemplate?.SkButton != null)
                        {
                            if (string.IsNullOrWhiteSpace(item?.CellTemplate?.SkButton.Name))
                            {
                                throw new InvalidOperationException("SkButton.Name is required.");
                            }
                        }
                        if (item?.CellTemplate?.SkButtons.Count > 0)
                        {
                            foreach (var item1 in item.CellTemplate.SkButtons)
                            {
                                if (string.IsNullOrWhiteSpace(item1.Name))
                                {
                                    throw new InvalidOperationException("SkButton.Name is required.");
                                }
                            }
                        }
                    }
                }
                SkiaRenderer.UpdateSelectedItems(SelectedItems);
                SkiaRenderer.SetColumns(Columns!);
                SkiaRenderer.SetGroup(GroupSettings);
                if (GroupSettings != null)
                {
                    if (string.IsNullOrEmpty(GroupSettings.GroupBy))
                    {
                        throw new InvalidOperationException("GroupSettings.GroupBy is required when GroupSettings is set.");
                    }
                    if (_collectionView != null)
                    {
                        _collectionView.ClearGroup();
                        _collectionView.ApplyGroup(GroupSettings.GroupBy);
                        _collectionView.GroupFields = GroupSettings?.HeaderFields?.ToList();
                    }
                }

                if (_collectionView != null)
                {
                    _collectionView.CollectionChanged -= CollectionViewChanged;
                    _collectionView.CollectionChanged += CollectionViewChanged;
                    _collectionView.IsLiveSort = IsLiveSorting;
                    _collectionView.AddNewRowAtBottomInGroup = AddNewRowAtBottomInGroup;
                    _collectionView.FilterByGroup = FilterByGroup;
                }
                UpdateCollection();
                UpdateTotalRows();


                SkiaRenderer.UpdateVisibleColumns();
                SkiaRenderer.SetScrollBars(HorizontalScrollViewer, VerticalScrollViewer);
                _appearanceConfig.SetShowGridLines(ShowGridLines);
                _appearanceConfig.SetFontSize(SKFontSize);
                // Density first (it picks the font-size padding pair), then the explicit pixel
                // overrides, which win over it. Applied here and not only in the DP callbacks so a
                // value equal to the DP default — or one set before the control loaded — still lands.
                _appearanceConfig.SetDensity(Density);
                _appearanceConfig.SetRowHeightOverride(SKRowHeight);
                _appearanceConfig.SetColumnHeaderHeightOverride(SKColumnHeaderHeight);
                // The XAML header row carries a literal height; push the computed one over it.
                // Without this, a grid that never assigns SKFontSize kept the stale XAML literal.
                ApplyColumnHeaderHeight();
                // Same story for the two scroll bars — the horizontal one carries a XAML literal
                // height, and a thickness set before Loaded never reached the config.
                _appearanceConfig.SetVerticalScrollBarWidth(VerticalScrollBarWidth);
                _appearanceConfig.SetHorizontalScrollBarHeight(HorizontalScrollBarHeight);
                ApplyScrollBarMetrics();
                // A non-default thickness changes the canvas width and both viewports, which the
                // UpdateSkiaGrid()/UpdateScrollValues() above computed from the OLD thickness. One
                // post-layout pass fixes that; skipped when neither DP is set, so the default load
                // path stays exactly as it was.
                if (VerticalScrollBarWidth > 0 || HorizontalScrollBarHeight > 0)
                    QueueGridMetricsRecalculation();
                _appearanceConfig.SetFontFamily(SKFontFamily);
                // Header text must use the same face/size/style as the Skia rows. Only the DP
                // callbacks used to do this, so a grid relying on the defaults got WPF's font here.
                ApplyDataGridFont();
                _appearanceConfig.SetFontStyle(SKFontStyle);
                _appearanceConfig.SetGridLinesColor(GridLinesColor);
                _appearanceConfig.SetForegroundColor(ForegroundColor);
                _appearanceConfig.SetRowBackground(RowBackground);
                _appearanceConfig.SetAlternatingRowBackground(AlternatingRowBackground);
                SkiaRenderer.SetGroupRowBackgroundColor(GroupSettings?.RowBackground);
                SkiaRenderer.SetGroupFontColor(GroupSettings?.ForegroundColor);
                // Apply selected-row colors here too, in case they were binding-set before
                // the renderer existed (the DP callbacks no-op while SkiaRenderer is null).
                // Null → keep the built-in defaults (#0072C6 bg / white text).
                SkiaRenderer.SetSelectedRowBackground(SelectedRowBackground);
                SkiaRenderer.SetSelectedRowForeground(SelectedRowForeground);
                SubscribeToGroupColumnEvents(GroupSettings);

                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Activated += ParentWindow_Activated;
                    parentWindow.Deactivated += ParentWindow_Deactivated;
                }
                DataListView.AddHandler(
                       DataGridColumnHeader.PreviewMouseDoubleClickEvent,
                       new MouseButtonEventHandler((sender, e) =>
                       {
                           if (e.OriginalSource is FrameworkElement element &&
                               element.TemplatedParent is System.Windows.Controls.Primitives.Thumb)
                           {
                               e.Handled = true; // Cancel default resizing
                           }
                       }),
                       true);
                _sortingManager.SubscribeTimer();
                if (SortEvery.HasValue && SortEvery.Value > 0)
                {
                    _sortingManager.StartTimer(SortEvery.Value);
                }


            };
            Unloaded += (s, e) =>
            {
                if (_collectionView != null)
                    _collectionView.CollectionChanged -= CollectionViewChanged;

                _sortingManager.UnsubscribeTimer();

                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Activated -= ParentWindow_Activated;
                    parentWindow.Deactivated -= ParentWindow_Deactivated;
                }
            };
            SizeChanged += (s, o) =>
            {
                SetScale();
                // Scroll values before the canvas — see the Loaded handler above. A resize is the most
                // common way the bar crosses its auto-visibility threshold, so this order matters most
                // here.
                UpdateScrollValues();
                UpdateSkiaGrid();
                UpdateHorizontalScroll();
                UpdateVerticalScroll();
            };

        }

        // ── UI Automation ───────────────────────────────────────────────

        /// <summary>
        /// Process-wide master switch for UI Automation across ALL <see cref="SkiaGridViewV2"/>
        /// instances. Default <c>true</c> (current behavior). Set it once at app startup (e.g. from
        /// app config) to enable/disable UIA for every grid from a single place.
        /// <para>
        /// Semantics — master AND per-instance: the effective state of a grid is
        /// <c>GlobalAutomationEnabled &amp;&amp; IsAutomationEnabled</c>. When
        /// <c>GlobalAutomationEnabled == false</c>, NO grid exposes its UIA peer tree regardless of
        /// its per-grid <see cref="IsAutomationEnabled"/>. When <c>true</c>, the per-grid
        /// <see cref="IsAutomationEnabled"/> decides (default <c>true</c>).
        /// </para>
        /// Additive and default-<c>true</c>, so with no change to this switch behavior is
        /// byte-for-byte unchanged.
        /// </summary>
        public static bool GlobalAutomationEnabled { get; set; } = true;

        /// <summary>
        /// The effective automation state for THIS grid: the per-instance
        /// <see cref="IsAutomationEnabled"/> AND the process-wide
        /// <see cref="GlobalAutomationEnabled"/> master switch. All automation gates route through
        /// this rather than the raw DP so the master switch applies everywhere.
        /// </summary>
        internal bool EffectiveAutomationEnabled => IsAutomationEnabled && GlobalAutomationEnabled;

        /// <summary>
        /// Enables/disables this grid's UI Automation peer tree. Default <c>true</c>.
        /// Kill-switch for very large grids when no automation client needs them: with a
        /// UIA listener attached (screen reader, Accessibility Insights, ambient event
        /// hooks), every StructureChanged event triggers a peer-tree walk whose cost
        /// scales with the number of exposed rows. When <c>false</c>:
        ///   * grids that have not yet been queried return NO peer at all;
        ///   * grids whose peer already exists (WPF caches peers per element — they cannot
        ///     be un-created) expose an empty children list and stop raising structure
        ///     events, collapsing every walk to O(1).
        /// Toggling back to <c>true</c> at runtime restores the tree on the next client query.
        /// </summary>
        public bool IsAutomationEnabled
        {
            get => (bool)GetValue(IsAutomationEnabledProperty);
            set => SetValue(IsAutomationEnabledProperty, value);
        }

        public static readonly DependencyProperty IsAutomationEnabledProperty =
            DependencyProperty.Register(nameof(IsAutomationEnabled), typeof(bool), typeof(SkiaGridViewV2),
                new PropertyMetadata(true, IsAutomationEnabledChanged));

        private static void IsAutomationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid) return;
            // Enabling: attached clients must re-walk (children go empty → populated).
            // Disabling: one final forced raise lets clients drop the now-empty subtree.
            grid._automationPeer?.NotifyStructureChanged(force: true);
        }

        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            // No peer at all when automation is disabled up front. WPF only caches
            // non-null peers, so a later toggle to true creates the peer lazily on the
            // next client query.
            if (!EffectiveAutomationEnabled) return null!;
            _automationPeer = new Automation.SkiaGridViewV2AutomationPeer(this);
            return _automationPeer;
        }

        #region Columns
        public SkGridColumnCollection Columns
        {
            get => (SkGridColumnCollection)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(SkGridColumnCollection), typeof(SkiaGridViewV2), new PropertyMetadata(null, OnColumnsChanged));

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }



        #endregion Columns

        #region ChildRowTemplate


        #endregion ChildRowTemplate



        #region GroupColumns
        public SKGroupDefinition GroupSettings
        {
            get => (SKGroupDefinition)GetValue(GroupSettingsProperty);
            set => SetValue(GroupSettingsProperty, value);
        }

        public static readonly DependencyProperty GroupSettingsProperty =
            DependencyProperty.Register(nameof(GroupSettings), typeof(SKGroupDefinition), typeof(SkiaGridViewV2), new PropertyMetadata(null));



        #endregion GroupColumns

        #region ItemSource
        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(SkiaGridViewV2), new PropertyMetadata(default, OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid)
                return;

            grid._collectionView = new CustomCollectionView((IEnumerable)e.NewValue, grid.reflectionHelper);



            grid._collectionView.IsLiveSort = grid.IsLiveSorting;
            grid._collectionView.AddNewRowAtBottomInGroup = grid.AddNewRowAtBottomInGroup;
            grid._collectionView.CollectionChanged -= grid.CollectionViewChanged;
            grid._collectionView.CollectionChanged += grid.CollectionViewChanged;
            grid._collectionView.FilterByGroup = grid.FilterByGroup;
            grid._collectionView.GroupFields = grid.GroupSettings?.HeaderFields?.ToList();
            if (grid.Columns != null)
            {
                var sortcolumn = grid.Columns.LastOrDefault(x => x.GridViewColumnSort != SkGridViewColumnSort.None);
                if (sortcolumn?.BindingPath != null && sortcolumn?.GridViewColumnSort != null)
                    grid.ApplySort(sortcolumn.BindingPath, sortcolumn.GridViewColumnSort == SkGridViewColumnSort.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
            }

            grid.lastSelectedRowIndex = 0;
            // Scroll values before the canvas — a new ItemsSource can change the bar's visibility.
            grid.UpdateScrollValues();
            grid.UpdateSkiaGrid();
            grid._collectionView.Refresh();

        }


        public IEnumerable? ViewList
        {
            get { if (_collectionView != null) return _collectionView.ViewList; else return null; }

        }



        //    UpdateScrollValues();
        //}
        private void TryGenerateColumns(object itemsSource)
        {
            if (itemsSource is IEnumerable enumerable)
            {
                var itemType = itemsSource.GetType().GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    .Select(i => i.GetGenericArguments()[0])
                    .FirstOrDefault()
                    ?? enumerable.Cast<object>().FirstOrDefault()?.GetType();

                if (itemType != null && Columns.Count == 0)
                {
                    GenerateColumnsFromType(itemType);
                }
            }
        }
        private void GenerateColumnsFromType(Type modelType)
        {
            Columns.Clear();
            foreach (var prop in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Columns.Add(new SKGridViewColumn
                {
                    Header = prop.Name,
                    // BindingPath = prop.Name,
                    Width = 120,
                    IsVisible = true
                });
            }
            UpdateColumnsInDataGrid();
            UpdateScrollValues();
        }
        #endregion ItemSource

        #region SelectedItems
        public ObservableCollection<object> SelectedItems
        {
            get { return (ObservableCollection<object>)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(nameof(SelectedItems), typeof(ObservableCollection<object>), typeof(SkiaGridViewV2), new PropertyMetadata(default, OnSelectedItemsChanged));

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 skGridView) return;

            if (e.NewValue is ObservableCollection<object> newSelection)
            {
                // Re-point the renderer AND the selection manager at the NEW collection
                // instance. Previously this callback only invalidated the canvas, so when
                // a ViewModel/binding swapped the SelectedItems collection on a data
                // refresh, the renderer kept matching highlights against the OLD (now
                // detached) collection — selection appeared lost on the next update.
                // The manager's setter forwards to SkiaRenderer.UpdateSelectedItems.
                if (skGridView._selectionManager != null)
                    skGridView._selectionManager.SelectedItems = newSelection;
                else
                    skGridView.SkiaRenderer?.UpdateSelectedItems(newSelection);
            }

            skGridView.SkiaCanvas?.InvalidateVisual();
            // A VM/binding swap of the SelectedItems collection is a selection change too —
            // raise UIA selection events so event-driven tests see it (change-gated).
            skGridView.RaiseAutomationSelectionChanged();
        }

        /// <summary>
        /// Enables per-cell hover tooltips. Default <c>false</c> (opt-in — no cost or
        /// behavior change when off). When true, hovering a cell whose text is ellipsis-
        /// truncated shows the full value in a tooltip. Cells that fit show nothing.
        /// Runtime-togglable; turning it off closes any open tooltip.
        /// </summary>
        public bool IsTooltipEnabled
        {
            get => (bool)GetValue(IsTooltipEnabledProperty);
            set => SetValue(IsTooltipEnabledProperty, value);
        }

        public static readonly DependencyProperty IsTooltipEnabledProperty =
            DependencyProperty.Register(nameof(IsTooltipEnabled), typeof(bool), typeof(SkiaGridViewV2),
                new PropertyMetadata(false, IsTooltipEnabledChanged));

        private static void IsTooltipEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Toggling off must dismiss any tooltip currently showing.
            if (d is SkiaGridViewV2 grid && !(bool)e.NewValue)
                grid.HideCellTooltip();
        }

        public bool RetainSelectionOnLFocusLost
        {
            get { return (bool)GetValue(RetainSelectionOnLFocusLostProperty); }
            set { SetValue(RetainSelectionOnLFocusLostProperty, value); }
        }

        public static readonly DependencyProperty RetainSelectionOnLFocusLostProperty =
            DependencyProperty.Register(
                nameof(RetainSelectionOnLFocusLost),
                typeof(bool),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(false, RetainSelectionOnLFocusLostPropertyChanged));

        private static void RetainSelectionOnLFocusLostPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                if (!(bool)e.NewValue)
                    skGridView?.SelectedItems?.Clear();
            }
        }

        public bool CanUserSelectRows
        {
            get { return (bool)GetValue(CanUserSelectRowsProperty); }
            set { SetValue(CanUserSelectRowsProperty, value); }
        }

        public static readonly DependencyProperty CanUserSelectRowsProperty =
            DependencyProperty.Register(
                nameof(CanUserSelectRows),
                typeof(bool),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(true, CanUserSelectRowsPropertyChanged));

        private static void CanUserSelectRowsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                if (!(bool)e.NewValue)
                    skGridView?.SelectedItems?.Clear();
            }
        }
        public Action<object, SKGridViewColumn> OnCellClicked
        {
            get { return (Action<object, SKGridViewColumn>)GetValue(OnCellClickedProperty); }
            set { SetValue(OnCellClickedProperty, value); }
        }

        public static readonly DependencyProperty OnCellClickedProperty =
            DependencyProperty.Register(nameof(OnCellClicked), typeof(Action<object, SKGridViewColumn>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        public Action<object> OnRowClicked
        {
            get { return (Action<object>)GetValue(OnRowClickedProperty); }
            set { SetValue(OnRowClickedProperty, value); }
        }

        public static readonly DependencyProperty OnRowClickedProperty =
            DependencyProperty.Register(nameof(OnRowClicked), typeof(Action<object>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        public Action<object> OnRowRightClicked
        {
            get { return (Action<object>)GetValue(OnRowRightClickedProperty); }
            set { SetValue(OnRowRightClickedProperty, value); }
        }

        public static readonly DependencyProperty OnRowRightClickedProperty =
            DependencyProperty.Register(nameof(OnRowRightClicked), typeof(Action<object>), typeof(SkiaGridViewV2), new PropertyMetadata(default));
        public Action<object> OnRowDoubleClicked
        {
            get { return (Action<object>)GetValue(OnRowDoubleClickedProperty); }
            set { SetValue(OnRowDoubleClickedProperty, value); }
        }

        public static readonly DependencyProperty OnRowDoubleClickedProperty =
            DependencyProperty.Register(nameof(OnRowDoubleClicked), typeof(Action<object>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        public Action<Key> OnPreviewKeyDownEvent
        {
            get { return (Action<Key>)GetValue(OnPreviewKeyDownEventProperty); }
            set { SetValue(OnPreviewKeyDownEventProperty, value); }
        }

        public static readonly DependencyProperty OnPreviewKeyDownEventProperty =
            DependencyProperty.Register(nameof(OnPreviewKeyDownEvent), typeof(Action<Key>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        public Action OnSkGridDoubleClicked
        {
            get { return (Action)GetValue(OnSkGridDoubleClickedProperty); }
            set { SetValue(OnSkGridDoubleClickedProperty, value); }
        }

        public static readonly DependencyProperty OnSkGridDoubleClickedProperty =
            DependencyProperty.Register(nameof(OnSkGridDoubleClicked), typeof(Action), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        #region ICommand DPs (MVVM-friendly alternatives to Action delegates)

        /// <summary>Fires when a row is left-clicked. CommandParameter = row data item.</summary>
        public System.Windows.Input.ICommand RowClickedCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(RowClickedCommandProperty);
            set => SetValue(RowClickedCommandProperty, value);
        }
        public static readonly DependencyProperty RowClickedCommandProperty =
            DependencyProperty.Register(nameof(RowClickedCommand), typeof(System.Windows.Input.ICommand), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        /// <summary>Fires when a row is double-clicked. CommandParameter = row data item.</summary>
        public System.Windows.Input.ICommand RowDoubleClickedCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(RowDoubleClickedCommandProperty);
            set => SetValue(RowDoubleClickedCommandProperty, value);
        }
        public static readonly DependencyProperty RowDoubleClickedCommandProperty =
            DependencyProperty.Register(nameof(RowDoubleClickedCommand), typeof(System.Windows.Input.ICommand), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        /// <summary>Fires when a row is right-clicked. CommandParameter = row data item.</summary>
        public System.Windows.Input.ICommand RowRightClickedCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(RowRightClickedCommandProperty);
            set => SetValue(RowRightClickedCommandProperty, value);
        }
        public static readonly DependencyProperty RowRightClickedCommandProperty =
            DependencyProperty.Register(nameof(RowRightClickedCommand), typeof(System.Windows.Input.ICommand), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        /// <summary>Fires when a cell is clicked. CommandParameter = (object Row, SKGridViewColumn Column) tuple.</summary>
        public System.Windows.Input.ICommand CellClickedCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(CellClickedCommandProperty);
            set => SetValue(CellClickedCommandProperty, value);
        }
        public static readonly DependencyProperty CellClickedCommandProperty =
            DependencyProperty.Register(nameof(CellClickedCommand), typeof(System.Windows.Input.ICommand), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        /// <summary>Fires after sort is applied. CommandParameter = (string ColumnHeader, SkGridViewColumnSort Direction) tuple.</summary>
        public System.Windows.Input.ICommand SortChangedCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(SortChangedCommandProperty);
            set => SetValue(SortChangedCommandProperty, value);
        }
        public static readonly DependencyProperty SortChangedCommandProperty =
            DependencyProperty.Register(nameof(SortChangedCommand), typeof(System.Windows.Input.ICommand), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        /// <summary>Fires when selection changes. CommandParameter = SelectedItems collection.</summary>
        public System.Windows.Input.ICommand SelectionChangedCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(SelectionChangedCommandProperty);
            set => SetValue(SelectionChangedCommandProperty, value);
        }
        public static readonly DependencyProperty SelectionChangedCommandProperty =
            DependencyProperty.Register(nameof(SelectionChangedCommand), typeof(System.Windows.Input.ICommand), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        /// <summary>Fires when a checkbox is toggled. CommandParameter = (object Item, string BindingPath, bool NewValue).</summary>
        public System.Windows.Input.ICommand CheckboxToggledCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(CheckboxToggledCommandProperty);
            set => SetValue(CheckboxToggledCommandProperty, value);
        }
        public static readonly DependencyProperty CheckboxToggledCommandProperty =
            DependencyProperty.Register(nameof(CheckboxToggledCommand), typeof(System.Windows.Input.ICommand), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        /// <summary>
        /// Fires when a group (grouped mode) or a parent row (tree / <see cref="ChildProperty"/> mode)
        /// expands or collapses. CommandParameter = <see cref="SkExpandChangedEventArgs"/>.
        /// Default null → no notifications and no payload cost.
        /// </summary>
        public System.Windows.Input.ICommand ExpandChangedCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(ExpandChangedCommandProperty);
            set => SetValue(ExpandChangedCommandProperty, value);
        }
        public static readonly DependencyProperty ExpandChangedCommandProperty =
            DependencyProperty.Register(nameof(ExpandChangedCommand), typeof(System.Windows.Input.ICommand), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        #endregion ICommand DPs

        #region Expand/collapse notification + state (additive, default-off)

        /// <summary>
        /// Raised when a group header or a tree parent row changes expand state — user glyph click,
        /// <see cref="ToggleGroup"/> / <see cref="ToggleRow"/>, or a bulk expand/collapse (one bulk
        /// event, not one per group). <see cref="SkExpandChangedEventArgs.AffectedItems"/> carries the
        /// rows that became visible/hidden, which is what a per-row market-data subscription needs.
        /// <para>Raised on the UI thread, after the grid state and row count have settled.</para>
        /// </summary>
        public event EventHandler<SkExpandChangedEventArgs>? ExpandChanged;

        /// <summary>True when anything is listening — gates payload materialization in GroupingManager.</summary>
        private bool HasExpandListeners => ExpandChanged != null || ExpandChangedCommand != null;

        private void OnExpandChanged(SkExpandChangedEventArgs e)
        {
            ExpandChanged?.Invoke(this, e);
            var cmd = ExpandChangedCommand;
            if (cmd != null && cmd.CanExecute(e)) cmd.Execute(e);
        }

        /// <summary>True when the named group is expanded (grouped mode). Unknown names report the default, true.</summary>
        public bool IsGroupExpanded(string groupName) => _groupingManager.IsGroupExpanded(groupName);

        /// <summary>Snapshot of expand state per group name (grouped mode). Empty before the first flatten.</summary>
        public IReadOnlyDictionary<string, bool> GetGroupExpandStates() => _groupingManager.GetGroupExpandStates();

        /// <summary>True when this parent row's children are expanded (tree / <see cref="ChildProperty"/> mode).</summary>
        public bool IsRowExpanded(object item) => _groupingManager.IsRowExpanded(item);

        /// <summary>Parent rows currently expanded (tree / <see cref="ChildProperty"/> mode).</summary>
        public IReadOnlyList<object> GetExpandedRowItems() => _groupingManager.GetExpandedRowItems();

        /// <summary>
        /// Data rows currently visible: rows of collapsed groups and header/subtotal rows are excluded;
        /// expanded parents' children are included. Use this to re-sync subscriptions after a data refresh.
        /// </summary>
        public IReadOnlyList<object> GetVisibleDataItems() => _groupingManager.GetVisibleDataItems();

        /// <summary>
        /// Expand/collapse one parent row's children (tree / <see cref="ChildProperty"/> mode) —
        /// the programmatic equivalent of clicking its toggle glyph. No-op in grouped mode
        /// (use <see cref="ToggleGroup"/>) and when the row is already in that state.
        /// </summary>
        public void ToggleRow(object item, bool isExpand) => _groupingManager.ToggleRow(item, isExpand);

        /// <summary>Expand every parent row (tree / <see cref="ChildProperty"/> mode). No-op in grouped mode.</summary>
        public void ExpandAllRows() => _groupingManager.SetAllRowsExpanded(true);

        /// <summary>Collapse every parent row (tree / <see cref="ChildProperty"/> mode). No-op in grouped mode.</summary>
        public void CollapseAllRows() => _groupingManager.SetAllRowsExpanded(false);

        #endregion Expand/collapse notification + state

        public bool CanUserReorderColumns
        {
            get { return (bool)GetValue(CanUserReorderColumnsProperty); }
            set { SetValue(CanUserReorderColumnsProperty, value); }
        }

        public static readonly DependencyProperty CanUserReorderColumnsProperty =
            DependencyProperty.Register(
                nameof(CanUserReorderColumns),
                typeof(bool),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(true, CanUserReorderColumnsChanged));

        private static void CanUserReorderColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                skGridView.DataListView.CanUserReorderColumns = (bool)e.NewValue;
            }
        }

        public bool CanUserResizeColumns
        {
            get { return (bool)GetValue(CanUserResizeColumnsProperty); }
            set { SetValue(CanUserResizeColumnsProperty, value); }
        }

        public static readonly DependencyProperty CanUserResizeColumnsProperty =
            DependencyProperty.Register(
                nameof(CanUserResizeColumns),
                typeof(bool),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(true, CanUserResizeColumnsChanged));

        private static void CanUserResizeColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                skGridView.DataListView.CanUserResizeColumns = (bool)e.NewValue;
            }
        }

        public bool CanUserSortColumns
        {
            get { return (bool)GetValue(CanUserSortColumnsProperty); }
            set { SetValue(CanUserSortColumnsProperty, value); }
        }

        public static readonly DependencyProperty CanUserSortColumnsProperty =
            DependencyProperty.Register(
                nameof(CanUserSortColumns),
                typeof(bool),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(true, CanUserSortColumnsChanged));

        private static void CanUserSortColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                skGridView.DataListView.CanUserSortColumns = (bool)e.NewValue;
            }
        }


        public float SKFontSize
        {
            get { return (float)GetValue(SKFontSizeProperty); }
            set { SetValue(SKFontSizeProperty, value); }
        }

        public static readonly DependencyProperty SKFontSizeProperty =
            DependencyProperty.Register(
                nameof(SKFontSize),
                typeof(float),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(11f, SKFontSizeChanged));

        private static void SKFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView && e.NewValue != null)
            {
                var size = (float)e.NewValue;
                skGridView._appearanceConfig.SetFontSize(size);
                skGridView.ApplyDataGridFont();
                skGridView.ApplyColumnHeaderHeight();
                // Font size drives RowHeight → canvas extent + both scrollbar ranges must be redone.
                skGridView.QueueGridMetricsRecalculation();
            }
        }

        /// <summary>
        /// Vertical density of the grid. Row and column-header heights are DERIVED from
        /// <see cref="SKFontSize"/> using this density's padding, so a grid keeps its intended
        /// proportions when the font changes — nothing to recalculate in the consumer.
        /// <para>
        /// <see cref="SKGridDensity.Compact"/> (default) = font+3 rows / font+7 header → 14/18 px at
        /// the default 11 px font, the time-and-sales tape density.
        /// <see cref="SKGridDensity.Normal"/> = font+6 / font+12 → 17/23 px, the roomier
        /// positions / account / portfolio density — set it per grid where rows need breathing space.
        /// </para>
        /// Prefer this over the explicit <see cref="SKRowHeight"/> / <see cref="SKColumnHeaderHeight"/>
        /// pixel overrides, which stop tracking the font size.
        /// </summary>
        public SKGridDensity Density
        {
            get { return (SKGridDensity)GetValue(DensityProperty); }
            set { SetValue(DensityProperty, value); }
        }

        public static readonly DependencyProperty DensityProperty =
            DependencyProperty.Register(
                nameof(Density),
                typeof(SKGridDensity),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(SKGridDensity.Compact, DensityChanged));

        private static void DensityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 skGridView || skGridView._appearanceConfig == null) return;
            skGridView._appearanceConfig.SetDensity((SKGridDensity)e.NewValue);
            skGridView.ApplyColumnHeaderHeight();
            skGridView.QueueGridMetricsRecalculation();
        }

        /// <summary>
        /// Explicit data-row height in px — an escape hatch for a design that needs an exact pixel
        /// height. <c>0</c> (the default) means "derive from <see cref="SKFontSize"/> and
        /// <see cref="Density"/>", which is normally what you want: a fixed value here stops tracking
        /// the font size. Also replaces the reflection workaround consumers needed while this value
        /// was internal-only.
        /// </summary>
        public double SKRowHeight
        {
            get { return (double)GetValue(SKRowHeightProperty); }
            set { SetValue(SKRowHeightProperty, value); }
        }

        public static readonly DependencyProperty SKRowHeightProperty =
            DependencyProperty.Register(
                nameof(SKRowHeight),
                typeof(double),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(0d, SKRowHeightChanged));

        private static void SKRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 skGridView || skGridView._appearanceConfig == null) return;
            skGridView._appearanceConfig.SetRowHeightOverride((double)e.NewValue);
            skGridView.QueueGridMetricsRecalculation();
        }

        /// <summary>
        /// Explicit column-header height in px — the header counterpart to <see cref="SKRowHeight"/>,
        /// and the same escape hatch. <c>0</c> (the default) means "derive from
        /// <see cref="SKFontSize"/> and <see cref="Density"/>". Ignored while
        /// <see cref="ColumnHeaderVisible"/> is false.
        /// </summary>
        public double SKColumnHeaderHeight
        {
            get { return (double)GetValue(SKColumnHeaderHeightProperty); }
            set { SetValue(SKColumnHeaderHeightProperty, value); }
        }

        public static readonly DependencyProperty SKColumnHeaderHeightProperty =
            DependencyProperty.Register(
                nameof(SKColumnHeaderHeight),
                typeof(double),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(0d, SKColumnHeaderHeightChanged));

        private static void SKColumnHeaderHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 skGridView || skGridView._appearanceConfig == null) return;
            skGridView._appearanceConfig.SetColumnHeaderHeightOverride((double)e.NewValue);
            skGridView.ApplyColumnHeaderHeight();
            // A taller/shorter header changes skiaContainer's height → canvas extent + viewport.
            skGridView.QueueGridMetricsRecalculation();
        }

        /// <summary>Set while a metrics recalculation is already queued, so a burst coalesces to one pass.</summary>
        private bool _metricsRecalcQueued;

        /// <summary>
        /// Recompute everything that depends on the row / header height — the SKElement canvas extent
        /// (<see cref="GetSkiaHeight"/> is <c>TotalRows * RowHeight</c>), both scrollbar ranges and their
        /// auto-visibility — then repaint.
        /// <para>
        /// Must be called by every property that changes those heights: <see cref="SKFontSize"/>,
        /// <see cref="Density"/>, <see cref="SKRowHeight"/>, <see cref="SKColumnHeaderHeight"/>,
        /// <see cref="ColumnHeaderVisible"/> — and by the two scroll bar thickness properties
        /// (<see cref="VerticalScrollBarWidth"/> / <see cref="HorizontalScrollBarHeight"/>), which
        /// change the canvas width and the viewports rather than the row height.
        /// Before this existed, changing the font at runtime left the
        /// canvas sized to the OLD row height and both scrollbars on the old extent — rows drew at the
        /// new height into a stale area, the last rows could not be scrolled to, and nothing repainted
        /// until an unrelated event happened to trigger one.
        /// </para>
        /// <para>
        /// Deferred to <see cref="DispatcherPriority.Loaded"/> (below Render, so it runs AFTER the layout
        /// pass) because a header-height change only reaches <c>skiaContainer.ActualHeight</c> — which
        /// <see cref="GetSkiaHeight"/> reads — once layout has run. Coalesced, so setting several of these
        /// properties together costs one pass. No-op before Loaded, which does the full pass itself.
        /// </para>
        /// </summary>
        private void QueueGridMetricsRecalculation()
        {
            if (!IsLoaded || _metricsRecalcQueued) return;
            _metricsRecalcQueued = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _metricsRecalcQueued = false;
                if (!IsLoaded || _scrollManager == null) return;

                // Scroll values FIRST: it settles each scrollbar's auto-visibility, and
                // GetSkiaWidth() subtracts the vertical scrollbar's width.
                _scrollManager.UpdateScrollValues();
                UpdateSkiaGrid();
                SkiaCanvas?.InvalidateVisual();
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Mirror the Skia data font (<see cref="SKFontFamily"/> / <see cref="SKFontSize"/> /
        /// <see cref="SKFontStyle"/>) onto the WPF header DataGrid, so column-header text is drawn
        /// in the same face and size as the rows underneath it.
        /// <para>
        /// Previously this only happened inside the three DP callbacks, so a grid that never
        /// ASSIGNED those DPs kept WPF's inherited default font in its headers (typically Segoe UI
        /// 12) while the rows used the Skia font — headers came out a different family and ~1 px
        /// larger than the data. Loaded now calls this too, so the default configuration matches.
        /// </para>
        /// Invalid / uninstalled family names are swallowed: the header keeps its previous font
        /// rather than throwing out of a Loaded handler.
        /// </summary>
        private void ApplyDataGridFont()
        {
            if (DataListView == null) return;

            DataListView.FontSize = SKFontSize;

            var family = SKFontFamily;
            if (!string.IsNullOrWhiteSpace(family))
            {
                try { DataListView.FontFamily = new FontFamily(family); }
                catch (Exception ex) { Diagnostics.GridLogger.LogWarning(Diagnostics.GridLogger.Render, $"Header FontFamily '{family}' rejected: {ex.Message}"); }
            }

            var lower = (SKFontStyle ?? "Normal").ToLowerInvariant();
            DataListView.FontStyle = lower.Contains("italic") ? FontStyles.Italic : FontStyles.Normal;
            DataListView.FontWeight = lower.Contains("bold") ? FontWeights.Bold : FontWeights.Normal;
        }

        /// <summary>
        /// Push <see cref="Services.GridAppearanceConfig.ColumnHeaderHeight"/> onto the WPF grid
        /// row that hosts the header DataGrid. Single funnel for the font-size, explicit-height and
        /// header-visibility paths so the XAML literal is never left stale.
        /// </summary>
        private void ApplyColumnHeaderHeight()
        {
            if (_appearanceConfig == null || SKGridColumnHeader == null) return;
            var height = _appearanceConfig.ColumnHeaderHeight;
            SKGridColumnHeader.Height = new GridLength(height);
            // Also pin the DataGrid's own header height. Without it the header cells are
            // CONTENT-sized (text + Padding) while the grid row reserves `height`, so any
            // mismatch showed up as a strip of window background under the headers — visible
            // as soon as the header font stopped being WPF's larger default. Keeps
            // AutomationDataProvider's ColumnHeaderHeight read accurate too.
            if (DataListView != null && height > 0)
                DataListView.ColumnHeaderHeight = height;
        }

        /// <summary>
        /// Column-header background as a hex color string. <c>null</c> (the default) keeps the
        /// built-in <c>#343434</c>. A single column still overrides this via
        /// <see cref="SKGridViewColumn.BackColor"/>, and an active filter still paints its header
        /// with the filter color.
        /// </summary>
        public string? ColumnHeaderBackground
        {
            get { return (string?)GetValue(ColumnHeaderBackgroundProperty); }
            set { SetValue(ColumnHeaderBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ColumnHeaderBackgroundProperty =
            DependencyProperty.Register(
                nameof(ColumnHeaderBackground),
                typeof(string),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(null, OnColumnHeaderAppearanceChanged));

        /// <summary>
        /// Column-header text color as a hex color string. <c>null</c> (the default) keeps the
        /// built-in <c>#E6E6E6</c>.
        /// </summary>
        public string? ColumnHeaderForeground
        {
            get { return (string?)GetValue(ColumnHeaderForegroundProperty); }
            set { SetValue(ColumnHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty ColumnHeaderForegroundProperty =
            DependencyProperty.Register(
                nameof(ColumnHeaderForeground),
                typeof(string),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(null, OnColumnHeaderAppearanceChanged));

        /// <summary>
        /// Color of the 1 px vertical line between column headers, as a hex color string.
        /// <c>null</c> (the default) keeps the built-in <c>#1E1E1E</c>.
        /// </summary>
        public string? ColumnHeaderSeparatorColor
        {
            get { return (string?)GetValue(ColumnHeaderSeparatorColorProperty); }
            set { SetValue(ColumnHeaderSeparatorColorProperty, value); }
        }

        public static readonly DependencyProperty ColumnHeaderSeparatorColorProperty =
            DependencyProperty.Register(
                nameof(ColumnHeaderSeparatorColor),
                typeof(string),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(null, OnColumnHeaderAppearanceChanged));

        /// <summary>
        /// Header colors are baked into the per-column <c>DataGridColumnHeader</c> styles built by
        /// <see cref="Managers.GridColumnManager.UpdateColumnsInDataGrid"/>, so a late change needs
        /// the header columns rebuilt. No-op before the columns exist — Loaded builds them anyway.
        /// </summary>
        private static void OnColumnHeaderAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 skGridView) return;
            if (skGridView.Columns == null || skGridView.Columns.Count == 0) return;
            if (!skGridView.IsLoaded) return;
            skGridView.UpdateColumnsInDataGrid();
        }
        public string SKFontFamily
        {
            get { return (string)GetValue(SKFontFamilyProperty); }
            set { SetValue(SKFontFamilyProperty, value); }
        }

        public static readonly DependencyProperty SKFontFamilyProperty =
            DependencyProperty.Register(
                nameof(SKFontFamily),
                typeof(string),
                typeof(SkiaGridViewV2),
                new PropertyMetadata("Microsoft Sans Serif", SKFontFamilyChanged));

        private static void SKFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView && e.NewValue != null)
            {
                var fontFamily = (string)e.NewValue;
                skGridView._appearanceConfig.SetFontFamily(fontFamily);
                skGridView.ApplyDataGridFont();
                // Family does not change row height, but every glyph width changes — repaint (and let
                // the funnel re-settle the scrollbars, which is a no-op here).
                skGridView.QueueGridMetricsRecalculation();
            }
        }
        public string SKFontStyle
        {
            get { return (string)GetValue(SKFontStyleProperty); }
            set { SetValue(SKFontStyleProperty, value); }
        }

        public static readonly DependencyProperty SKFontStyleProperty =
            DependencyProperty.Register(
                nameof(SKFontStyle),
                typeof(string),
                typeof(SkiaGridViewV2),
                new PropertyMetadata("Normal", SKFontStyleChanged));

        private static void SKFontStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                if (e.NewValue != null)
                {
                    var fontstyle = (string)e.NewValue;
                    skGridView._appearanceConfig.SetFontStyle(fontstyle);
                    skGridView.ApplyDataGridFont();
                    // Bold/italic changes glyph advance widths — repaint.
                    skGridView.QueueGridMetricsRecalculation();
                }
            }
        }


        public Action<string?, SkGridViewColumnSort?> SortChanged
        {
            get { return (Action<string?, SkGridViewColumnSort?>)GetValue(SortChangedProperty); }
            set { SetValue(SortChangedProperty, value); }
        }

        public static readonly DependencyProperty SortChangedProperty =
            DependencyProperty.Register(nameof(SortChanged), typeof(Action<string?, SkGridViewColumnSort?>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        public Action SortChanging
        {
            get { return (Action)GetValue(SortChangingProperty); }
            set { SetValue(SortChangingProperty, value); }
        }

        public static readonly DependencyProperty SortChangingProperty =
            DependencyProperty.Register(nameof(SortChanging), typeof(Action), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        public bool IsLiveSorting
        {
            get { return (bool)GetValue(IsLiveSortingProperty); }
            set { SetValue(IsLiveSortingProperty, value); }
        }

        public static readonly DependencyProperty IsLiveSortingProperty =
            DependencyProperty.Register(nameof(IsLiveSorting), typeof(bool), typeof(SkiaGridViewV2), new PropertyMetadata(true, IsLiveSortingChanged));

        private static void IsLiveSortingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid)
                return;
            if (grid._collectionView != null)
            {
                grid._collectionView.IsLiveSort = (bool)e.NewValue;
                if ((bool)e.NewValue)
                {
                    grid._collectionView.Refresh();
                }
            }
        }

        public bool AddNewRowAtBottomInGroup
        {
            get { return (bool)GetValue(AddNewRowAtBottomInGroupProperty); }
            set { SetValue(AddNewRowAtBottomInGroupProperty, value); }
        }

        public static readonly DependencyProperty AddNewRowAtBottomInGroupProperty =
            DependencyProperty.Register(nameof(AddNewRowAtBottomInGroup), typeof(bool), typeof(SkiaGridViewV2), new PropertyMetadata(true, AddNewRowAtBottomInGroupChanged));

        private static void AddNewRowAtBottomInGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid)
                return;
            if (grid._collectionView != null)
            {
                grid._collectionView.AddNewRowAtBottomInGroup = (bool)e.NewValue;

            }
        }

        public int? SortEvery
        {
            get { return (int?)GetValue(SortEveryProperty); }
            set { SetValue(SortEveryProperty, value); }
        }
        public static readonly DependencyProperty SortEveryProperty =
            DependencyProperty.Register(nameof(SortEvery), typeof(int?), typeof(SkiaGridViewV2), new PropertyMetadata(null, SortEveryChanged));

        private static void SortEveryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid) return;
            grid._sortingManager.HandleSortEveryChanged(e.NewValue as int?);
        }

        /// <summary>
        /// When true (default), a column's <c>GridViewColumnSort</c> both renders the ▲/▼ header glyph
        /// AND installs a sort on the CollectionView (current behavior — unchanged). When set false, the
        /// header glyph still renders but NO CollectionView sort is installed/cleared from column-driven
        /// paths — for consumers that sort their own source collection and only want the grid to display
        /// the sort indicator. SortChanged / SortChangedCommand still fire so such consumers get notified.
        /// (Note: <see cref="ClearSort"/> still clears the WPF glyph, as it always has.)
        /// </summary>
        public bool UseCollectionViewSort
        {
            get { return (bool)GetValue(UseCollectionViewSortProperty); }
            set { SetValue(UseCollectionViewSortProperty, value); }
        }
        public static readonly DependencyProperty UseCollectionViewSortProperty =
            DependencyProperty.Register(nameof(UseCollectionViewSort), typeof(bool), typeof(SkiaGridViewV2), new PropertyMetadata(true));

        /// <summary>
        /// When true, incremental row splices (single or range Add/Remove) defer the TotalRows +
        /// canvas-extent + scrollbar settle to a single Background-priority dispatcher post, so a burst
        /// of inserts within one dispatcher pump collapses to ONE settle instead of one per insert.
        /// Default false = the settle runs synchronously per splice (current behavior, unchanged). When
        /// on, a frame between the last splice and the posted settle may briefly show a stale extent.
        /// </summary>
        public bool CoalesceRowUpdates
        {
            get { return (bool)GetValue(CoalesceRowUpdatesProperty); }
            set { SetValue(CoalesceRowUpdatesProperty, value); }
        }
        public static readonly DependencyProperty CoalesceRowUpdatesProperty =
            DependencyProperty.Register(nameof(CoalesceRowUpdates), typeof(bool), typeof(SkiaGridViewV2), new PropertyMetadata(false));

        public bool FilterByGroup
        {
            get { return (bool)GetValue(FilterByGroupProperty); }
            set { SetValue(FilterByGroupProperty, value); }
        }
        public static readonly DependencyProperty FilterByGroupProperty =
            DependencyProperty.Register(nameof(FilterByGroup), typeof(bool), typeof(SkiaGridViewV2),
                new PropertyMetadata(false, FilterByGroupChanged));

        private static void FilterByGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Sync the DP to the underlying CV. The CV is library-internal, so consumers
            // configure this knob exclusively through the grid's DP.
            if (d is not SkiaGridViewV2 grid) return;
            if (grid._collectionView != null)
            {
                grid._collectionView.FilterByGroup = (bool)e.NewValue;
                grid._collectionView.Refresh();
                grid.Refresh();
            }
        }



        // Delegates to SortingManager (extracted in Step 8)
        private void UpdateTimerbaseSortingColumnColor(string color, SKGridViewColumn? column)
            => _sortingManager.UpdateTimerbaseSortingColumnColor(color, column);

        /// <summary>
        /// Property name on data items that contains child collection (e.g., "Legs", "Details").
        /// When set, the grid supports expand/collapse for hierarchical data without implementing ITreeItem.
        /// </summary>
        public string? ChildProperty
        {
            get => (string?)GetValue(ChildPropertyProperty);
            set => SetValue(ChildPropertyProperty, value);
        }
        public static readonly DependencyProperty ChildPropertyProperty =
            DependencyProperty.Register(nameof(ChildProperty), typeof(string), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        public bool ColumnHeaderVisible
        {
            get { return (bool)GetValue(ColumnHeaderVisibleProperty); }
            set { SetValue(ColumnHeaderVisibleProperty, value); }
        }

        public static readonly DependencyProperty ColumnHeaderVisibleProperty =
            DependencyProperty.Register(
                nameof(ColumnHeaderVisible),
                typeof(bool),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(true, OnColumnHeaderVisibleChanged));

        private static void OnColumnHeaderVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                var visible = (bool)e.NewValue;
                skGridView._appearanceConfig.SetColumnHeaderVisible(visible);
                skGridView.ApplyColumnHeaderHeight();
                skGridView.DataListView.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
                // Hiding/showing the header collapses or restores its row → the canvas gains or loses
                // that height, so the extent and scrollbars have to be recomputed.
                skGridView.QueueGridMetricsRecalculation();
                // The Header_N peers enter/leave the automation tree with this flag, and they are
                // served from a cache — without dropping it an already-attached client keeps showing
                // headers at their old rectangle. InvalidateRowCache() clears the header cache and
                // notifies; header visibility is part of the structure gate's shape tuple, so the
                // raise actually goes out even though rows and columns are unchanged.
                skGridView.InvalidateAutomationCache();
            }
        }
        public bool IsDeferredScrollingEnabled
        {
            get { return (bool)GetValue(IsDeferredScrollingEnabledProperty); }
            set { SetValue(IsDeferredScrollingEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsDeferredScrollingEnabledProperty =
            DependencyProperty.Register(
                nameof(IsDeferredScrollingEnabled),
                typeof(bool),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(false));

        public Action<SKGridViewColumn> ColumnRightClick
        {
            get { return (Action<SKGridViewColumn>)GetValue(ColumnRightClickProperty); }
            set { SetValue(ColumnRightClickProperty, value); }
        }

        public static readonly DependencyProperty ColumnRightClickProperty =
            DependencyProperty.Register(nameof(ColumnRightClick), typeof(Action<SKGridViewColumn>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        public Action<SKGridViewColumn> ColumnLeftClick
        {
            get { return (Action<SKGridViewColumn>)GetValue(ColumnLeftClickProperty); }
            set { SetValue(ColumnLeftClickProperty, value); }
        }

        public static readonly DependencyProperty ColumnLeftClickProperty =
            DependencyProperty.Register(nameof(ColumnLeftClick), typeof(Action<SKGridViewColumn>), typeof(SkiaGridViewV2), new PropertyMetadata(default));
        public ContextMenu HeaderContextMenu
        {
            get { return (ContextMenu)GetValue(HeaderContextMenuProperty); }
            set { SetValue(HeaderContextMenuProperty, value); }
        }

        public static readonly DependencyProperty HeaderContextMenuProperty =
            DependencyProperty.Register(nameof(HeaderContextMenu), typeof(ContextMenu), typeof(SkiaGridViewV2), new PropertyMetadata(default, OnHeaderContextMenuChanged));

        private static void OnHeaderContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                skGridView.DataListView.ContextMenu = e.NewValue as ContextMenu;
            }
        }

        public ContextMenu ContextMenu
        {
            get { return (ContextMenu)GetValue(ContextMenuProperty); }
            set { SetValue(ContextMenuProperty, value); }
        }

        public static readonly DependencyProperty ContextMenuProperty =
            DependencyProperty.Register(nameof(ContextMenu), typeof(ContextMenu), typeof(SkiaGridViewV2), new PropertyMetadata(default, OnContextMenuChanged));

        private static void OnContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                skGridView.skiaContainer.ContextMenu = e.NewValue as ContextMenu;
            }
        }

        public ContextMenu ItemsContextMenu
        {
            get { return (ContextMenu)GetValue(ItemsContextMenuProperty); }
            set { SetValue(ItemsContextMenuProperty, value); }
        }

        public static readonly DependencyProperty ItemsContextMenuProperty =
            DependencyProperty.Register(nameof(ItemsContextMenu), typeof(ContextMenu), typeof(SkiaGridViewV2), new PropertyMetadata(default, OnItemsContextMenuChanged));

        private static void OnItemsContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                skGridView.SkiaCanvas.ContextMenu = e.NewValue as ContextMenu;
            }
        }
        public Action<double> HorizontalScrollBarPositionChanged
        {
            get { return (Action<double>)GetValue(HorizontalScrollBarPositionChangedProperty); }
            set { SetValue(HorizontalScrollBarPositionChangedProperty, value); }
        }

        public static readonly DependencyProperty HorizontalScrollBarPositionChangedProperty =
            DependencyProperty.Register(nameof(HorizontalScrollBarPositionChanged), typeof(Action<double>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        /// <summary>
        /// Width in px that the vertical scroll bar currently occupies — <c>0</c> when it is not shown.
        /// Read this instead of guessing a constant: it accounts for a
        /// <see cref="VerticalScrollBarWidth"/> override, and falls back to the system metric for a bar
        /// that is visible but has not been through a layout pass yet (its <c>ActualWidth</c> is still 0
        /// at that point).
        /// <para>
        /// Pair it with <see cref="VerticalScrollBarVisibilityChanged"/> for column-fitting math: the
        /// callback tells you WHEN to recompute, this tells you HOW MUCH width the bar took.
        /// </para>
        /// </summary>
        public double VerticalScrollBarActualWidth =>
            Managers.ScrollManager.EffectiveVerticalBarWidth(VerticalScrollViewer);

        /// <summary>
        /// Raised when the vertical scroll bar's auto-visibility flips — i.e. only while
        /// <see cref="VerticalScrollBarVisible"/> is <c>Auto</c>. The bool is the new state
        /// (<c>true</c> = the bar is now shown). Default null.
        /// <para>
        /// Fires only on a real change, not on every data tick, and is raised SYNCHRONOUSLY inside the
        /// scroll-value pass — which every code path runs BEFORE it sizes the canvas. So column widths
        /// set from this callback are picked up in the same layout pass, with no one-frame lag and no
        /// visible jump as the bar comes and goes.
        /// </para>
        /// <para>
        /// Setting <see cref="SKGridViewColumn.Width"/> from here is supported: it re-enters the
        /// scroll-value pass, and those nested calls are dropped rather than recomputing once per
        /// column. The outer pass still finishes with your new widths.
        /// </para>
        /// <para>
        /// An explicit <c>Visible</c> / <c>Hidden</c> does NOT raise this — that is your own call, so
        /// you already know.
        /// </para>
        /// </summary>
        public Action<bool> VerticalScrollBarVisibilityChanged
        {
            get { return (Action<bool>)GetValue(VerticalScrollBarVisibilityChangedProperty); }
            set { SetValue(VerticalScrollBarVisibilityChangedProperty, value); }
        }

        public static readonly DependencyProperty VerticalScrollBarVisibilityChangedProperty =
            DependencyProperty.Register(nameof(VerticalScrollBarVisibilityChanged), typeof(Action<bool>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        public Action<double> VerticalScrollBarPositionChanged
        {
            get { return (Action<double>)GetValue(VerticalScrollBarPositionChangedProperty); }
            set { SetValue(VerticalScrollBarPositionChangedProperty, value); }
        }

        public static readonly DependencyProperty VerticalScrollBarPositionChangedProperty =
            DependencyProperty.Register(nameof(VerticalScrollBarPositionChanged), typeof(Action<double>), typeof(SkiaGridViewV2), new PropertyMetadata(default));

        // ── Row drag (2.18.0) ───────────────────────────────────────────────────────
        // Additive and default-off: RowDragMode is None unless a consumer opts in, so a grid that
        // never touches these members behaves exactly as it did before.

        /// <summary>
        /// Whether a row can be dragged, and what the grid does when it is dropped.
        /// Default <see cref="SKRowDragMode.None"/> — no drag at all.
        /// <para>
        /// <see cref="SKRowDragMode.Reorder"/> moves the dragged row(s) inside the collection bound
        /// to <see cref="ItemsSource"/> (it must implement <see cref="IList"/>); the built-in move is
        /// skipped while a sort or grouping is active, since view order would not survive it.
        /// <see cref="SKRowDragMode.Notify"/> tracks and reports but never mutates anything.
        /// <see cref="SKRowDragMode.DragOut"/> instead starts a real WPF drag-and-drop so ANOTHER
        /// WINDOW (or another application) can receive the row / the pressed cell — nothing moves
        /// inside the grid and no insertion indicator is drawn. See <see cref="SKRowDragData"/>.
        /// </para>
        /// <para>
        /// Group header rows and tree child rows are never draggable in either mode.
        /// The drag set is the current selection when the pressed row is part of a multi-row
        /// selection, otherwise just the pressed row.
        /// </para>
        /// </summary>
        public SKRowDragMode RowDragMode
        {
            get => (SKRowDragMode)GetValue(RowDragModeProperty);
            set => SetValue(RowDragModeProperty, value);
        }

        public static readonly DependencyProperty RowDragModeProperty =
            DependencyProperty.Register(nameof(RowDragMode), typeof(SKRowDragMode), typeof(SkiaGridViewV2),
                new PropertyMetadata(SKRowDragMode.None, OnRowDragModeChanged));

        private static void OnRowDragModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Turning drag off mid-drag must not leave an indicator painted or the mouse captured.
            if (d is SkiaGridViewV2 grid && (SKRowDragMode)e.NewValue == SKRowDragMode.None)
                grid._rowDragController?.Cancel();
        }

        /// <summary>
        /// Hex colour of the drop-insertion line and of the translucent ghost over the rows being
        /// dragged. Null/empty = the built-in <c>#3399FF</c>.
        /// </summary>
        public string? RowDragIndicatorColor
        {
            get => (string?)GetValue(RowDragIndicatorColorProperty);
            set => SetValue(RowDragIndicatorColorProperty, value);
        }

        public static readonly DependencyProperty RowDragIndicatorColorProperty =
            DependencyProperty.Register(nameof(RowDragIndicatorColor), typeof(string), typeof(SkiaGridViewV2),
                new PropertyMetadata(default(string)));

        /// <summary>
        /// Raised once per drag, when the pointer passes the system drag threshold and BEFORE the
        /// grid starts tracking. Set <c>Cancel</c> to refuse the drag. Mouse capture is not held
        /// while this runs, so a handler may start a WPF <c>DragDrop.DoDragDrop</c> for a
        /// cross-window drop — such a handler should also set <c>Cancel = true</c>.
        /// </summary>
        public event EventHandler<SKRowDragStartingEventArgs>? RowDragStarting;

        /// <summary>
        /// Raised when a drag is released over the grid. In <see cref="SKRowDragMode.Reorder"/> the
        /// grid performs the source move AFTER this event unless a handler set <c>Handled</c>.
        /// </summary>
        public event EventHandler<SKRowDroppedEventArgs>? RowDropped;

        /// <summary>
        /// Raised after a <see cref="SKRowDragMode.DragOut"/> drag finishes, carrying the effect the
        /// drop target applied (<c>None</c> = nothing accepted it). The grid never removes rows
        /// itself, not even on <c>Move</c> — handle this if the source collection should give them up.
        /// </summary>
        public event EventHandler<SKRowDragCompletedEventArgs>? RowDragCompleted;

        /// <summary>ICommand counterpart of <see cref="RowDragCompleted"/>.</summary>
        public ICommand RowDragCompletedCommand
        {
            get => (ICommand)GetValue(RowDragCompletedCommandProperty);
            set => SetValue(RowDragCompletedCommandProperty, value);
        }

        public static readonly DependencyProperty RowDragCompletedCommandProperty =
            DependencyProperty.Register(nameof(RowDragCompletedCommand), typeof(ICommand), typeof(SkiaGridViewV2),
                new PropertyMetadata(default(ICommand)));

        /// <summary>
        /// ICommand counterpart of <see cref="RowDropped"/> — parameter is the same
        /// <see cref="SKRowDroppedEventArgs"/>, so a ViewModel handler can set <c>Handled</c>.
        /// </summary>
        public ICommand RowDroppedCommand
        {
            get => (ICommand)GetValue(RowDroppedCommandProperty);
            set => SetValue(RowDroppedCommandProperty, value);
        }

        public static readonly DependencyProperty RowDroppedCommandProperty =
            DependencyProperty.Register(nameof(RowDroppedCommand), typeof(ICommand), typeof(SkiaGridViewV2),
                new PropertyMetadata(default(ICommand)));

        /// <summary>
        /// Whether the built-in reorder can actually run: view order must equal source order, so an
        /// active sort or grouping rules it out, as does a fixed-size or read-only source. Exposed so
        /// a consumer can grey out a "drag to reorder" affordance instead of discovering that the
        /// drop did nothing.
        /// </summary>
        public bool CanReorderRows =>
            GroupSettings == null
            && _collectionView?.IsSorted != true
            && ItemsSource is IList { IsFixedSize: false, IsReadOnly: false };

        // Flattened rows in VIEW order — the same list MouseHandler and KeyboardHandler index into,
        // so a drag's row indexes always agree with what a click on that row would hit.
        private List<object> RowDragViewItems() =>
            GroupSettings == null
                ? SkiaRenderer?.Items?.Select(x => x.Item!).ToList() ?? new()
                : SkiaRenderer?.GroupItemSource?.Where(x => x.IsExpanded || x.IsGroupHeader)
                                               .Select(x => (object)(x.Item ?? x)).ToList() ?? new();

        // Group headers carry a GroupModel, not a data item — there is nowhere in the source to move
        // them to. Tree children live inside their parent's child collection, not the source list.
        private bool IsRowDraggable(object item)
        {
            // Null is possible because this list keeps MouseHandler's exact shape (see above) rather
            // than filtering — an unset RowModel.Item must not be draggable.
            if (item is null) return false;
            if (item is GroupModel) return false;
            if (!string.IsNullOrEmpty(ChildProperty))
            {
                var row = SkiaRenderer?.Items?.FirstOrDefault(x => ReferenceEquals(x.Item, item));
                if (row != null && row.IsChildRow) return false;
            }
            return true;
        }

        private void PublishRowDragVisual(int insertIndex, List<int>? rowIndexes)
        {
            if (SkiaRenderer == null) return;
            SkiaRenderer.RowDragInsertIndex = insertIndex;
            SkiaRenderer.RowDragRowIndexes = rowIndexes;
            SkiaRenderer.RowDragIndicatorColorHex = string.IsNullOrWhiteSpace(RowDragIndicatorColor)
                ? Renderer.SkiaRenderer.DefaultRowDragIndicatorColor
                : RowDragIndicatorColor!;
        }

        private bool RaiseRowDragStarting(SKRowDragStartingEventArgs args)
        {
            ClearPendingSelectionCollapse();
            RowDragStarting?.Invoke(this, args);
            if (!args.Cancel)
                SkiaCanvas.Cursor = Cursors.Hand;
            return !args.Cancel;
        }

        // internal, not private, so a test can drive a drop without a physically pressed mouse
        // button — this is the method that decides whether the consumer's collection is mutated.
        internal void RaiseRowDropped(SKRowDroppedEventArgs args)
        {
            SkiaCanvas.Cursor = null;

            RowDropped?.Invoke(this, args);
            if (RowDroppedCommand != null && RowDroppedCommand.CanExecute(args))
                RowDroppedCommand.Execute(args);

            if (args.Handled || RowDragMode != SKRowDragMode.Reorder) return;
            if (!CanReorderRows)
            {
                Diagnostics.GridLogger.LogWarning(Diagnostics.GridLogger.Input,
                    "Row drop ignored: reorder needs an unsorted, ungrouped, mutable IList ItemsSource.");
                return;
            }

            // Mutate the SOURCE, never the collection view: a view-only move is undone by the next
            // Refresh(), which is exactly why MoveRowUp/MoveRowDown are [Obsolete].
            if (Input.RowDragController.MoveBlock((IList)ItemsSource, args.Items, args.TargetItem))
                Refresh();
        }

        // A selection collapse that a press asked for but a possible drag has postponed. Applied
        // on mouse-up if no drag started; dropped outright if one did.
        private (List<dynamic> Rows, int Index)? _pendingSelectionCollapse;

        internal void DeferSelectionCollapse(List<dynamic> rows, int index)
            => _pendingSelectionCollapse = (rows, index);

        // A drag won the gesture — the selection stays as the user built it.
        private void ClearPendingSelectionCollapse() => _pendingSelectionCollapse = null;

        private void ApplyPendingSelectionCollapse()
        {
            var pending = _pendingSelectionCollapse;
            _pendingSelectionCollapse = null;
            if (pending == null || !CanUserSelectRows) return;

            _selectionManager.SelectedItems = SelectedItems;
            _selectionManager.EnsureInitialized();
            SelectedItems = _selectionManager.SelectedItems!;
            _selectionManager.HandleSingleClick(pending.Value.Rows, pending.Value.Index);

            ExecuteRowDragCommand(SelectionChangedCommand, SelectedItems);
            RaiseAutomationSelectionChanged();
            SkiaCanvas.InvalidateVisual();
        }

        private static void ExecuteRowDragCommand(ICommand? command, object? parameter)
        {
            if (command != null && command.CanExecute(parameter))
                command.Execute(parameter);
        }

        // Called by MouseHandler on left-button-down. Inert unless RowDragMode was opted into.
        // `column` is the column the press resolved to — the SAME one OnCellClicked receives, so a
        // dragged-out cell and a clicked cell can never disagree.
        internal void ArmRowDrag(System.Windows.IInputElement canvas, Point point, int rowIndex,
                                 SKGridViewColumn? column)
            => _rowDragController?.Arm(canvas, point, rowIndex, column);

        /// <summary>
        /// Start a real WPF drag-and-drop so targets OUTSIDE this grid — another window, another
        /// control, another application — can receive the row(s) or the pressed cell.
        /// <para>
        /// <c>DragDrop.DoDragDrop</c> is BLOCKING: it pumps a nested message loop and does not
        /// return until the user releases. Nothing after the call runs during the drag, which is why
        /// the controller disarms before getting here.
        /// </para>
        /// </summary>
        private void BeginExternalRowDrag(List<object> items, int rowIndex, SKGridViewColumn? column)
        {
            if (items.Count == 0) return;

            var pressedItem = items.Count == 1
                ? items[0]
                : (rowIndex >= 0 ? RowDragViewItems().ElementAtOrDefault(rowIndex) : null) ?? items[0];

            var cellText = SkiaRenderer?.FormatCellText(pressedItem, column);
            var text = BuildRowDragText(items);

            var payload = new SKRowDragData(this, items, column, cellText, text);
            var data = new DataObject();
            data.SetData(SKRowDragData.Format, payload);
            // Text as well, so the drop target does not have to be this app — Excel, a text box, a
            // web form all take this. A handler is free to overwrite it in RowDragStarting.
            if (!string.IsNullOrEmpty(text)) data.SetData(DataFormats.UnicodeText, text);

            var starting = new SKRowDragStartingEventArgs(items, rowIndex, column, cellText, data);
            // The gesture is a drag, so the press's deferred selection collapse never happens.
            ClearPendingSelectionCollapse();
            RowDragStarting?.Invoke(this, starting);
            if (starting.Cancel) return;

            DragDropEffects result;
            try
            {
                result = DragDrop.DoDragDrop(SkiaCanvas, data, starting.AllowedEffects);
            }
            catch (Exception ex)
            {
                // A drag can be refused by the shell (one already in progress, or an OLE hiccup).
                // Losing a drag must never take the grid down with it.
                Diagnostics.GridLogger.LogError(Diagnostics.GridLogger.Input, "DoDragDrop failed", ex);
                return;
            }

            var completed = new SKRowDragCompletedEventArgs(items, result, column, cellText);
            RowDragCompleted?.Invoke(this, completed);
            if (RowDragCompletedCommand != null && RowDragCompletedCommand.CanExecute(completed))
                RowDragCompletedCommand.Execute(completed);
        }

        /// <summary>
        /// Tab-separated text for the dragged rows, header row first — built from the VISIBLE
        /// columns in display order and through the same formatting the cells draw with, so what is
        /// pasted matches what was on screen.
        /// </summary>
        // internal for tests: the text half of the drag payload is what out-of-process
        // targets receive, so its shape is worth pinning down.
        internal string BuildRowDragText(List<object> items)
        {
            var cols = Columns?.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
            if (cols == null || cols.Count == 0 || SkiaRenderer == null) return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Join("\t", cols.Select(c => c.Header)));
            foreach (var item in items)
                sb.AppendLine(string.Join("\t", cols.Select(c => SkiaRenderer.FormatCellText(item, c) ?? string.Empty)));
            return sb.ToString();
        }

        // One row per auto-scroll tick while the pointer sits in the edge zone during a drag.
        private void ScrollRowsForDrag(int direction)
        {
            if (VerticalScrollViewer == null || RowHeight <= 0) return;
            var target = VerticalScrollViewer.Value + direction * RowHeight;
            VerticalScrollViewer.Value = Math.Clamp(target, VerticalScrollViewer.Minimum, VerticalScrollViewer.Maximum);
            UpdateVerticalScroll();
        }


        public SKScrollBarVisibility HorizontalScrollBarVisible
        {
            get { return (SKScrollBarVisibility)GetValue(HorizontalScrollBarVisibleProperty); }
            set { SetValue(HorizontalScrollBarVisibleProperty, value); }
        }

        public static readonly DependencyProperty HorizontalScrollBarVisibleProperty =
            DependencyProperty.Register(
                nameof(HorizontalScrollBarVisible),
                typeof(SKScrollBarVisibility),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(SKScrollBarVisibility.Auto, OnHorizontalScrollBarVisibilityChanged));

        private static void OnHorizontalScrollBarVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                // Auto deliberately assigns no Visibility here — ManageHorizontalScrollBar(), inside
                // UpdateScrollValues(), owns that decision. What was missing is the recalculation
                // below: the Auto branch fell straight through and nothing re-ran, so switching to
                // Auto at runtime left the bar frozen in whatever state it was last put into until
                // some unrelated event happened to call UpdateScrollValues().
                if (((SKScrollBarVisibility)e.NewValue) == SKScrollBarVisibility.Visible)
                    skGridView.HorizontalScrollViewer.Visibility = Visibility.Visible;
                else if (((SKScrollBarVisibility)e.NewValue) == SKScrollBarVisibility.Hidden)
                    skGridView.HorizontalScrollViewer.Visibility = Visibility.Collapsed;

                // Every path recomputes: Auto needs its visibility settled, and Visible/Hidden change
                // the canvas extent (GetSkiaWidth subtracts the vertical bar's width).
                skGridView.QueueGridMetricsRecalculation();
            }
        }


        public SKScrollBarVisibility VerticalScrollBarVisible
        {
            get { return (SKScrollBarVisibility)GetValue(VerticalScrollBarVisibleProperty); }
            set { SetValue(VerticalScrollBarVisibleProperty, value); }
        }

        public static readonly DependencyProperty VerticalScrollBarVisibleProperty =
            DependencyProperty.Register(
                nameof(VerticalScrollBarVisible),
                typeof(SKScrollBarVisibility),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(SKScrollBarVisibility.Auto, OnVerticalScrollBarVisibilityChanged));

        private static void OnVerticalScrollBarVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 skGridView)
            {
                // Auto deliberately assigns no Visibility here — ManageVerticalScrollBar(), inside
                // UpdateScrollValues(), owns that decision. See the horizontal callback above for why
                // the recalculation is not optional.
                if (((SKScrollBarVisibility)e.NewValue) == SKScrollBarVisibility.Visible)
                    skGridView.VerticalScrollViewer.Visibility = Visibility.Visible;
                else if (((SKScrollBarVisibility)e.NewValue) == SKScrollBarVisibility.Hidden)
                    skGridView.VerticalScrollViewer.Visibility = Visibility.Collapsed;

                skGridView.QueueGridMetricsRecalculation();
            }
        }

        /// <summary>
        /// Width of the vertical scroll bar in px. <c>0</c> (the default) keeps WPF's own width — the
        /// system metric, ~17 px — so a grid that never sets this is unchanged.
        /// <para>
        /// Set it to thin the bar down for a dense tape (8–10 px) or to widen it for touch. Values
        /// below the system metric DO take effect: the control also lowers the bar's
        /// <c>MinWidth</c>, which WPF's default ScrollBar style otherwise pins to that metric.
        /// </para>
        /// The bar's LENGTH is layout-driven (it spans the header + canvas rows) and is not settable.
        /// Pair with <see cref="HorizontalScrollBarHeight"/> for the horizontal bar.
        /// </summary>
        public double VerticalScrollBarWidth
        {
            get { return (double)GetValue(VerticalScrollBarWidthProperty); }
            set { SetValue(VerticalScrollBarWidthProperty, value); }
        }

        public static readonly DependencyProperty VerticalScrollBarWidthProperty =
            DependencyProperty.Register(
                nameof(VerticalScrollBarWidth),
                typeof(double),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(0d, VerticalScrollBarWidthChanged));

        private static void VerticalScrollBarWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 skGridView || skGridView._appearanceConfig == null) return;
            skGridView._appearanceConfig.SetVerticalScrollBarWidth((double)e.NewValue);
            skGridView.ApplyScrollBarMetrics();
            // The canvas width is MainGrid.ActualWidth minus this bar (GetSkiaWidth), and the
            // horizontal bar's viewport subtracts it too (ScrollManager.UpdateScrollValues) — both
            // have to be redone once layout has picked up the new width.
            skGridView.QueueGridMetricsRecalculation();
        }

        /// <summary>
        /// Height of the horizontal scroll bar in px. <c>0</c> (the default) keeps the built-in
        /// 20 px the control has always used, so a grid that never sets this is unchanged.
        /// <para>
        /// As with <see cref="VerticalScrollBarWidth"/>, values below the system metric work because
        /// the control lowers the bar's <c>MinHeight</c> alongside its height.
        /// </para>
        /// The bar's LENGTH is layout-driven (it spans the canvas column) and is not settable.
        /// </summary>
        public double HorizontalScrollBarHeight
        {
            get { return (double)GetValue(HorizontalScrollBarHeightProperty); }
            set { SetValue(HorizontalScrollBarHeightProperty, value); }
        }

        public static readonly DependencyProperty HorizontalScrollBarHeightProperty =
            DependencyProperty.Register(
                nameof(HorizontalScrollBarHeight),
                typeof(double),
                typeof(SkiaGridViewV2),
                new PropertyMetadata(0d, HorizontalScrollBarHeightChanged));

        private static void HorizontalScrollBarHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 skGridView || skGridView._appearanceConfig == null) return;
            skGridView._appearanceConfig.SetHorizontalScrollBarHeight((double)e.NewValue);
            skGridView.ApplyScrollBarMetrics();
            // A shorter/taller bar changes skiaContainer's height → canvas extent + vertical viewport.
            skGridView.QueueGridMetricsRecalculation();
        }

        /// <summary>
        /// Push the configured scroll bar thickness onto the two WPF ScrollBars — the vertical bar's
        /// width and the horizontal bar's height. Single funnel for Loaded and both DP callbacks, so
        /// the XAML literals are never left stale (mirrors <see cref="ApplyColumnHeaderHeight"/>).
        /// <para>
        /// MinWidth / MinHeight are written alongside Width / Height because WPF's default ScrollBar
        /// style pins them to the system metric (~17 px): with only Width set, any thinner value is
        /// silently clamped and the DP would look like it did nothing.
        /// </para>
        /// <para>
        /// An unset (0) vertical width CLEARS the local values rather than writing a number, handing
        /// the bar back to the system width instead of one we guessed. The horizontal bar has always
        /// been pinned to 20 px in XAML, so its unset case writes that same 20.
        /// </para>
        /// </summary>
        private void ApplyScrollBarMetrics()
        {
            if (_appearanceConfig == null) return;

            if (VerticalScrollViewer != null)
            {
                var width = _appearanceConfig.VerticalScrollBarWidth;
                if (width is > 0)
                {
                    VerticalScrollViewer.Width = width.Value;
                    VerticalScrollViewer.MinWidth = width.Value;
                }
                else
                {
                    VerticalScrollViewer.ClearValue(FrameworkElement.WidthProperty);
                    VerticalScrollViewer.ClearValue(FrameworkElement.MinWidthProperty);
                }
            }

            if (HorizontalScrollViewer != null)
            {
                var height = _appearanceConfig.HorizontalScrollBarHeight;
                HorizontalScrollViewer.Height = height;
                HorizontalScrollViewer.MinHeight = height;
            }
        }
        #endregion SelectedItems

        #region SelectionStyle

        /// <summary>Controls how selected rows are highlighted: Fill (bg+text change) or Border (outline only).</summary>
        public SKSelectionStyle SelectionStyle
        {
            get => (SKSelectionStyle)GetValue(SelectionStyleProperty);
            set => SetValue(SelectionStyleProperty, value);
        }

        public static readonly DependencyProperty SelectionStyleProperty =
            DependencyProperty.Register(nameof(SelectionStyle), typeof(SKSelectionStyle), typeof(SkiaGridViewV2), new PropertyMetadata(SKSelectionStyle.Fill));

        /// <summary>
        /// Background color of a selected row (hex, e.g. "#1E5A2E"). Default null → the
        /// built-in blue (#0072C6). Applies when SelectionStyle=Fill.
        /// </summary>
        public string? SelectedRowBackground
        {
            get => (string?)GetValue(SelectedRowBackgroundProperty);
            set => SetValue(SelectedRowBackgroundProperty, value);
        }

        public static readonly DependencyProperty SelectedRowBackgroundProperty =
            DependencyProperty.Register(nameof(SelectedRowBackground), typeof(string), typeof(SkiaGridViewV2),
                new PropertyMetadata(null, SelectedRowBackgroundChanged));

        private static void SelectedRowBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 grid)
            {
                grid.SkiaRenderer?.SetSelectedRowBackground(e.NewValue as string);
                grid.SkiaCanvas?.InvalidateVisual();
            }
        }

        /// <summary>
        /// Text color of a selected row (hex, e.g. "#FFFFFF"). Default null → white.
        /// Applies when SelectionStyle=Fill.
        /// </summary>
        public string? SelectedRowForeground
        {
            get => (string?)GetValue(SelectedRowForegroundProperty);
            set => SetValue(SelectedRowForegroundProperty, value);
        }

        public static readonly DependencyProperty SelectedRowForegroundProperty =
            DependencyProperty.Register(nameof(SelectedRowForeground), typeof(string), typeof(SkiaGridViewV2),
                new PropertyMetadata(null, SelectedRowForegroundChanged));

        private static void SelectedRowForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaGridViewV2 grid)
            {
                grid.SkiaRenderer?.SetSelectedRowForeground(e.NewValue as string);
                grid.SkiaCanvas?.InvalidateVisual();
            }
        }

        #endregion SelectionStyle

        #region ColorProperties

        public string ForegroundColor
        {
            get { return (string)GetValue(ForegroundColorProperty); }
            set { SetValue(ForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.Register(nameof(ForegroundColor), typeof(string), typeof(SkiaGridViewV2), new PropertyMetadata(default, ForegroundColorChanged));

        private static void ForegroundColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid) return;
            grid._appearanceConfig.SetForegroundColor((string)e.NewValue);
        }

        public string RowBackground
        {
            get { return (string)GetValue(RowBackgroundProperty); }
            set { SetValue(RowBackgroundProperty, value); }
        }

        public static readonly DependencyProperty RowBackgroundProperty =
            DependencyProperty.Register(nameof(RowBackground), typeof(string), typeof(SkiaGridViewV2), new PropertyMetadata(default, RowBackgroundChanged));

        private static void RowBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid) return;
            grid._appearanceConfig.SetRowBackground((string)e.NewValue);
        }

        public string? AlternatingRowBackground
        {
            get { return (string?)GetValue(AlternatingRowBackgroundProperty); }
            set { SetValue(AlternatingRowBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AlternatingRowBackgroundProperty =
            DependencyProperty.Register(nameof(AlternatingRowBackground), typeof(string), typeof(SkiaGridViewV2), new PropertyMetadata(null, AlternatingRowBackgroundChanged));

        private static void AlternatingRowBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid) return;
            grid._appearanceConfig.SetAlternatingRowBackground((string)e.NewValue);
        }

        public bool ShowGridLines
        {
            get { return (bool)GetValue(ShowGridLinesProperty); }
            set { SetValue(ShowGridLinesProperty, value); }
        }

        public static readonly DependencyProperty ShowGridLinesProperty =
            DependencyProperty.Register(nameof(ShowGridLines), typeof(bool), typeof(SkiaGridViewV2), new PropertyMetadata(false, ShowgGridLineChanged));

        private static void ShowgGridLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid) return;
            grid._appearanceConfig.SetShowGridLines((bool)e.NewValue);
        }

        public string GridLinesColor
        {
            get { return (string)GetValue(GridLinesColorProperty); }
            set { SetValue(GridLinesColorProperty, value); }
        }

        public static readonly DependencyProperty GridLinesColorProperty =
            DependencyProperty.Register(nameof(GridLinesColor), typeof(string), typeof(SkiaGridViewV2), new PropertyMetadata(default, GridLinesColorChanged));

        private static void GridLinesColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SkiaGridViewV2 grid) return;
            grid._appearanceConfig.SetGridLinesColor((string)e.NewValue);
        }


        #endregion ColorProperties

        #region PrivateMethods

        // SortTimerChanged moved to SortingManager (Step 8)

        private void ParentWindow_Deactivated(object? sender, EventArgs e)
        {
            SkiaRenderer.SetWindowActive(RetainSelectionOnLFocusLost ? true : false);

        }

        private void ParentWindow_Activated(object? sender, EventArgs e)
        {
            SkiaRenderer.SetWindowActive(true);
        }
        private void CollectionViewChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // M2 perf fix: granular single-item changes splice the flat row list in place
            // instead of re-flattening all N rows. The full rebuild ran on EVERY source
            // CollectionChanged — measured 90.8% of the dispatcher (46.0 s / 50.7 s) at
            // 70k rows with a live feed (O(N) walk + N RowModel allocations, 4×/sec).
            // Falls back to the full flatten for anything it can't prove equivalent.
            if (TryIncrementalRowUpdate(e)) return;
            _groupingManager.UpdateCollection();
        }

        /// <summary>
        /// Incremental equivalent of GroupingManager.UpdateCollection for granular
        /// single-item Add/Remove in FLAT, UNGROUPED mode. In that mode FlattenRowItems
        /// produces exactly one RowModel per _viewList entry, in order — so a CV Add at
        /// view index i maps to Items.Insert(i, ...) and a Remove at i to Items.RemoveAt(i),
        /// followed by the same UpdateTotalRows() side effect the full path runs
        /// (TotalRows + canvas extent + scrollbars). Behavior parity is enforced by
        /// sanity checks; ANY doubt returns false → caller takes the full-flatten path.
        /// </summary>
        private bool TryIncrementalRowUpdate(NotifyCollectionChangedEventArgs e)
        {
            // Grouped rendering flattens GroupItemSource, not Items — full path only.
            if (GroupSettings != null) return false;
            // Tree mode interleaves expanded children — view index ≠ flat index. When NOTHING is
            // expanded the flat list is exactly the top-level rows in order (flat index == view
            // index), so the splice is still safe; only bail when a row is actually expanded.
            if (!string.IsNullOrEmpty(ChildProperty) && AnyRowExpanded) return false;

            var items = SkiaRenderer?.Items;
            if (items == null || _collectionView == null) return false;

            // Multi-item Add (K >= 1) — splice K RowModels contiguously at the mapped view index.
            // K > 1 only originates from the range API (InsertRange). The count invariant guarantees
            // the full flatten would have produced exactly items.Count + K rows.
            if (e.Action == NotifyCollectionChangedAction.Add
                && e.NewItems is { Count: >= 1 }
                && e.NewStartingIndex >= 0
                && e.NewStartingIndex <= items.Count
                && items.Count + e.NewItems.Count == _collectionView.Count)
            {
                for (int j = 0; j < e.NewItems.Count; j++)
                {
                    var newItem = e.NewItems[j]!;
                    items.Insert(e.NewStartingIndex + j,
                        new RowModel { Item = newItem, IsChildRow = false, HasChild = ItemHasChildren(newItem) });
                }
                RequestRowSettle();
                return true;
            }

            // Multi-item Remove (K >= 1) — remove a contiguous block. Verify each mapped slot really
            // holds its item BEFORE mutating anything; any mismatch → full path.
            if (e.Action == NotifyCollectionChangedAction.Remove
                && e.OldItems is { Count: >= 1 }
                && e.OldStartingIndex >= 0
                && e.OldStartingIndex + e.OldItems.Count <= items.Count
                && items.Count - e.OldItems.Count == _collectionView.Count)
            {
                for (int j = 0; j < e.OldItems.Count; j++)
                    if (!ReferenceEquals(items[e.OldStartingIndex + j].Item, e.OldItems[j]))
                        return false;
                for (int j = e.OldItems.Count - 1; j >= 0; j--)
                    items.RemoveAt(e.OldStartingIndex + j);
                RequestRowSettle();
                return true;
            }

            return false; // Reset / Move / Replace / index unknown → full flatten
        }

        // True if any row is currently expanded. Collapse leaves a `false` entry in the map, so this
        // tests for a `true` value, not Count == 0. Runs only on CollectionChanged, never per-frame.
        private bool AnyRowExpanded =>
            _groupingManager?.ExpandedItems is { Count: > 0 } exp && exp.Values.Any(v => v);

        // Mirrors DataFlatteningService's HasChild computation so an incrementally-spliced top-level
        // row still shows its expand toggle when it has children (ChildProperty reflection).
        private bool ItemHasChildren(object? item)
        {
            if (item == null || string.IsNullOrEmpty(ChildProperty)) return false;
            try
            {
                var raw = reflectionHelper.GetPropValue(item, ChildProperty);
                return raw is System.Collections.IEnumerable en && en.Cast<object>().Any();
            }
            catch { return false; }
        }

        // Delegates to GridColumnManager (extracted in Step 13)
        private void UpdateColumnsInDataGrid() => _columnManager.UpdateColumnsInDataGrid();
        private void SubscribeToColumnEvents(IEnumerable<SKGridViewColumn> cols) => _columnManager.SubscribeToColumnEvents(cols);
        private void UnSubscribeColumnEvent(SKGridViewColumn col) => _columnManager.UnSubscribeColumnEvent(col);
        private void SubscribeColumnEvent(SKGridViewColumn col) => _columnManager.SubscribeColumnEvent(col);
        private void Group_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is SKGroupDefinition column)
            {
                if (e.PropertyName == nameof(SKGroupDefinition.RowBackground))
                    SkiaRenderer.SetGroupRowBackgroundColor(column.RowBackground);
                if (e.PropertyName == nameof(SKGroupDefinition.ForegroundColor))
                    SkiaRenderer.SetGroupFontColor(column.ForegroundColor);
            }
        }
        private void SubscribeToGroupColumnEvents(SKGroupDefinition? group)
        {
            if (group != null) { group.PropertyChanged -= Group_PropertyChanged; group.PropertyChanged += Group_PropertyChanged; }
        }

        /// <summary>
        /// Height genuinely available to the rows: MainGrid's star row (row 1), which is whatever is
        /// left after the column-header row and the horizontal scroll bar row.
        /// <para>
        /// Measured from the ROW DEFINITION, never from <c>skiaContainer</c>. That container is a
        /// vertical StackPanel whose height follows its only child — the canvas — and the canvas
        /// height is itself derived from this value in <see cref="GetSkiaHeight"/>. Reading the
        /// container here closed a feedback loop: once the grid was grown until every row fitted, the
        /// canvas (and so the container) became the full content height, the overflow test then
        /// compared content against content, got 0, and the Auto bar could never come back however
        /// far the grid was shrunk afterwards. A star row is sized by the Grid from the space
        /// remaining, so it shrinks with the control and cannot be fed by its own children.
        /// </para>
        /// </summary>
        private double RowBandHeight()
        {
            if (MainGrid != null && MainGrid.RowDefinitions.Count > 1)
            {
                var band = MainGrid.RowDefinitions[1].ActualHeight;
                if (band > 0) return band;
            }
            // Before the first layout pass nothing is arranged yet; fall back to the control height.
            return MainGrid?.ActualHeight ?? 0;
        }

        private double GetSkiaHeight()
        {
            var band = RowBandHeight();
            var content = TotalRows * RowHeight;
            return band < content ? band : content;
        }

        private double GetSkiaWidth()
        {
            var totalcolumnwidth = GetVisibleColumnsWidth();
            var scrollBarWidth = Managers.ScrollManager.EffectiveVerticalBarWidth(VerticalScrollViewer);
            var availableWidth = MainGrid.ActualWidth - scrollBarWidth;
            return Math.Min(totalcolumnwidth, Math.Max(0, availableWidth));
        }

        internal void UpdateSkiaGrid()
        {
            SkiaCanvas.Height = GetSkiaHeight();
            SkiaCanvas.Width = GetSkiaWidth();

        }

        // Memoized sum of visible column widths. Recomputed lazily after InvalidateVisibleColumnsWidthCache()
        // is called at the four column-mutation sites (rebuild / IsVisible / Width / drag-resize). Reorder
        // and window SizeChanged do NOT change the sum, so they keep the cache — that is the win: every
        // incremental insert and SizeChanged stops re-summing ~88 columns.
        private double? _visibleColumnsWidthCache;
        private double GetVisibleColumnsWidth() =>
            _visibleColumnsWidthCache ??= DataListView.Columns.Where(x => x.Visibility == Visibility.Visible).Sum(x => x.Width.Value);
        internal void InvalidateVisibleColumnsWidthCache() => _visibleColumnsWidthCache = null;

        // B4 fix: MarkDirty() removed (was never called)

        // Delegates to ScrollManager (extracted in Step 11)
        private void UpdateScrollValues() => _scrollManager.UpdateScrollValues();

        private void SetScale()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                var res = source.CompositionTarget.TransformToDevice.M11;
                Scale = (float)res;
            }
            else
            {
                Scale = Helper.GetSystemDpi();
            }
        }

        // Delegates to SortingManager (extracted in Step 8)
        private void ApplySort(string propertyName, ListSortDirection direction)
            => _sortingManager.ApplySort(propertyName, direction);

        /// <summary>
        /// Remove any active sort from the grid and return rows to source-collection order.
        /// Resets each column's GridViewColumnSort to None and clears the WPF sort glyph.
        /// Header clicks cycle Asc → Desc → None on their own; call this when you need to
        /// programmatically reset sort from a button, command, or state restore path.
        /// </summary>
        public void ClearSort()
        {
            foreach (var c in Columns.Where(x => x.GridViewColumnSort != SkGridViewColumnSort.None))
                c.GridViewColumnSort = SkGridViewColumnSort.None;

            foreach (var wpfCol in DataListView.Columns)
                wpfCol.SortDirection = null;

            _sortingManager.ClearSort();
        }

        // Delegates to GroupingManager (extracted in Step 12)
        private void UpdateCollection()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateCollection);
                return;
            }
            _groupingManager.UpdateCollection();
            _automationPeer?.InvalidateRowCache();
        }

        /// <summary>
        /// Drop the automation peer's cached row + header peers and raise
        /// <see cref="System.Windows.Automation.AutomationEvents.StructureChanged"/>.
        /// Call this whenever the set of columns visible to UIA changes (IsVisible toggled,
        /// DisplayIndex shuffled, columns added/removed) so Accessibility Insights /
        /// WinAppDriver re-walk the children and pick up the new AutomationIds.
        /// Without this, a column that becomes visible at runtime via "View → Settings"
        /// shows on screen but its AutomationId is missing from the UIA tree until the
        /// window is closed and reopened (which rebuilds the peer from scratch).
        /// </summary>
        internal void InvalidateAutomationCache() => _automationPeer?.InvalidateRowCache();

        private void UpdateTotalRows()
        {
            if (GroupSettings != null)
            {
                TotalRows = SkiaRenderer.GroupItemSource.Where(x => x.IsExpanded == true || x.IsGroupHeader).Count();
            }
            else if (SkiaRenderer != null)
            {
                TotalRows = SkiaRenderer.Items.Count;
            }
            // Scroll values BEFORE the canvas (see the Loaded handler). This is the path a data
            // change takes, so it is where the bar most often turns OFF: rows go to zero when a
            // symbol is unsubscribed, the bar collapses — and sizing the canvas first left it a
            // bar-width short, so the bar vanished but its empty gutter stayed behind.
            UpdateScrollValues();
            UpdateSkiaGrid();
        }

        private bool _rowSettlePending;
        // Coalescing chokepoint for the incremental splice path. When CoalesceRowUpdates is off
        // (default), this is a direct synchronous UpdateTotalRows() — identical to prior behavior.
        // When on, it posts a single Background-priority settle so a burst of splices within one
        // dispatcher pump collapses to ONE UpdateTotalRows() after the last splice.
        private void RequestRowSettle()
        {
            if (!CoalesceRowUpdates) { UpdateTotalRows(); return; }
            if (_rowSettlePending) return;
            _rowSettlePending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _rowSettlePending = false; // reset first so a later splice can re-arm even if this throws
                UpdateTotalRows();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // Delegates to GroupingManager (extracted in Step 12)
        internal void UpdateGroupToggle(double x, double y) => _groupingManager.UpdateGroupToggle(x, y, TotalRows);
        internal void UpdateRowToggle(double x, double y) => _groupingManager.UpdateRowToggle(x, y, TotalRows);
        public void ToggleGroup(string groupName, bool IsExpand) => _groupingManager.ToggleGroup(groupName, IsExpand);
        public void CollapseAll() => _groupingManager.CollapseAll();
        public void ExpandAll() => _groupingManager.ExpandAll();

        // Reset/Flatten methods delegated to GroupingManager (Step 12)
        private void ResetGroupToggleValues() => _groupingManager.ResetGroupToggleValues();
        private void ResetRowToggleValues() => _groupingManager.ResetRowToggleValues();

        private void InvokeHeaderClickWithDelay()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InvokeDataGridHeaderClick();
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Walk the DataGrid's header visual tree and (re)attach the column-header click
        /// handlers. Returns true if at least one header was found — the caller uses this
        /// to know whether WPF has realized the headers yet (it does so asynchronously
        /// after a column change), so the dirty flag is only cleared once the hookup
        /// actually happened. Expensive (full visual-tree walk + per-header delegate
        /// swaps); must run only on a structural column change, NOT on every LayoutUpdated.
        /// </summary>
        private bool InvokeDataGridHeaderClick()
        {
            bool foundAny = false;
            var headers = FindVisualChildren<DataGridColumnHeader>(DataListView);
            foreach (var header in headers)
            {
                foundAny = true;
                header.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(ColumnHeader_LeftClick));
                header.RemoveHandler(UIElement.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(ColumnHeader_RightClick));

                header.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(ColumnHeader_LeftClick), true);
                header.AddHandler(UIElement.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(ColumnHeader_RightClick), true);
            }
            return foundAny;
        }

        /// <summary>
        /// Flag the column-header click handlers as needing (re)attachment. Set on any
        /// structural column change (initial build, add/remove, reorder, visibility toggle).
        /// The next LayoutUpdated — by which point WPF has realized the new headers —
        /// performs the walk once and clears the flag. Cheap to call repeatedly.
        /// </summary>
        internal void MarkHeadersDirty() => _headersDirty = true;

        private void ColumnHeader_LeftClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridColumnHeader header)
            {
                var col = Columns?.FirstOrDefault(x => x.Header == header.Content?.ToString() || x.DisplayHeader == header.Content?.ToString());
                ColumnLeftClick?.Invoke(col!);
            }
        }

        private void ColumnHeader_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridColumnHeader header)
            {
                var col = Columns.FirstOrDefault(x => x.Header == header.Content?.ToString() || x.DisplayHeader == header.Content?.ToString());
                ColumnRightClick?.Invoke(col!);
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        #endregion PrivateMethods

        #region Template
        public SKCellTemplate? CellTemplate
        {
            get => (SKCellTemplate?)GetValue(CellTemplateProperty);
            set => SetValue(CellTemplateProperty, value);
        }
        public static readonly DependencyProperty CellTemplateProperty =
            DependencyProperty.Register(nameof(CellTemplate), typeof(SKCellTemplate), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        public SKRowTemplate? RowTemplate
        {
            get => (SKRowTemplate?)GetValue(RowTemplateProperty);
            set => SetValue(RowTemplateProperty, value);
        }
        public static readonly DependencyProperty RowTemplateProperty =
            DependencyProperty.Register(nameof(RowTemplate), typeof(SKRowTemplate), typeof(SkiaGridViewV2), new PropertyMetadata(null));

        #endregion Template



        // Scroll event handlers delegated to ScrollManager (Step 11)
        private void HorizontalScrollViewer_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => _scrollManager.HandleHorizontalValueChanged(sender, e);
        private void HorizontalScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _scrollManager.HandleHorizontalPreviewMouseUp(sender, e);
        private void HorizontalScrollViewer_Scroll(object sender, ScrollEventArgs e) => _scrollManager.HandleHorizontalScroll(sender, e);
        internal void UpdateHorizontalScroll() => _scrollManager.UpdateHorizontalScroll();
        private void VerticalScrollViewer_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => _scrollManager.HandleVerticalValueChanged(sender, e);
        private void VerticalScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _scrollManager.HandleVerticalPreviewMouseUp(sender, e);
        private void VerticalScrollViewer_Scroll(object sender, ScrollEventArgs e) => _scrollManager.HandleVerticalScroll(sender, e);
        internal void UpdateVerticalScroll()
        {
            _scrollManager.UpdateVerticalScroll();
            // The peer tree is viewport-virtualized: scrolling changes WHICH rows are
            // exposed, so attached automation clients need a (coalesced) structure
            // notification to re-walk. No-op without listeners / when disabled.
            _automationPeer?.NotifyViewportChanged();
        }

        // Delegates to SelectionManager (extracted in Step 9)
        public void SelectAllRows()
        {
            try
            {
                if (SkiaRenderer.GroupItemSource == null && _collectionView == null)
                    return;

                // Use flattened items (includes expanded children) for select all
                var items = GroupSettings == null
                    ? SkiaRenderer.Items?.Select(x => x.Item)?.Cast<object>().ToList() ?? new()
                    : SkiaRenderer.GroupItemSource.Where(x => x.IsExpanded || x.IsGroupHeader)
                                                  .Select(x => (object)(x.Item ?? x))
                                                  .ToList();

                if (items.Count == 0) return;

                _selectionManager.SelectedItems = SelectedItems;
                _selectionManager.SelectAll(items);
                SelectedItems = _selectionManager.SelectedItems!;
                RaiseAutomationSelectionChanged();
            }
            catch { }
        }

        // ── UI Automation-driven selection ──────────────────────────────────────
        //
        // A UIA SelectionItemPattern.Select()/AddToSelection()/RemoveFromSelection()
        // must be indistinguishable from a user left-click: it drives the SAME
        // SelectionManager, mutates the SAME bound SelectedItems ObservableCollection
        // (so a TwoWay binding pushes to the VM synchronously), and fires the SAME
        // SelectionChangedCommand — so dependent ICommand.CanExecute re-queries and a
        // context menu's items enable exactly as they do for a click. Called on the UI
        // thread (the row peer marshals via the Dispatcher when the UIA call is off-thread).
        internal enum AutomationSelectMode { Replace, Add, Remove }

        internal bool SelectRowFromAutomation(int rowIndex)
            => PerformAutomationSelection(rowIndex, AutomationSelectMode.Replace);

        internal bool AddRowToSelectionFromAutomation(int rowIndex)
            => PerformAutomationSelection(rowIndex, AutomationSelectMode.Add);

        internal bool RemoveRowFromSelectionFromAutomation(int rowIndex)
            => PerformAutomationSelection(rowIndex, AutomationSelectMode.Remove);

        private bool PerformAutomationSelection(int rowIndex, AutomationSelectMode mode)
        {
            if (!CanUserSelectRows) return false;
            if (SkiaRenderer?.GroupItemSource == null) return false;

            // Build the flattened row list EXACTLY as MouseHandler does (group headers
            // carry the GroupModel itself; data rows carry .Item).
            var itemsource = GroupSettings == null
                ? SkiaRenderer.Items?.Select(i => i.Item)?.Cast<object>().ToList()
                : SkiaRenderer.GroupItemSource.Where(i => i.IsExpanded || i.IsGroupHeader)
                                              .Select(i => (object)(i.Item ?? i)).ToList();
            if (itemsource == null || rowIndex < 0 || rowIndex >= itemsource.Count) return false;
            List<dynamic> s = itemsource;

            // Sync DP → SelectionManager (identical prologue to MouseHandler.HandleLeftButtonDown).
            _selectionManager.SelectedItems = SelectedItems;
            _selectionManager.EnsureInitialized();
            SelectedItems = _selectionManager.SelectedItems!;

            bool changed;
            switch (mode)
            {
                case AutomationSelectMode.Add:
                    changed = _selectionManager.AddToSelection(s, rowIndex);
                    break;
                case AutomationSelectMode.Remove:
                    changed = _selectionManager.RemoveFromSelection(s, rowIndex);
                    break;
                default:
                    _selectionManager.HandleSingleClick(s, rowIndex);
                    changed = true;
                    break;
            }

            // Same selection-changed notification a left-click raises.
            if (SelectionChangedCommand != null && SelectionChangedCommand.CanExecute(SelectedItems))
                SelectionChangedCommand.Execute(SelectedItems);

            SkiaCanvas?.InvalidateVisual();
            RaiseAutomationSelectionChanged();
            return changed;
        }

        /// <summary>
        /// Ask the automation peer (if one exists) to raise UIA selection events for the
        /// current selection state. Change-gated inside the peer — a call that finds no
        /// net change raises nothing, so this is safe to invoke from every selection path
        /// (click / keyboard / SelectAll / UIA / VM swap) without spamming events.
        /// No-op when no peer has been created (no UIA client has queried the grid).
        /// </summary>
        internal void RaiseAutomationSelectionChanged() => _automationPeer?.RaiseSelectionChanged();

        /// <summary>
        /// Open the row context menu (the one wired via <see cref="ItemsContextMenu"/>, i.e.
        /// SkiaCanvas.ContextMenu) positioned at the given flattened row — used by the row
        /// peer's IInvokeProvider so a UIA test can open a row's menu without a coordinate
        /// right-click. Selects the row first (so command CanExecute reflects it), exactly
        /// like the user right-click path. Returns false when there is no menu to open.
        /// </summary>
        internal bool OpenRowContextMenuFromAutomation(int rowIndex)
        {
            SelectRowFromAutomation(rowIndex);

            var menu = ItemsContextMenu ?? ContextMenu;
            if (menu == null || SkiaCanvas == null) return false;

            menu.PlacementTarget = SkiaCanvas;
            menu.Placement = PlacementMode.Relative;
            menu.HorizontalOffset = 4;
            // Just below the row's top edge, in canvas-local coordinates.
            menu.VerticalOffset = rowIndex * RowHeight - ScrollOffsetY + RowHeight;
            menu.IsOpen = true;
            return true;
        }

        private void SkiaCanvas_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            using var _metrics = Diagnostics.GridMetrics.Measure(Diagnostics.GridMetrics.RenderPaintSurface);
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear();
            canvas.Scale(Scale);
            canvas.Save();
            canvas.Translate(-ScrollOffsetX, -ScrollOffsetY);
            Draw(canvas);
            canvas.Restore();
        }

        private void Draw(SKCanvas canvas)
        {
            try
            {
                // Build render context once, reuse (avoid per-frame allocation)
                if (SkiaRenderer.RenderContext == null)
                {
                    SkiaRenderer.RenderContext = new Renderer.GridRenderContext
                    {
                        GroupToggleDetails = GroupToggleDetails,
                        RowToggleDetails = RowToggleDetails,
                        ButtonDetails = ButtonDetails,
                        RowTemplate = RowTemplate,
                        CellTemplate = CellTemplate,
                        ExpandedItems = _groupingManager?.ExpandedItems
                    };
                    SkiaRenderer.InitButtonRenderer();
                }
                SkiaRenderer.SelectionStyle = SelectionStyle;
                SkiaRenderer.Draw(canvas, ScrollOffsetX, ScrollOffsetY, RowHeight, TotalRows);
            }
            catch (Exception ex)
            {
                throw; // B34 fix: preserve stack trace
            }
        }

        // Delegates to SortingManager (extracted in Step 8)
        private void DataListView_Sorting(object sender, DataGridSortingEventArgs e)
            => _sortingManager.HandleSortClick(e, SortEvery);

        private void MainGrid_LostFocus(object sender, RoutedEventArgs e)
        {
        }

        // Delegates to KeyboardHandler (extracted in Step 10)
        private void SkiaCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            // Escape abandons a drag without dropping — the conventional escape hatch, and it must
            // win over the keyboard handler so navigation keys cannot fire mid-drag.
            if (e.Key == Key.Escape && _rowDragController.IsDragging)
            {
                _rowDragController.Cancel();
                e.Handled = true;
                return;
            }
            _keyboardHandler.HandleKeyDown(sender, e);
        }

        // Mouse handlers delegated to MouseHandler (Step 10)
        private void MainGrid_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
            => _mouseHandler.HandleMouseWheel(sender, e);

        private void MainGrid_MouseDown(object sender, MouseButtonEventArgs e)
            => _mouseHandler.HandleMouseDown(sender, e);

        private void SkiaCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
            => _mouseHandler.HandleRightButtonDown(sender, e);

        private void SkiaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _mouseHandler.HandleLeftButtonDown(sender, e);

        private void skiaContainer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
            => _mouseHandler.HandleContainerPreviewMouseDown(sender, e);

        // ── Per-cell hover tooltip (opt-in via IsTooltipEnabled) ─────────────────
        // Single reused ToolTip. Work happens only on cell-to-cell transitions (the
        // _lastTooltipCell gate), never per mouse-move pixel, and never touches the
        // render loop / InvalidateVisual — the tooltip is a separate WPF popup.
        private System.Windows.Controls.ToolTip? _cellToolTip;
        // Dedupe key includes the hovered BUTTON identity (data + name), not just (row,col),
        // so moving the pointer between two buttons in the SAME cell still re-resolves the
        // tooltip. UpdateButtonHover runs before UpdateCellTooltip on each move, so the button
        // fields are already current when the key is built.
        private (int Row, int Col, object? BtnData, string? BtnName) _lastTooltipCell = (-1, -1, null, null);

        private void SkiaCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var p = e.GetPosition(SkiaCanvas);
            // A live row drag owns the move: hover repaints and the tooltip popup are both
            // meaningless mid-drag, and the popup would take the mouse off the canvas.
            if (_rowDragController.Update(p)) return;
            UpdateButtonHover(p);                        // always — opt-in per SkButton hover color
            if (IsTooltipEnabled) UpdateCellTooltip(p);  // tooltip is a separate opt-in
        }

        private void SkiaCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _rowDragController.Complete(e.GetPosition(SkiaCanvas));
            // No drag claimed the gesture, so it was a plain click after all — collapse now.
            ApplyPendingSelectionCollapse();
        }

        // Capture can be taken away from us (another control grabs it, the window deactivates).
        // Without this the indicator would stay painted and the next move would keep tracking.
        private void SkiaCanvas_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _rowDragController.Cancel();
            // Never carry a deferred collapse into a later gesture — it names a row index in a row
            // list that may no longer exist.
            ClearPendingSelectionCollapse();
        }

        private void UpdateCellTooltip(System.Windows.Point p)
        {
            if (RowHeight <= 0) return;
            int row = (int)((p.Y + ScrollOffsetY) / RowHeight);
            int col = HitTestColumnIndex(p.X + ScrollOffsetX);

            // Only act when the pointer crosses into a different cell OR onto a different
            // button within the same cell.
            var key = (row, col, _hoveredButtonKey.Data, _hoveredButtonKey.Name);
            if (key == _lastTooltipCell) return;
            _lastTooltipCell = key;

            string? text = ResolveTooltipText(row, col);

            if (string.IsNullOrEmpty(text)) { HideCellTooltip(); return; }
            ShowCellTooltip(text!);
        }

        /// <summary>
        /// Single owner of the tooltip-text decision (the reused WPF ToolTip has one source).
        /// Precedence:
        ///   (a) a hovered SkButton's <c>TooltipProvider</c> (then <c>TooltipPath</c>), resolved
        ///       against the row's data item;
        ///   (b) the hovered column's <c>TooltipPath</c>, resolved against the row's data item
        ///       (regardless of truncation);
        ///   (c) the existing truncated-bound-text fallback.
        /// Returns null when none apply. Gated by IsTooltipEnabled at the call site; runs on
        /// hover-change only, never per frame — no work is added to the draw loop.
        /// </summary>
        private string? ResolveTooltipText(int row, int col)
        {
            // (a) Hovered button — provider first, then path; both against THIS row's data item.
            if (_hoveredButtonRef is { } btn && _hoveredButtonKey.Data is { } btnData)
            {
                if (btn.TooltipProvider != null)
                {
                    var provided = btn.TooltipProvider(btnData);
                    if (!string.IsNullOrEmpty(provided)) return provided;
                }
                if (!string.IsNullOrEmpty(btn.TooltipPath))
                {
                    var resolved = SkiaRenderer?.ResolveTextPath(btnData, btn.TooltipPath);
                    if (!string.IsNullOrEmpty(resolved)) return resolved;
                }
            }

            // (b) column TooltipPath + (c) truncated-text fallback — owned by the renderer.
            return (row >= 0 && col >= 0) ? SkiaRenderer?.GetCellTooltip(row, col) : null;
        }

        // ── Button hover color (opt-in via SkButton.HoverBackground/ForegroundColor) ──
        // Repaints ONLY when the hovered button changes AND the old or new button actually
        // defines a hover color — so grids without hover colors never repaint on hover.
        private (object? Data, string? Name) _hoveredButtonKey;
        private SkButton? _hoveredButtonRef;

        private void UpdateButtonHover(System.Windows.Point p)
        {
            var br = SkiaRenderer?._buttonRenderer;
            if (br == null) return;

            double hx = p.X + ScrollOffsetX;
            double hy = p.Y + ScrollOffsetY;

            // Newest matching entry wins (same LastOrDefault semantics as click hit-test).
            var hit = ButtonDetails
                .Where(v => hx >= v.Value.x && hx <= v.Value.x + v.Value.width
                         && hy >= v.Value.y && hy <= v.Value.y + v.Value.height)
                .LastOrDefault();
            var newBtn = hit.Value.btn;
            (object? Data, string? Name) newKey = newBtn != null ? (hit.Key.Item1, hit.Key.name) : (null, null);

            // Unchanged hovered button → nothing to do.
            if (Equals(newKey.Data, _hoveredButtonKey.Data) && newKey.Name == _hoveredButtonKey.Name)
                return;

            bool oldHadHover = _hoveredButtonRef is { } ob && (ob.HoverBackgroundColor != null || ob.HoverForegroundColor != null);
            bool newHasHover = newBtn is { } nb && (nb.HoverBackgroundColor != null || nb.HoverForegroundColor != null);

            _hoveredButtonKey = newKey;
            _hoveredButtonRef = newBtn;
            br.HoveredButton = newKey;

            // Repaint only if the appearance actually changes.
            if (oldHadHover || newHasHover)
                SkiaCanvas.InvalidateVisual();
        }

        private void ClearButtonHover()
        {
            var br = SkiaRenderer?._buttonRenderer;
            bool oldHadHover = _hoveredButtonRef is { } ob && (ob.HoverBackgroundColor != null || ob.HoverForegroundColor != null);
            _hoveredButtonKey = (null, null);
            _hoveredButtonRef = null;
            if (br != null) br.HoveredButton = (null, null);
            if (oldHadHover) SkiaCanvas.InvalidateVisual();
        }

        private void SkiaCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // A press whose collapse is still pending but whose pointer has left: drop it.
            if (!_rowDragController.IsDragging) ClearPendingSelectionCollapse();
            _lastTooltipCell = (-1, -1, null, null);
            HideCellTooltip();
            ClearButtonHover();
        }

        /// <summary>Visible-column index at a canvas-space X (scroll already added). -1 if none.</summary>
        private int HitTestColumnIndex(double x)
        {
            var cols = DataListView.Columns
                .Where(c => c.Visibility == Visibility.Visible)
                .OrderBy(c => c.DisplayIndex)
                .ToList();
            double cx = x;
            for (int i = 0; i < cols.Count; i++)
            {
                cx -= cols[i].ActualWidth;
                if (cx <= 0) return i;
            }
            return -1;
        }

        private void ShowCellTooltip(string text)
        {
            _cellToolTip ??= new System.Windows.Controls.ToolTip
            {
                // PlacementTarget is required for a free-standing ToolTip (one not attached
                // to an element's ToolTip property) to resolve a position — without it the
                // popup may not render at all.
                PlacementTarget = SkiaCanvas,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
            };

            if (_cellToolTip.IsOpen && Equals(_cellToolTip.Content, text))
                return; // already showing this exact text — don't churn the popup

            // Close-then-reopen so it repositions at the new cell and refreshes content.
            _cellToolTip.IsOpen = false;
            _cellToolTip.Content = text;
            _cellToolTip.IsOpen = true;
        }

        private void HideCellTooltip()
        {
            if (_cellToolTip != null) _cellToolTip.IsOpen = false;
        }

        internal void DataListView_ColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            if (sender is DataGrid items)
            {
                var columns = new List<SKGridViewColumn>();

                foreach (var item in items.Columns.OrderBy(c => c.DisplayIndex))
                {
                    var existingItem = Columns.FirstOrDefault(x => x.Header == item.Header?.ToString() || x.DisplayHeader == item.Header?.ToString());
                    UnSubscribeColumnEvent(existingItem!);
                    existingItem.DisplayIndex = item.DisplayIndex;

                    SubscribeColumnEvent(existingItem!);
                }
                // Scroll values before the canvas — a reorder changes the total column width, which
                // can flip the horizontal bar and therefore the space left for the canvas.
                UpdateScrollValues();
                UpdateSkiaGrid();
                SkiaRenderer.UpdateVisibleColumns();
                Refresh();
                // User drag-reordered a column — header visuals are regenerated, so the
                // click handlers need re-attaching on the next layout pass.
                MarkHeadersDirty();
            }
        }

        private bool _isRefreshing = false;

        public void Refresh()
        {
            // Removed per-frame logging — too frequent, causes debug lag
            if (_isRefreshing) return;
            _isRefreshing = true;

            try
            {
                // Flush any deferred collection view refreshes (throttled property changes)
                _collectionView?.RefreshIfDirty();

                SkiaCanvas.InvalidateVisual();
            }
            finally
            {
                // Robustness: always clear the re-entrancy guard, even if the flush or
                // InvalidateVisual() throws (e.g. a cross-thread InvalidOperationException).
                // Without this, a single throwing call latches _isRefreshing=true and every
                // later (correct-thread) Refresh() on this instance no-ops forever.
                _isRefreshing = false;
            }
        }

        private readonly Services.GridExportService _exportService = new();

        public string ExportData(SKExportType exportType)
        {
            return _exportService.Export(
                exportType,
                Columns,
                SkiaRenderer.Items,
                SkiaRenderer.GroupItemSource,
                GroupSettings,
                SelectedItems,
                reflectionHelper);
        }

        public void ScrollToVerticalOffset(double offset)
        {
            _scrollManager.ScrollToVerticalOffset(offset);
            // Viewport moved — see UpdateVerticalScroll for why the peer is notified.
            _automationPeer?.NotifyViewportChanged();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        // Start dirty so the initial column set gets hooked on the first layout pass.
        private bool _headersDirty = true;

        private void DataListView_LayoutUpdated(object sender, EventArgs e)
        {
            // PERF: LayoutUpdated is a tree-global, high-frequency signal. The previous
            // code ran a full header-tree walk + per-header delegate swaps on EVERY fire
            // (the cold-open walk-storm — FindVisualChildren was the #1 UI-thread frame,
            // ~297 walks per open). Gate the walk behind a dirty flag set only by
            // structural column changes: now it runs once per change instead of per
            // layout pass. Clear the flag ONLY after headers were actually found — WPF
            // realizes column headers asynchronously after a column change, so an early
            // fire finds none and must leave the flag set to retry on a later fire.
            if (!_headersDirty) return;
            if (InvokeDataGridHeaderClick())
                _headersDirty = false;
        }

        // B6 fix: Complete Dispose — unsubscribe ALL event handlers
        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Managers
                _sortingManager?.Dispose();
                SkiaRenderer?._buttonRenderer?.Dispose();
                SkiaRenderer?.Dispose();

                // Canvas events
                SkiaCanvas.PaintSurface -= SkiaCanvas_PaintSurface;
                SkiaCanvas.MouseLeftButtonDown -= SkiaCanvas_MouseLeftButtonDown;
                SkiaCanvas.MouseRightButtonDown -= SkiaCanvas_MouseRightButtonDown;
                SkiaCanvas.MouseMove -= SkiaCanvas_MouseMove;
                SkiaCanvas.MouseLeave -= SkiaCanvas_MouseLeave;
                SkiaCanvas.MouseLeftButtonUp -= SkiaCanvas_MouseLeftButtonUp;
                SkiaCanvas.LostMouseCapture -= SkiaCanvas_LostMouseCapture;
                SkiaCanvas.KeyDown -= SkiaCanvas_KeyDown;
                // Releases capture and drops the auto-scroll DispatcherTimer, which would
                // otherwise keep the controller (and through it this grid) alive.
                _rowDragController?.Teardown();
                HideCellTooltip();

                // DataGrid events
                DataListView.Sorting -= DataListView_Sorting;
                DataListView.ColumnReordered -= DataListView_ColumnReordered!;
                DataListView.LayoutUpdated -= DataListView_LayoutUpdated;

                // Scroll events
                HorizontalScrollViewer.ValueChanged -= HorizontalScrollViewer_ValueChanged;
                HorizontalScrollViewer.Scroll -= HorizontalScrollViewer_Scroll;
                VerticalScrollViewer.ValueChanged -= VerticalScrollViewer_ValueChanged;
                VerticalScrollViewer.Scroll -= VerticalScrollViewer_Scroll;

                // MainGrid events
                MainGrid.MouseWheel -= MainGrid_MouseWheel;
                MainGrid.LostFocus -= MainGrid_LostFocus;
                MainGrid.MouseDown -= MainGrid_MouseDown;

                // Container events
                skiaContainer.PreviewMouseDown -= skiaContainer_PreviewMouseDown;

                // Parent window events
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Activated -= ParentWindow_Activated;
                    parentWindow.Deactivated -= ParentWindow_Deactivated;
                }

                // Group events
                if (GroupSettings != null)
                    GroupSettings.PropertyChanged -= Group_PropertyChanged;

                // Column events
                if (Columns != null)
                {
                    foreach (var col in Columns)
                        _columnManager?.UnSubscribeColumnEvent(col);
                }

                // Collection view
                if (_collectionView != null)
                {
                    _collectionView.CollectionChanged -= CollectionViewChanged;
                    _collectionView.Dispose();
                }

                // Clear caches to release compiled delegates and cached state.
                // Selection cache is per-instance on CellRenderer (owned by SkiaRenderer)
                // and is cleaned up implicitly by SkiaRenderer.Dispose() above.
                reflectionHelper?.ClearCache();
                SkiaRenderer?._cellRenderer?.InvalidateSelectionCache();
            }
        }

        // Delegates to FilterManager (extracted in Step 7)
        public void AddOrUpdateFilter(Filter filter) => _filterManager.AddOrUpdateFilter(filter);
        public void RemoveFilter(Filter filter) => _filterManager.RemoveFilter(filter);

        /// <summary>Filter by exact value comparison (e.g., Price > 100).</summary>
        public void AddValueFilter(string column, string @operator, string value, Type dataType) => _filterManager.AddValueFilter(column, @operator, value, dataType);
        /// <summary>Filter by wildcard text match (e.g., Name matches "ACCT*").</summary>
        public void AddTextFilter(string column, string text) => _filterManager.AddTextFilter(column, text);
        /// <summary>Filter by list of allowed values (e.g., Status in ["Open", "Filled"]).</summary>
        public void AddListFilter(string column, List<string> list) => _filterManager.AddListFilter(column, list);
        /// <summary>Remove any active filter on the specified column.</summary>
        public void RemoveFilter(string column) => _filterManager.RemoveFilter(column);

        public void ScrollToHorizontalOffset(double offset) => _scrollManager.ScrollToHorizontalOffset(offset);

        private void DataListView_ColumnReordering(object sender, DataGridColumnReorderingEventArgs e)
        {
            ColumnReordering?.Invoke(this, e);
        }

        public event EventHandler<DataGridColumnReorderingEventArgs> ColumnReordering;
        [Obsolete("Manipulate your source ObservableCollection directly. Use source.Move(oldIndex, newIndex).")]
        public void MoveRowUp()
        {
            if (SelectedItems != null && SelectedItems.Count == 1 && _collectionView != null)
            {
#pragma warning disable CS0618
                _collectionView.MoveRowUp(SelectedItems!.FirstOrDefault()!);
#pragma warning restore CS0618
            }
        }

        [Obsolete("Manipulate your source ObservableCollection directly. Use source.Move(oldIndex, newIndex).")]
        public void MoveRowDown()
        {
            if (SelectedItems != null && SelectedItems.Count == 1 && _collectionView != null)
            {
#pragma warning disable CS0618
                _collectionView.MoveRowDown(SelectedItems!.FirstOrDefault()!);
#pragma warning restore CS0618
            }
        }

        [Obsolete("Manipulate your source ObservableCollection directly. Use source.Insert(index, item).")]
        public void InsertBlankRow(object obj)
        {
            if (SelectedItems != null && SelectedItems.Count == 1 && _collectionView != null)
            {
#pragma warning disable CS0618
                _collectionView.InsertBlankRow(SelectedItems!.FirstOrDefault()!, obj);
#pragma warning restore CS0618
            }
        }

        [Obsolete("Manipulate your source ObservableCollection directly. Use source.Remove(item).")]
        public void DeleteBlankRow(object item)
        {
            if (item != null && _collectionView != null)
            {
#pragma warning disable CS0618
                _collectionView.DeleteEmptyRow(item);
#pragma warning restore CS0618
            }
        }
        public void RefreshCollection()
        {
            _collectionView?.Refresh();
            Refresh();
        }

        /// <summary>
        /// Internal accessor for the underlying collection view. The CV is a library
        /// implementation detail — consumers configure the grid through DPs and public
        /// methods only. Tests reach this via InternalsVisibleTo.
        /// </summary>
        internal Helpers.ICustomCollectionView? CollectionView => _collectionView;

        /// <summary>
        /// Number of rows currently visible in the view (after filters / grouping). For
        /// status / diagnostics display in consumer code. Returns 0 before the view is built.
        /// </summary>
        public int VisibleItemCount => _collectionView?.Count ?? 0;

        /// <summary>Number of active filters on the view (status / diagnostics).</summary>
        public int FilterCount => _collectionView?.Filters?.Count ?? 0;

        /// <summary>Number of groups currently in the view (status / diagnostics). 0 when grouping is inactive.</summary>
        public int GroupCount => _collectionView?.GroupList?.Count() ?? 0;

        // ── View-index lookup ──────────────────────────────────────────────────────
        //
        // When a sort or filter is active, an item's index in the source ObservableCollection
        // does not correspond to its rendered row position. After mutating the source, callers
        // need a way to ask "where is my item on screen?" — IndexOfInView returns that.
        //
        // Three cases are handled:
        //   1. Grouped view: walk the renderer's GroupItemSource (group-headers + items in
        //      display order). The returned index is the visual row index (group header rows
        //      count as rows).
        //   2. Ungrouped (flat or tree): walk the renderer's Items. Each RowModel.Item is the
        //      bound data instance; tree-child rows are interleaved after their parents, so
        //      this gives the actual rendered position even in tree mode.
        //   3. Item not present (filtered out, never added, removed): returns -1.

        /// <summary>
        /// Find the rendered row index of <paramref name="item"/> in the current view.
        /// Returns -1 if the item is not currently visible (filtered out, removed, or in a
        /// collapsed group). The result reflects sort / filter / grouping at call time.
        ///
        /// Typical usage: after <c>source.Insert(N, newItem)</c> when sort or filter is
        /// active, call <c>IndexOfInView(newItem)</c> to discover where it landed on screen.
        /// </summary>
        public int IndexOfInView(object? item)
        {
            if (item == null) return -1;

            // Grouped mode — walk the flattened group rows in display order. Mirror the
            // renderer's visibility filter (Draw uses `IsGroupHeader || IsExpanded`),
            // otherwise rows inside collapsed groups would inflate the visual index.
            if (GroupSettings != null && SkiaRenderer?.GroupItemSource != null)
            {
                int visualIndex = 0;
                foreach (var row in SkiaRenderer.GroupItemSource)
                {
                    if (!(row.IsGroupHeader || row.IsExpanded)) continue; // collapsed → invisible
                    if (ReferenceEquals(row.Item, item) || Equals(row.Item, item))
                        return visualIndex;
                    visualIndex++;
                }
                return -1;
            }

            // Ungrouped (flat or tree) — walk the renderer's row models.
            var items = SkiaRenderer?.Items;
            if (items == null) return -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i].Item, item) || Equals(items[i].Item, item))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Scroll the grid so <paramref name="item"/> is visible. Returns true if the item
        /// was found and scrolled to; false if it is not in the current view (filtered out,
        /// in a collapsed group, or absent).
        /// </summary>
        public bool ScrollIntoView(object? item)
        {
            int idx = IndexOfInView(item);
            if (idx < 0) return false;
            ScrollToVerticalOffset(idx * RowHeight);
            return true;
        }

        /// <summary>
        /// Insert <paramref name="item"/> into the grid so it appears at <paramref name="viewIndex"/>
        /// — the exact visual row position the caller intends — regardless of any active sort
        /// or filter. The item is also added to the bound source collection so the data model
        /// stays consistent. Use this for "Insert above selected row" / "Add blank row at row N
        /// for editing" patterns where you want the row to appear exactly where the user clicked,
        /// not at a sort-decided position.
        ///
        /// Behavior:
        ///   * Sort comparer is bypassed for this insert (item goes to viewIndex, not sort position).
        ///   * Filter is bypassed for this insert (item is shown even if it would normally be filtered).
        ///   * Subsequent refreshes (sort change, filter change, group change, live-sort) WILL
        ///     re-evaluate the item against current sort + filter. To keep an inserted blank row
        ///     in place during editing, set IsLiveSorting=false and don't change filter/sort until
        ///     editing commits.
        ///   * Grouping is not supported by this API — when GroupSettings is non-null this method
        ///     returns false without inserting. (Insertion at a flat view-index has no meaningful
        ///     mapping inside a grouped layout.)
        ///
        /// Returns true on success, false if grouping is active or ItemsSource is not a mutable IList.
        /// </summary>
        public bool InsertAtViewIndex(int viewIndex, object item)
        {
            if (item == null || _collectionView == null) return false;
            if (GroupSettings != null) return false;                 // not supported in grouped mode
            if (ItemsSource is not System.Collections.IList source) return false;

            // Side-channel: tell the CV to place the next inserted item at the given view-index,
            // bypassing the normal sort / filter logic. The CV consumes the override on the next
            // InsertNewItem call (which fires synchronously when we add to the source below).
            if (_collectionView is Helpers.CustomCollectionView cv)
                cv.RequestExplicitViewIndex(viewIndex);

            // Adding to source raises CollectionChanged(Add) synchronously, which routes
            // through Source_CollectionChanged → InsertNewItem → the override path above.
            source.Add(item);
            return true;
        }

        /// <summary>
        /// Insert K items at <paramref name="index"/> in one batch. When no sort/group/filter is
        /// active, this raises a single Add and the grid splices K rows in one pass (O(K), not a
        /// whole-book re-flatten). With a sort/filter/group active it degrades to per-item insertion
        /// (sorted/filtered placement preserved). Returns false if the view is unavailable.
        /// </summary>
        public bool InsertRange(int index, IReadOnlyList<object> items)
        {
            if (items == null || _collectionView is not Helpers.CustomCollectionView cv) return false;
            cv.InsertRange(index, items);
            return true;
        }

        /// <summary>
        /// Remove K items in one batch. Contiguous, ungrouped removals raise a single Remove and the
        /// grid splices in one pass; otherwise degrades to per-item removal. Returns false if the
        /// view is unavailable.
        /// </summary>
        public bool RemoveRange(IReadOnlyList<object> items)
        {
            if (items == null || _collectionView is not Helpers.CustomCollectionView cv) return false;
            cv.RemoveRange(items);
            return true;
        }

        /// <summary>Apply grouping by a property name at runtime.</summary>
        public void ApplyGroup(string propertyName)
        {
            _collectionView?.ApplyGroup(propertyName);
            _collectionView?.Refresh();
            Refresh();
        }

        /// <summary>
        /// Apply grouping AND configure the aggregations to display in each group header
        /// row. Convenience overload — replaces direct CV access in consumer code.
        /// Pass an empty enumerable (or call <see cref="ApplyGroup(string)"/>) for no aggregations.
        /// </summary>
        public void ApplyGroup(string propertyName, IEnumerable<SKGroupField> aggregations)
        {
            if (_collectionView != null)
                _collectionView.GroupFields = aggregations?.ToList() ?? new List<SKGroupField>();
            _collectionView?.ApplyGroup(propertyName);
            _collectionView?.Refresh();
            Refresh();
        }

        /// <summary>Clear active grouping at runtime.</summary>
        public void ClearGroup()
        {
            _collectionView?.ClearGroup();
            _collectionView?.Refresh();
            Refresh();
        }
    }
}

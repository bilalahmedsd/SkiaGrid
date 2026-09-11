# FastGrid - Complete Property & API Reference

**SkiaSharpControlV2 v2.18.0** | .NET 8 / WPF | MIT

Every member below was extracted directly from the source in `D:\Bilal\SkiaGrid\SkiaSharpControlV2`. Nothing is estimated.

| Section | Count |
|---|---|
| Grid dependency properties (settable from XAML) | 71 |
| Grid methods, events and read-only properties | 51 |
| Column, button, styling, grouping and row-drag model members | 141 |
| Enumeration values | 43 |
| **Total public API members documented** | **306** |

---

## 1. Grid Dependency Properties (71)

Settable from XAML on `<skia:SkiaGridViewV2 .../>`.

| Property | Type | Default | Category | What it does |
|---|---|---|---|---|
| IsAutomationEnabled | bool | true | Automation | Enables this grid's UI Automation peer tree; when false grids expose no peer (or an empty children list) and stop raising structure events, and toggling it forces a structure-changed raise. |
| Columns | SkGridColumnCollection | null | Columns | The collection of column definitions the grid renders and mirrors into the WPF header DataGrid. |
| GroupSettings | SKGroupDefinition | null | Grouping | The grouping definition (GroupBy path, header aggregation fields, group row colors); non-null switches the grid into grouped rendering. |
| ItemsSource | IEnumerable | default | Data | The bound data collection; a new value builds a fresh CustomCollectionView, re-applies live-sort/group settings, re-applies any column sort and resets scroll metrics. |
| SelectedItems | ObservableCollection<object> | default | Selection | The collection of selected row items; a swapped instance is re-pointed at the selection manager and renderer, repaints, and raises UIA selection events. |
| IsTooltipEnabled | bool | false | Interaction | Per-cell hover tooltips that show the full value for ellipsis-truncated cells (plus button/column tooltip paths); turning it off dismisses any open tooltip. |
| RetainSelectionOnLFocusLost | bool | false | Selection | Whether the selection highlight survives the parent window losing focus; setting it false clears SelectedItems. |
| CanUserSelectRows | bool | true | Selection | Whether rows may be selected by mouse, keyboard or automation; setting it false clears the current selection. |
| OnCellClicked | Action<object, SKGridViewColumn> | default | Interaction | Delegate invoked with the row data item and resolved column when a cell is clicked. |
| OnRowClicked | Action<object> | default | Interaction | Delegate invoked with the row data item when a row is left-clicked. |
| OnRowRightClicked | Action<object> | default | Interaction | Delegate invoked with the row data item when a row is right-clicked. |
| OnRowDoubleClicked | Action<object> | default | Interaction | Delegate invoked with the row data item when a row is double-clicked. |
| OnPreviewKeyDownEvent | Action<Key> | default | Interaction | Delegate the keyboard handler invokes with each key pressed on the Skia canvas before the grid's own handling. |
| OnSkGridDoubleClicked | Action | default | Interaction | Delegate invoked when the grid surface itself is double-clicked. |
| RowClickedCommand | ICommand | null | Interaction | Command executed on row left-click with the row data item as parameter. |
| RowDoubleClickedCommand | ICommand | null | Interaction | Command executed on row double-click with the row data item as parameter. |
| RowRightClickedCommand | ICommand | null | Interaction | Command executed on row right-click with the row data item as parameter. |
| CellClickedCommand | ICommand | null | Interaction | Command executed on cell click with a (Row, Column) tuple as parameter. |
| SortChangedCommand | ICommand | null | Sorting | Command executed after a sort is applied, with a (ColumnHeader, Direction) tuple as parameter. |
| SelectionChangedCommand | ICommand | null | Selection | Command executed whenever selection changes (click, keyboard, automation or deferred collapse) with the SelectedItems collection as parameter. |
| CheckboxToggledCommand | ICommand | null | Interaction | Command executed when a checkbox cell is toggled, with an (Item, BindingPath, NewValue) parameter. |
| ExpandChangedCommand | ICommand | null | Grouping | Command executed when a group header or tree parent row expands or collapses, with SkExpandChangedEventArgs as parameter; also gates whether expand payloads are materialized. |
| CanUserReorderColumns | bool | true | Columns | Whether the user can drag column headers to reorder them; pushed onto the header DataGrid's CanUserReorderColumns. |
| CanUserResizeColumns | bool | true | Columns | Whether the user can drag-resize column widths; pushed onto the header DataGrid's CanUserResizeColumns. |
| CanUserSortColumns | bool | true | Sorting | Whether clicking a column header sorts the grid; pushed onto the header DataGrid's CanUserSortColumns. |
| SKFontSize | float | 11f | Font | Font size used for Skia-drawn cell text, which also drives derived row and column-header heights, the mirrored header font, and a full grid-metrics recalculation. |
| Density | SKGridDensity | SKGridDensity.Compact | Appearance | Vertical density preset whose padding derives row and column-header heights from SKFontSize (Compact = font+3/font+7, Normal = font+6/font+12). |
| SKRowHeight | double | 0d | Appearance | Explicit data-row height in pixels overriding the density-derived value; 0 means derive from SKFontSize and Density. |
| SKColumnHeaderHeight | double | 0d | Appearance | Explicit column-header height in pixels overriding the density-derived value; 0 means derive from SKFontSize and Density, and it is ignored while the header is hidden. |
| ColumnHeaderBackground | string | null | Appearance | Column-header background as a hex color string, with null keeping the built-in #343434 and per-column BackColor and filter colors still overriding it. |
| ColumnHeaderForeground | string | null | Appearance | Column-header text color as a hex color string, with null keeping the built-in #E6E6E6. |
| ColumnHeaderSeparatorColor | string | null | Appearance | Color of the 1 px vertical separator line between column headers, with null keeping the built-in #1E1E1E. |
| SKFontFamily | string | "Microsoft Sans Serif" | Font | Font family used for Skia-drawn cell text and mirrored onto the header DataGrid; an unresolvable family is logged and ignored. |
| SKFontStyle | string | "Normal" | Font | Font style string for Skia-drawn text, parsed for "bold"/"italic" and mirrored onto the header DataGrid's FontStyle and FontWeight. |
| SortChanged | Action<string?, SkGridViewColumnSort?> | default | Sorting | Delegate invoked after a sort is applied, receiving the sorted column and its new direction. |
| SortChanging | Action | default | Sorting | Delegate invoked just before a sort is applied. |
| IsLiveSorting | bool | true | Sorting | Whether the collection view re-sorts automatically as items change; enabling it refreshes the view immediately. |
| AddNewRowAtBottomInGroup | bool | true | Grouping | Whether newly added rows are placed at the bottom of their group in the collection view instead of at the group's sorted position. |
| SortEvery | int? | null | Sorting | Interval in seconds for timer-based re-sorting; null or non-positive stops the sort timer. |
| UseCollectionViewSort | bool | true | Sorting | Whether a column's sort state installs a real CollectionView sort (true) or only renders the header glyph and fires notifications while the consumer sorts its own source (false). |
| CoalesceRowUpdates | bool | false | Behaviour | Whether incremental row splices defer the TotalRows/canvas-extent/scrollbar settle to one Background-priority dispatcher post so a burst of inserts collapses to a single settle. |
| FilterByGroup | bool | false | Filtering | Whether filters are evaluated per group on the collection view; changing it refreshes the view and repaints. |
| ChildProperty | string | null | Data | Name of the property on data items holding a child collection, enabling hierarchical expand/collapse (tree mode) without implementing ITreeItem. |
| ColumnHeaderVisible | bool | true | Columns | Whether the column-header row is shown; hiding it collapses the header height, recomputes grid metrics and invalidates the automation header cache. |
| IsDeferredScrollingEnabled | bool | false | Scrolling | Whether scrollbar dragging defers the content update until the thumb is released. |
| ColumnRightClick | Action<SKGridViewColumn> | default | Columns | Delegate invoked with the matched column definition when a column header is right-clicked. |
| ColumnLeftClick | Action<SKGridViewColumn> | default | Columns | Delegate invoked with the matched column definition when a column header is left-clicked. |
| HeaderContextMenu | ContextMenu | default | Interaction | Context menu assigned to the header DataGrid, shown on right-click over column headers. |
| ContextMenu | ContextMenu | default | Interaction | Context menu assigned to the Skia container, shown on right-click over the grid surface. |
| ItemsContextMenu | ContextMenu | default | Interaction | Context menu assigned to the Skia canvas, shown on right-click over rows and used by the automation row-menu path. |
| HorizontalScrollBarPositionChanged | Action<double> | default | Scrolling | Delegate invoked with the new horizontal scroll offset whenever the horizontal scrollbar position changes. |
| VerticalScrollBarVisibilityChanged | Action<bool> | default | Scrolling | Delegate invoked synchronously inside the scroll-value pass when the vertical scrollbar's Auto visibility flips, receiving the new shown state. |
| VerticalScrollBarPositionChanged | Action<double> | default | Scrolling | Delegate invoked with the new vertical scroll offset whenever the vertical scrollbar position changes. |
| RowDragMode | SKRowDragMode | SKRowDragMode.None | RowDrag | Whether rows can be dragged and what happens on drop (None, Reorder of the source IList, Notify only, or DragOut as a real WPF drag-and-drop); setting None cancels any drag in progress. |
| RowDragIndicatorColor | string | default(string) | RowDrag | Hex color of the drop-insertion line and the translucent ghost over dragged rows; null or empty uses the built-in #3399FF. |
| RowDragCompletedCommand | ICommand | default(ICommand) | RowDrag | Command executed after a DragOut drag finishes, with SKRowDragCompletedEventArgs carrying the effect the drop target applied. |
| RowDroppedCommand | ICommand | default(ICommand) | RowDrag | Command executed when a drag is released over the grid, with SKRowDroppedEventArgs whose Handled flag suppresses the built-in reorder. |
| HorizontalScrollBarVisible | SKScrollBarVisibility | SKScrollBarVisibility.Auto | Scrolling | Visibility policy for the horizontal scrollbar (Auto lets the scroll-value pass decide, Visible/Hidden force it) and triggers a grid-metrics recalculation on change. |
| VerticalScrollBarVisible | SKScrollBarVisibility | SKScrollBarVisibility.Auto | Scrolling | Visibility policy for the vertical scrollbar (Auto lets the scroll-value pass decide, Visible/Hidden force it) and triggers a grid-metrics recalculation on change. |
| VerticalScrollBarWidth | double | 0d | Scrolling | Width of the vertical scrollbar in pixels (MinWidth is lowered too so sub-system-metric values apply); 0 keeps WPF's system width. |
| HorizontalScrollBarHeight | double | 0d | Scrolling | Height of the horizontal scrollbar in pixels (MinHeight is lowered too so sub-system-metric values apply); 0 keeps the built-in 20 px. |
| SelectionStyle | SKSelectionStyle | SKSelectionStyle.Fill | Selection | How selected rows are highlighted - Fill changes background and text color, Border draws only an outline. |
| SelectedRowBackground | string | null | Selection | Background hex color of a selected row when SelectionStyle is Fill; null uses the built-in blue #0072C6. |
| SelectedRowForeground | string | null | Selection | Text hex color of a selected row when SelectionStyle is Fill; null uses white. |
| ForegroundColor | string | default | Appearance | Default cell text color for Skia-drawn rows, forwarded to the appearance config. |
| RowBackground | string | default | Appearance | Background color of data rows, forwarded to the appearance config. |
| AlternatingRowBackground | string | null | Appearance | Background color applied to alternate data rows; null disables row banding. |
| ShowGridLines | bool | false | Appearance | Whether horizontal/vertical grid lines are drawn between cells. |
| GridLinesColor | string | default | Appearance | Color of the drawn grid lines, forwarded to the appearance config. |
| CellTemplate | SKCellTemplate | null | Appearance | Grid-wide cell template (custom cell content such as SkButtons) passed to the renderer's render context. |
| RowTemplate | SKRowTemplate | null | Appearance | Grid-wide row template passed to the renderer's render context for custom row rendering. |

### Count by category

| Category | Properties |
|---|---|
| Interaction | 15 |
| Appearance | 13 |
| Scrolling | 8 |
| Selection | 7 |
| Sorting | 7 |
| Columns | 6 |
| RowDrag | 4 |
| Grouping | 3 |
| Font | 3 |
| Data | 2 |
| Automation | 1 |
| Behaviour | 1 |
| Filtering | 1 |

---

## 2. Grid Methods, Events & Read-only Properties (51)

| Member | Kind | Category | What it does |
|---|---|---|---|
| SkiaGridViewV2() : ctor | Method | Lifecycle | Public constructor that runs InitializeComponent and wires up every internal manager (renderer, appearance, columns, flattening, grouping, scroll, selection, mouse, row-drag, keyboard, sorting, filter) plus the Loaded/Unloaded/SizeChanged handlers. |
| IsGroupExpanded(string groupName) : bool | Method | Grouping | Returns true when the named group is expanded in grouped mode, reporting the default of true for unknown group names. |
| GetGroupExpandStates() : IReadOnlyDictionary<string, bool> | Method | Grouping | Returns a snapshot of the expand state per group name in grouped mode, empty before the first flatten. |
| IsRowExpanded(object item) : bool | Method | Grouping | Returns true when the given parent row's children are expanded in tree / ChildProperty mode. |
| GetExpandedRowItems() : IReadOnlyList<object> | Method | Grouping | Returns the parent rows that are currently expanded in tree / ChildProperty mode. |
| GetVisibleDataItems() : IReadOnlyList<object> | Method | Grouping | Returns the data rows currently visible, excluding rows of collapsed groups and header/subtotal rows while including expanded parents' children. |
| ToggleRow(object item, bool isExpand) : void | Method | Grouping | Programmatically expands or collapses one parent row's children in tree / ChildProperty mode, a no-op in grouped mode or when already in that state. |
| ExpandAllRows() : void | Method | Grouping | Expands every parent row in tree / ChildProperty mode, a no-op in grouped mode. |
| CollapseAllRows() : void | Method | Grouping | Collapses every parent row in tree / ChildProperty mode, a no-op in grouped mode. |
| ClearSort() : void | Method | Sorting | Removes any active sort, resetting each column's GridViewColumnSort to None, clearing the WPF header sort glyphs, and clearing the sort on the collection view. |
| ToggleGroup(string groupName, bool IsExpand) : void | Method | Grouping | Expands or collapses the named group by delegating to the grouping manager. |
| CollapseAll() : void | Method | Grouping | Collapses every group in grouped mode. |
| ExpandAll() : void | Method | Grouping | Expands every group in grouped mode. |
| SelectAllRows() : void | Method | Selection | Selects every flattened visible row (including expanded children and group headers), syncs the result back into SelectedItems, and raises the UI Automation selection-changed notification. |
| Refresh() : void | Method | Data | Flushes any deferred collection-view refresh and invalidates the Skia canvas to repaint, guarded against re-entrancy. |
| ExportData(SKExportType exportType) : string | Method | Export | Exports the grid contents (all rows or the current selection per the export type) to a tab-separated string via the internal GridExportService. |
| ScrollToVerticalOffset(double offset) : void | Method | Scrolling | Scrolls the grid vertically to the given offset and notifies the automation peer that the viewport moved. |
| Dispose() : void | Method | Lifecycle | Suppresses finalization and calls Dispose(true) to tear the control down. |
| Dispose(bool disposing) : void | Method | Lifecycle | Disposes the managers and renderer and unsubscribes every canvas, DataGrid, scrollbar, MainGrid, container, parent-window, group, column and collection-view event handler, then clears reflection and selection caches. |
| AddOrUpdateFilter(Filter filter) : void | Method | Filtering | Adds a filter or replaces the existing filter for its column by delegating to the FilterManager. |
| RemoveFilter(Filter filter) : void | Method | Filtering | Removes the given filter instance from the view. |
| AddValueFilter(string column, string op, string value, Type dataType) : void | Method | Filtering | Adds a filter that compares a column against a value using the given operator and data type (e.g. Price > 100). |
| AddTextFilter(string column, string text) : void | Method | Filtering | Adds a wildcard text-match filter on the given column (e.g. Name matches "ACCT*"). |
| AddListFilter(string column, List<string> list) : void | Method | Filtering | Adds a filter restricting a column to a list of allowed values (e.g. Status in ["Open","Filled"]). |
| RemoveFilter(string column) : void | Method | Filtering | Removes any active filter on the named column. |
| ScrollToHorizontalOffset(double offset) : void | Method | Scrolling | Scrolls the grid horizontally to the given offset via the ScrollManager. |
| MoveRowUp() : void | Method | Data | Obsolete helper that moves the single selected row up one position in the collection view; callers should move items in the source collection instead. |
| MoveRowDown() : void | Method | Data | Obsolete helper that moves the single selected row down one position in the collection view; callers should move items in the source collection instead. |
| InsertBlankRow(object obj) : void | Method | Data | Obsolete helper that inserts the supplied object as a blank row next to the single selected row in the collection view. |
| DeleteBlankRow(object item) : void | Method | Data | Obsolete helper that removes the given empty row from the collection view. |
| RefreshCollection() : void | Method | Data | Forces a full refresh of the underlying collection view and then repaints the grid. |
| IndexOfInView(object? item) : int | Method | Data | Returns the rendered row index of the item under the current sort/filter/grouping, and -1 when the item is not visible. |
| ScrollIntoView(object? item) : bool | Method | Scrolling | Scrolls so the item's row is visible, returning false when the item is not present in the current view. |
| InsertAtViewIndex(int viewIndex, object item) : bool | Method | Data | Inserts the item into the bound IList source while telling the collection view to place it at the exact visual row index, bypassing sort and filter; false when grouping is active or ItemsSource is not a mutable IList. |
| InsertRange(int index, IReadOnlyList<object> items) : bool | Method | Data | Inserts K items at the given index as one batch so the grid can splice the rows in a single pass, returning false when the collection view is unavailable. |
| RemoveRange(IReadOnlyList<object> items) : bool | Method | Data | Removes K items as one batch so contiguous ungrouped removals splice in a single pass, returning false when the collection view is unavailable. |
| ApplyGroup(string propertyName) : void | Method | Grouping | Applies grouping by the named property at runtime and refreshes the view and the canvas. |
| ApplyGroup(string propertyName, IEnumerable<SKGroupField> aggregations) : void | Method | Grouping | Applies grouping by the named property and configures the aggregation fields shown in each group header row before refreshing. |
| ClearGroup() : void | Method | Grouping | Clears active grouping at runtime and refreshes the view and the canvas. |
| ExpandChanged | Event | Grouping | Raised on the UI thread after grid state settles when a group header or tree parent row changes expand state, with AffectedItems carrying the rows that became visible or hidden (one event per bulk expand/collapse). |
| RowDragStarting | Event | RowDrag | Raised once per drag when the pointer passes the system drag threshold and before tracking starts, letting a handler set Cancel to refuse the drag or start its own WPF DragDrop. |
| RowDropped | Event | RowDrag | Raised when a drag is released over the grid; in Reorder mode the grid performs the source move after this event unless a handler sets Handled. |
| RowDragCompleted | Event | RowDrag | Raised after a DragOut drag finishes, carrying the DragDropEffects the drop target applied so the source collection can give up the rows itself. |
| ColumnReordering | Event | Columns | Re-raised from the header DataGrid when the user begins reordering a column, forwarding the DataGridColumnReorderingEventArgs. |
| ViewList | Property | Data | Read-only accessor returning the underlying collection view's ViewList (the sorted/filtered row sequence), or null before the view exists. |
| VerticalScrollBarActualWidth | Property | Scrolling | Read-only width in px that the vertical scroll bar currently occupies (0 when hidden), accounting for an override and falling back to the system metric before the first layout pass. |
| CanReorderRows | Property | RowDrag | Read-only flag that is true only when the built-in row reorder can actually run: no grouping, no active sort, and an ItemsSource that is a non-fixed-size, non-read-only IList. |
| VisibleItemCount | Property | Diagnostics | Read-only count of rows currently visible in the view after filters and grouping, returning 0 before the view is built. |
| FilterCount | Property | Diagnostics | Read-only count of active filters on the collection view. |
| GroupCount | Property | Diagnostics | Read-only count of groups currently in the view, 0 when grouping is inactive. |
| GlobalAutomationEnabled | StaticProperty | Automation | Process-wide master switch (default true) that ANDs with each grid's IsAutomationEnabled, so setting it false stops every SkiaGridViewV2 instance from exposing its UI Automation peer tree. |

---

## 3. Columns & In-cell Buttons

| Type | Property | Type of value | Default | What it does |
|---|---|---|---|---|
| SKBaseColumn | Name | string? | - | Logical identifier for the column, used to reference it in code and in group/toggle target lists. |
| SKBaseColumn | BindingPath | string | - | Dot-notation property path on the row data item whose value is displayed in this column's cells. |
| SKBaseColumn | ContentAlignment | CellContentAlignment | CellContentAlignment.Left | Horizontal alignment of the cell content within the column. |
| SKBaseColumn | CellTemplate | SKCellTemplate? | null | Cell template supplying setters, triggers, buttons, or a custom draw delegate for this column's cells. |
| SKBaseColumn | Format | string? | null | .NET format string applied to the bound value before rendering. |
| SKBaseColumn | ShowBracketOnNegative | bool | false | When true, negative numeric values are rendered wrapped in parentheses. |
| SKBaseColumn | FormatWithAcronym | bool | false | When true, large numeric values are abbreviated with a magnitude acronym (K/M/B style). |
| SKBaseColumn | DataVisible | bool | true | Controls whether the bound data text is painted for this column's cells. |
| SKBaseColumn | PropertyChanged | event | - | Raised when a column property changes so the grid can invalidate and repaint. |
| SKGridViewColumn | Header | string? | null | Header text identity of the column used for lookup and as the fallback header caption. |
| SKGridViewColumn | IsVisible | bool | true | Controls whether the column is rendered in the grid at all. |
| SKGridViewColumn | CanUserResize | bool? | true | Controls whether the user may drag the column edge to change its width. |
| SKGridViewColumn | CanUserReorder | bool? | true | Controls whether the user may drag the column to a new display position. |
| SKGridViewColumn | CanUserSort | bool? | true | Controls whether clicking the column header sorts by this column. |
| SKGridViewColumn | GridViewColumnSort | SkGridViewColumnSort | None | Current sort direction applied to this column (None/Ascending/Descending). |
| SKGridViewColumn | DisplayHeader | string? | null | Caption actually drawn in the column header, overriding Header for display purposes. |
| SKGridViewColumn | TooltipPath | string? | null | Data property whose value is shown as this cell's tooltip, resolved at hover time independently of BindingPath and text truncation; requires the grid's IsTooltipEnabled. |
| SKGridViewColumn | Width | double | 100.0 | Pixel width of the column. |
| SKGridViewColumn | BackColor | string? | null | Hex background color applied to this column's cells. |
| SKGridViewColumn | DisplayIndex | int? | null | Explicit left-to-right ordering position of the column. |
| SKGridViewColumn | IsExpandableColumnForChildRows | bool | false | Marks this column as the one carrying the expand/collapse toggle glyph for parent rows with child rows. |
| SKGridViewColumn | ShowSubTotalOnSort | bool | false | Controls whether subtotal rows are produced for this column when a sort creates groups. |
| SKGridViewColumn | ShowGroupAggregateData | bool | true | Controls whether group aggregate values are displayed in this column on group header/subtotal rows. |
| SKGridViewColumn | ExpandIcon | string | "-" | Glyph shown in the toggle cell when child rows are expanded. |
| SKGridViewColumn | CollapseIcon | string | "+" | Glyph shown in the toggle cell when child rows are collapsed. |
| SKGridViewColumn | IsCheckboxColumn | bool | false | When true, renders a checkbox glyph bound to a boolean property instead of text. |
| SkGridColumnCollection | (no public members) | - | - | Freezable collection of SKGridViewColumn that exists to propagate DataContext inheritance to columns. |
| SkButton | Name | string | - | Required identifier of the button, used to tell buttons apart in click handlers. |
| SkButton | Text | string? | null | Caption text drawn on the button. |
| SkButton | TooltipPath | string? | null | Data property whose value is shown as this button's tooltip, resolved per row at hover time; requires the grid's IsTooltipEnabled. |
| SkButton | TooltipProvider | Func<object, string?>? | null | Callback given the row's data item that returns the button's tooltip text, taking precedence over TooltipPath. |
| SkButton | Width | double? | null | Explicit pixel width of the button slot. |
| SkButton | BackgroundColor | string? | null | Hex fill color of the button. |
| SkButton | BorderColor | string? | null | Hex color of the button's border outline. |
| SkButton | ForegroundColor | string? | null | Hex color of the button's text or icon. |
| SkButton | HoverBackgroundColor | string? | null | Background color used while the pointer is over the button; null means no hover effect. |
| SkButton | HoverForegroundColor | string? | null | Text/icon color used while the pointer is over the button; null means no hover effect. |
| SkButton | ImageSource | ImageSource? | null | WPF image used as the button's icon instead of or alongside text. |
| SkButton | ImageSize | double | 0.0 | Explicit square pixel size for ImageSource, where 0 means auto-fit the largest square that fits the row band and button width. |
| SkButton | MarginRight | double | 0.0 | Pixel gap reserved to the right of the button. |
| SkButton | MarginLeft | double | 0.0 | Pixel gap reserved to the left of the button. |
| SkButton | ContentAlignment | CellContentAlignment | Center | Horizontal alignment of the button within its cell. |
| SkButton | IsVisible | bool | true | Controls whether the button is drawn; when false the renderer also skips advancing the layout cursor so the next button collapses into its slot. |
| SkButton | OnClicked | Action<SkButton, object> | null | Delegate invoked with the button and the row data item when the button is clicked. |
| SkButton | Command | ICommand | null | ICommand alternative to OnClicked, executed on click with CommandParameter receiving the row data item. |
| SkButton | CommandParameter | object | null | Optional parameter passed to Command; when null the row data item is used. |

---

## 4. Styling: Triggers, Setters & Templates

| Type | Property | Type of value | Default | What it does |
|---|---|---|---|---|
| SKTrigger | IsTimerBased | bool | false | Marks the trigger as timer-based so its setters apply only for a limited time after the condition matches. |
| SKTrigger | Duration | double | 0.0 | Lifetime in which the timer-based highlight stays applied; setting it above zero auto-infers IsTimerBased. |
| SKTrigger | Setters | FreezableCollection<SKSetter> | new | Style setters applied when the trigger's condition evaluates true. |
| SKGroupTrigger | Setters | FreezableCollection<SKSetter> | new | Style setters applied to the group row when the group trigger matches. |
| SkGroupDataTrigger | Binding | object | null | Literal or bound value used as the left-hand operand when BindingPath is not set. |
| SkGroupDataTrigger | BindingPath | string? | - | Property path on the aggregated group data whose value is compared. |
| SkGroupDataTrigger | Aggregation | SkAggregation | - | Aggregation applied to the bound group values before comparison. |
| SkGroupDataTrigger | Operator | SKOperation | - | Comparison operator used between the aggregated value and Value. |
| SkGroupDataTrigger | Value | object | null | Right-hand operand the aggregated group value is compared against. |
| SKGroupMultiTrigger | Conditions | FreezableCollection<SKGroupCondition> | new | Set of group conditions that must all match (AND) for the trigger's setters to apply. |
| SKDataTrigger | Binding | object | null | Literal or bound value used as the left-hand operand when BindingPath is not set. |
| SKDataTrigger | BindingPath | string? | - | Property path on the row data item whose value is compared. |
| SKDataTrigger | Operator | SKOperation | - | Comparison operator used between the bound value and Value. |
| SKDataTrigger | Value | object | null | Right-hand operand the bound value is compared against. |
| SKMultiTrigger | Conditions | FreezableCollection<SKCondition> | new | Set of conditions that must all match (AND) for the trigger's setters to apply. |
| SKSetter | Property | SkStyleProperty | - | Which style aspect (Background, Foreground, BorderColor) this setter changes. |
| SKSetter | ValuePath | string? | - | Property path on the row data item read at draw time for a data-driven dynamic color instead of a literal Value. |
| SKSetter | Value | object | null | Literal value (typically a hex color string) applied to the target style property. |
| SKCellTemplate | SkButton | SkButton? | null | Single button rendered in cells using this template. |
| SKCellTemplate | SkButtons | FreezableCollection<SkButton> | new | Multiple buttons rendered left-to-right in cells using this template. |
| SKCellTemplate | DrawButton | Func<object, List<SkButton>> | null | Delegate given the row data item that returns the buttons to draw for that row, for per-row dynamic button sets. |
| SKCellTemplate | CustomDraw | Action<SkCellDrawContext>? | null | Custom drawing delegate invoked after the cell background is painted and the canvas clipped to the cell rect, replacing default text rendering. |
| SKCellTemplate | Setters | FreezableCollection<SKSetter> | new | Unconditional style setters applied to cells using this template. |
| SKCellTemplate | Triggers | FreezableCollection<SKTrigger> | new | Conditional triggers evaluated per cell, with the first match winning; also the XAML content property. |
| SKGroupCellTemplate | Setters | FreezableCollection<SKSetter> | new | Unconditional style setters applied to group header cells. |
| SKGroupCellTemplate | Triggers | FreezableCollection<SKGroupTrigger> | new | Group-level conditional triggers applied to group header cells; also the XAML content property. |
| SKRowTemplate | Setters | FreezableCollection<SKSetter> | new | Unconditional style setters applied to every row. |
| SKRowTemplate | Triggers | FreezableCollection<SKTrigger> | new | Conditional row-level triggers for data-driven row styling; also the XAML content property. |
| SKCondition | Binding | object | null | Literal or bound value used as the left-hand operand when BindingPath is empty. |
| SKCondition | BindingPath | string | "" | Property path on the row data item whose value is compared. |
| SKCondition | Operator | SKOperation | - | Comparison operator used between the bound value and Value. |
| SKCondition | Value | object | null | Right-hand operand the bound value is compared against. |
| SKGroupCondition | Binding | object | null | Literal or bound value used as the left-hand operand when BindingPath is not set. |
| SKGroupCondition | BindingPath | string? | - | Property path on the group's data whose aggregated value is compared. |
| SKGroupCondition | Aggregation | SkAggregation | - | Aggregation applied to the group's values before comparison. |
| SKGroupCondition | Operator | SKOperation | - | Comparison operator used between the aggregated value and Value. |
| SKGroupCondition | Value | object | null | Right-hand operand the aggregated group value is compared against. |
| SkCellDrawContext | Canvas | SKCanvas | - | The live Skia canvas being painted, already clipped to Bounds. |
| SkCellDrawContext | Data | object? | - | The row's bound data item for the cell being drawn. |
| SkCellDrawContext | Bounds | SKRect | - | The cell's rectangle in canvas coordinates, to be used for positioning draw calls. |
| SkCellDrawContext | RowIndex | int | - | Flattened zero-based row index within the rendered list. |
| SkCellDrawContext | ColumnIndex | int | - | Zero-based visible column index after column reordering. |
| SkCellDrawContext | Background | SKPaint | - | The resolved background paint for the cell after the full style cascade and selection overlay, exposed so custom drawing can match it. |
| SkCellDrawContext | Foreground | SKPaint | - | The resolved foreground paint for the cell, for custom text that should match the grid's colors and triggers. |
| SkCellDrawContext | Font | SKFont | - | The grid's current default font (size, family, style). |

---

## 5. Grouping & Aggregation

| Type | Property | Type of value | Default | What it does |
|---|---|---|---|---|
| SKGroupDefinition | ForegroundColor | string | null | Hex text color used for the group header row. |
| SKGroupDefinition | RowBackground | string | null | Hex background color used for the group header row. |
| SKGroupDefinition | GroupCellTemplate | SKGroupCellTemplate? | null | Template supplying setters and group triggers for this grouping's header cells. |
| SKGroupDefinition | GroupBy | string? | - | Property path on the row data item whose value defines group membership. |
| SKGroupDefinition | Target | string? | - | Name of the column in which the group header text is rendered. |
| SKGroupDefinition | HeaderFields | FreezableCollection<SKGroupField> | new | Aggregation fields shown in the group header row. |
| SKGroupDefinition | ToggleSymbol | SKGroupToggleSymbol? | - | Configuration of the expand/collapse glyphs and their styling for this grouping. |
| SKGroupDefinition | ShowSubtotalsWhenCollapsed | bool | false | When true, a collapsed group keeps its subtotal rows (and grand-total rows on CollapseAll) visible under the group header instead of hiding them with the data rows. |
| SKGroupDefinition | PropertyChanged | event | - | Raised when a group definition property changes so the grid can rebuild and repaint. |
| SKGroupField | BindingPath | string | "" | Property path on the row data item whose values are aggregated for this header field. |
| SKGroupField | TargetColumns | string | "" | Column name(s) in which this aggregated value is displayed. |
| SKGroupField | Aggregation | SkAggregation | - | Aggregation function applied to the bound values (Sum, Count, Avg, Min, Max, Distinct). |
| SKGroupField | GroupCellTemplate | SKGroupCellTemplate? | null | Template supplying setters and group triggers for this field's group header cell. |
| SKGroupToggleSymbol | TargetColumns | string? | - | Column name(s) in which the expand/collapse toggle symbol is drawn. |
| SKGroupToggleSymbol | Expand | string? | - | Glyph shown when the group is expanded. |
| SKGroupToggleSymbol | Collapse | string? | - | Glyph shown when the group is collapsed. |
| SKGroupToggleSymbol | ShowGroupDetail | bool? | - | Controls whether the group's detail (data) rows start out visible. |
| SKGroupToggleSymbol | BackgroundColor | string? | null | Hex background color of the toggle symbol cell. |
| SKGroupToggleSymbol | ForegroundColor | string? | null | Hex color of the toggle symbol glyph. |
| GroupModel | IsHeaderSubTotal | bool | false | Marks this flattened row as a grand-total (header-level) subtotal row. |
| GroupModel | IsGroupSubTotal | bool | false | Marks this flattened row as a per-group subtotal row. |
| GroupModel | IsGroupHeader | bool | false | Marks this flattened row as a group header row. |
| GroupModel | GroupName | string? | - | Display name of the group this row belongs to. |
| GroupModel | SubTotalGroupName | string? | - | Name of the group the subtotal row summarizes. |
| GroupModel | BindingPath | string? | - | Property path that produced this group's key value. |
| GroupModel | IsExpanded | bool | true | Whether the group's data rows are currently expanded. |
| GroupModel | Item | object? | - | The underlying row data item wrapped by this model, when the row is a data row. |
| RowModel | IsChildRow | bool | false | Marks this row as a child of a parent (expandable) row. |
| RowModel | Item | object? | - | The underlying row data item. |
| RowModel | HasChild | bool | false | Whether this row has child rows and therefore shows an expand/collapse toggle. |

---

## 6. Row Drag & Drop

| Type | Property | Type of value | Default | What it does |
|---|---|---|---|---|
| SKRowDragData | Format | const string | "SkiaGridViewV2.Rows" | The DataObject format key under which the drag payload is placed and looked up by drop targets. |
| SKRowDragData | Source | SkiaGridViewV2 | - | The grid the drag started from, letting a target ignore its own drags. |
| SKRowDragData | Rows | IReadOnlyList<object> | - | The dragged row data items in view order: the whole selection if the pressed row was selected, otherwise just that row. |
| SKRowDragData | Column | SKGridViewColumn? | - | The column the press landed in, or null if it could not be resolved. |
| SKRowDragData | CellText | string? | - | The formatted text of the single pressed cell, or null when no column resolved. |
| SKRowDragData | Text | string | - | Tab-separated text for the dragged rows including the header row, matching the text placed on the DataObject. |
| SKRowDragStartingEventArgs | Items | IReadOnlyList<object> | - | The rows about to be dragged: the current selection if the pressed row is part of it, otherwise just the pressed row. |
| SKRowDragStartingEventArgs | RowIndex | int | - | Index of the pressed row in view order, group rows included. |
| SKRowDragStartingEventArgs | Column | SKGridViewColumn? | - | The column the press landed in, or null if it could not be resolved. |
| SKRowDragStartingEventArgs | CellText | string? | - | Formatted text of the pressed cell, or null when no column resolved. |
| SKRowDragStartingEventArgs | Data | DataObject? | - | The DataObject being dragged in DragOut mode (null otherwise), pre-populated with SKRowDragData and text so handlers can add or overwrite formats. |
| SKRowDragStartingEventArgs | AllowedEffects | DragDropEffects | Copy or Move | Effects offered to the drop target in DragOut mode, narrowable to stop a target reporting an unsupported move. |
| SKRowDragCompletedEventArgs | Items | IReadOnlyList<object> | - | The rows that were dragged. |
| SKRowDragCompletedEventArgs | Effects | DragDropEffects | - | What the drop target applied, where None means the drag was rejected or abandoned. |
| SKRowDragCompletedEventArgs | Column | SKGridViewColumn? | - | The column the drag started in, or null. |
| SKRowDragCompletedEventArgs | CellText | string? | - | Formatted text of the cell the drag started in, or null. |
| SKRowDroppedEventArgs | Items | IReadOnlyList<object> | - | The rows that were dragged, in view order. |
| SKRowDroppedEventArgs | InsertIndex | int | - | The gap index in view order the rows were dropped into, where 0 is above the first row and RowCount is below the last. |
| SKRowDroppedEventArgs | TargetItem | object? | - | The row the dragged rows land immediately above, or null when dropped past the last row. |
| SKRowDroppedEventArgs | Handled | bool | false | Set to true to tell the grid not to move anything itself; ignored in Notify mode. |

---

## 7. Enumerations

| Enum | Value | Kind | Numeric | Meaning |
|---|---|---|---|---|
| CellContentAlignment | Left | enum value | - | Aligns cell content to the left edge. |
| CellContentAlignment | Center | enum value | - | Centers cell content horizontally. |
| CellContentAlignment | Right | enum value | - | Aligns cell content to the right edge. |
| FilterType | Value | enum value | - | Numeric/value-based filtering for the column. |
| FilterType | Text | enum value | - | Free-text filtering for the column. |
| FilterType | List | enum value | - | Distinct-value checklist filtering for the column. |
| FilterType | None | enum value | - | No filtering offered for the column. |
| SKExportType | All | enum value | - | Export every row in the grid. |
| SKExportType | Selected | enum value | - | Export only the currently selected rows. |
| SKGridDensity | Normal | enum value | - | SKFontSize + 6 row height and SKFontSize + 12 header height (17/23 px at an 11 px font), giving more breathing room per row. |
| SKGridDensity | Compact | enum value | - | Default since v2.14.0: SKFontSize + 3 row height and SKFontSize + 7 header height (14/18 px at an 11 px font), tape density fitting the most rows on screen. |
| SKOperation | GreaterThan | enum value | - | Matches when the left operand compares greater than the value. |
| SKOperation | LessThan | enum value | - | Matches when the left operand compares less than the value. |
| SKOperation | Equals | enum value | - | Matches when the operands compare equal. |
| SKOperation | NotEquals | enum value | - | Matches when the operands do not compare equal. |
| SKOperation | GreaterThanOrEqual | enum value | - | Matches when the left operand compares greater than or equal to the value. |
| SKOperation | LessThanOrEqual | enum value | - | Matches when the left operand compares less than or equal to the value. |
| SKRowDragMode | None | enum value | 0 | Row drag is off; a left-drag over the canvas does hover/tooltip work only. |
| SKRowDragMode | Reorder | enum value | 1 | Drag to reorder within the grid, drawing an insertion indicator and moving rows in the IList ItemsSource on drop unless sorted/grouped or Handled is set. |
| SKRowDragMode | Notify | enum value | 2 | Same in-grid tracking, indicator and events as Reorder, but the grid mutates nothing and the consumer handles RowDropped. |
| SKRowDragMode | DragOut | enum value | 3 | Starts a real WPF drag-and-drop so targets in other windows or applications can receive the row/cell payload, with no indicator and nothing moved inside the grid. |
| SKScrollBarVisibility | Auto | enum value | - | Show the scroll bar only when the content overflows. |
| SKScrollBarVisibility | Hidden | enum value | - | Never show the scroll bar. |
| SKScrollBarVisibility | Visible | enum value | - | Always show the scroll bar. |
| SKSelectionStyle | Fill | enum value | - | Selected row background and text colors change entirely. |
| SKSelectionStyle | Border | enum value | - | Selected row gets a border outline only, preserving original styling. |
| SkAggregation | None | enum value | - | No aggregation applied. |
| SkAggregation | Sum | enum value | - | Sum of the grouped values. |
| SkAggregation | Avg | enum value | - | Arithmetic mean of the grouped values. |
| SkAggregation | Min | enum value | - | Minimum of the grouped values. |
| SkAggregation | Max | enum value | - | Maximum of the grouped values. |
| SkAggregation | Count | enum value | - | Count of items in the group. |
| SkAggregation | Distinct | enum value | - | Count of distinct values in the group. |
| SkExpandChangeReason | UserToggle | enum value | - | The user clicked the expand/collapse toggle glyph on a group header or parent row. |
| SkExpandChangeReason | Api | enum value | - | Caused by an explicit ToggleGroup/ToggleRow call. |
| SkExpandChangeReason | ExpandAll | enum value | - | Caused by ExpandAll, reported as one bulk notification rather than one per group. |
| SkExpandChangeReason | CollapseAll | enum value | - | Caused by CollapseAll, reported as one bulk notification rather than one per group. |
| SkGridViewColumnSort | None | enum value | 0 | Column is unsorted. |
| SkGridViewColumnSort | Ascending | enum value | 1 | Column is sorted ascending. |
| SkGridViewColumnSort | Descending | enum value | 2 | Column is sorted descending. |
| SkStyleProperty | Background | enum value | - | The setter targets the cell/row background color. |
| SkStyleProperty | Foreground | enum value | - | The setter targets the text color. |
| SkStyleProperty | BorderColor | enum value | - | The setter targets the border outline color. |

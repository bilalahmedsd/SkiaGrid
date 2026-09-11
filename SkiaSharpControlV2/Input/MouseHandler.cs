
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Managers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SkiaSharpControlV2.Input
{
    /// <summary>
    /// Handles mouse input (left/right click, double-click, mouse wheel, outside-click) for the grid.
    /// Extracted from SkiaGridViewV2 (Step 10 refactoring).
    /// </summary>
    internal class MouseHandler
    {
        private readonly SkiaGridViewV2 _control;
        private readonly SelectionManager _selectionManager;

        public MouseHandler(SkiaGridViewV2 control, SelectionManager selectionManager)
        {
            _control = control;
            _selectionManager = selectionManager;
        }

        public void HandleLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            using var _metrics = SkiaSharpControlV2.Diagnostics.GridMetrics.Measure(SkiaSharpControlV2.Diagnostics.GridMetrics.InputMouseClick);
            if (_control._collectionView == null || _control.SkiaRenderer == null || _control.SkiaRenderer.GroupItemSource == null)
                return;

            var canvas = _control.SkiaCanvas;
            var point = e.GetPosition(canvas);
            int rowIndex = (int)((point.Y + _control.ScrollOffsetY) / _control.RowHeight);
            double x = point.X + _control.ScrollOffsetX;

            _control._groupingManager.UpdateGroupToggle(x, point.Y + _control.ScrollOffsetY, _control.TotalRows);
            _control._groupingManager.UpdateRowToggle(x, point.Y + _control.ScrollOffsetY, _control.TotalRows);

            var itemsource = _control.GroupSettings == null
                ? _control.SkiaRenderer.Items.Select(i => i.Item)?.Cast<object>().ToList()
                : _control.SkiaRenderer.GroupItemSource.Where(i => i.IsExpanded || i.IsGroupHeader).Select(i => (object)(i.Item ?? i)).ToList(); // Use GroupModel itself for headers (Item is null)
            if (itemsource?.Count() == 0) return;
            List<dynamic> s = itemsource!;

            // Sync DP → SelectionManager
            _selectionManager.SelectedItems = _control.SelectedItems;
            _selectionManager.EnsureInitialized();
            _control.SelectedItems = _selectionManager.SelectedItems!;

            if (rowIndex > s.Count() - 1) return;

            // Cell click detection. The resolved column is kept for the drag payload too, so a
            // dragged-out cell is always the same cell OnCellClicked reported.
            SKGridViewColumn? pressedColumn = null;
            double cx = x;
            foreach (var item in _control.DataListView.Columns.OrderBy(c => c.DisplayIndex).Where(c => c.Visibility == Visibility.Visible))
            {
                cx -= item.Width.Value;
                if (cx <= 0)
                {
                    var col = _control.Columns?.FirstOrDefault(c => c.Header == item?.Header?.ToString() || c.DisplayHeader == item?.Header?.ToString());
                    pressedColumn = col;
                    _control.OnCellClicked?.Invoke(s[rowIndex], col);
                    ExecuteCommand(_control.CellClickedCommand, (s[rowIndex], col));
                    break;
                }
            }

            // Selection
            if (e.LeftButton == MouseButtonState.Pressed && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && _control.CanUserSelectRows)
            {
                _selectionManager.HandleCtrlClick(s, rowIndex);
            }
            else if (e.LeftButton == MouseButtonState.Pressed && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && _control.CanUserSelectRows)
            {
                _selectionManager.HandleShiftClick(s, rowIndex);
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                // A plain press normally collapses the selection to the pressed row. That would
                // destroy a multi-row selection BEFORE the drag set is built, making a multi-row
                // drag impossible — so when row drag is enabled and the press lands on a row that
                // is part of a multi-row selection, the collapse is deferred to mouse-UP. A click
                // without a drag then behaves exactly as before, one event later; a drag keeps the
                // block. Gated on RowDragMode so a grid that has not opted in is untouched.
                bool deferCollapse = _control.CanUserSelectRows
                                     && _control.RowDragMode != SKRowDragMode.None
                                     && _selectionManager.IsPartOfMultiSelection(s[rowIndex]);

                if (_control.CanUserSelectRows && !deferCollapse)
                    _selectionManager.HandleSingleClick(s, rowIndex);
                if (deferCollapse)
                    _control.DeferSelectionCollapse(s, rowIndex);

                _control.OnRowClicked?.Invoke(s[rowIndex]);
                ExecuteCommand(_control.RowClickedCommand, s[rowIndex]);
                TriggerCheckboxClick(point.X + _control.ScrollOffsetX, point.Y + _control.ScrollOffsetY, rowIndex, itemsource);
                TriggerButtonClick(point.X + _control.ScrollOffsetX, point.Y + _control.ScrollOffsetY, rowIndex, itemsource);
            }

            if (e.ClickCount == 2)
            {
                _control.OnRowDoubleClicked?.Invoke(s[rowIndex]);
                ExecuteCommand(_control.RowDoubleClickedCommand, s[rowIndex]);
            }

            // Arm a possible row drag (no-op unless RowDragMode was opted into). Must come AFTER
            // selection so a press on an already-selected row drags the whole selection. Skipped
            // when the press landed on a button or checkbox — those act on mouse-down, and a few
            // px of pointer travel afterwards must not turn into a row drag.
            if (_control.RowDragMode != SKRowDragMode.None
                && !HitTestInteractive(point.X + _control.ScrollOffsetX, point.Y + _control.ScrollOffsetY))
                _control.ArmRowDrag(canvas, point, rowIndex, pressedColumn);

            // SelectionChanged command
            ExecuteCommand(_control.SelectionChangedCommand, _control.SelectedItems);

            // UIA: raise selection events so event-driven tests see a click-selection too
            // (change-gated in the peer — no-op when the click didn't change selection).
            _control.RaiseAutomationSelectionChanged();

            canvas.InvalidateVisual();
            canvas.Focus();
        }

        public void HandleRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_control._collectionView == null || _control.SkiaRenderer == null || _control.SkiaRenderer.GroupItemSource == null)
                return;

            var canvas = _control.SkiaCanvas;
            var point = e.GetPosition(canvas);
            int rowIndex = (int)((point.Y + _control.ScrollOffsetY) / _control.RowHeight);
            double x = point.X + _control.ScrollOffsetX;

            var s = _control.GroupSettings == null
                ? _control.SkiaRenderer.Items.Select(i => i.Item)?.Cast<object>().ToList()
                : _control.SkiaRenderer.GroupItemSource.Where(i => i.IsExpanded || i.IsGroupHeader).Select(i => (object)(i.Item ?? i)).ToList();

            if (rowIndex > s.Count - 1) return;

            // Cell click detection
            double cx = x;
            foreach (var item in _control.DataListView.Columns.OrderBy(c => c.DisplayIndex).Where(c => c.Visibility == Visibility.Visible))
            {
                cx -= item.Width.Value;
                if (cx <= 0)
                {
                    if (_control.Columns != null)
                    {
                        var col = _control.Columns?.FirstOrDefault(c => c.Header == item?.Header?.ToString() || c.DisplayHeader == item?.Header?.ToString());
                        _control.OnCellClicked?.Invoke(s[rowIndex], col);
                        ExecuteCommand(_control.CellClickedCommand, (s[rowIndex], col));
                    }
                    break;
                }
            }

            if (e.RightButton == MouseButtonState.Pressed && _control.CanUserSelectRows)
            {
                _selectionManager.SelectedItems = _control.SelectedItems;
                _selectionManager.EnsureInitialized();
                _control.SelectedItems = _selectionManager.SelectedItems!;
                _selectionManager.HandleRightClick(s, rowIndex);
            }

            _control.OnRowRightClicked?.Invoke(s[rowIndex]);
            ExecuteCommand(_control.RowRightClickedCommand, s[rowIndex]);
            _control.RaiseAutomationSelectionChanged();
            canvas.InvalidateVisual();
            canvas.Focus();
        }

        public void HandleMouseWheel(object sender, MouseWheelEventArgs e)
        {
            using var _metrics = SkiaSharpControlV2.Diagnostics.GridMetrics.Measure(SkiaSharpControlV2.Diagnostics.GridMetrics.InputMouseWheel);
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                _control.HorizontalScrollViewer.Value -= e.Delta / 1;
                _control.UpdateHorizontalScroll();
            }
            else
            {
                _control.VerticalScrollViewer.Value -= e.Delta / 1;
                _control.UpdateVerticalScroll();
            }
        }

        public void HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            _control.SkiaCanvas.Focus();
        }

        public void HandleContainerPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not SkiaSharp.Views.WPF.SKElement)
            {
                if (_selectionManager.HasSelection && !(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && _control.CanUserSelectRows)
                {
                    _selectionManager.ClearAndRefresh();
                    // Clearing selection by clicking outside a row is a selection change too.
                    _control.RaiseAutomationSelectionChanged();
                }
                if (e.ClickCount == 2)
                {
                    _control.OnSkGridDoubleClicked?.Invoke();
                }
            }
        }

        private void TriggerButtonClick(double x, double y, int rowIndex, List<dynamic> itemsource)
        {
            var values = _control.ButtonDetails.Where(v => (x >= v.Value.x && x <= v.Value.x + v.Value.width) && (y >= v.Value.y && y <= v.Value.y + v.Value.height)).LastOrDefault();
            if (values.Value.btn != null)
            {
                var btn = values.Value.btn;
                var rowData = itemsource[rowIndex];

                // Invoke Action delegate (backward compat)
                btn.OnClicked?.Invoke(btn, rowData);

                // Invoke ICommand (MVVM)
                var param = btn.CommandParameter ?? rowData;
                if (btn.Command != null && btn.Command.CanExecute(param))
                    btn.Command.Execute(param);
            }
        }

        private void TriggerCheckboxClick(double x, double y, int rowIndex, List<dynamic> itemsource)
        {
            var checkboxDetails = _control.SkiaRenderer?.RenderContext?.CheckboxDetails;
            if (checkboxDetails == null) return;

            // Use LastOrDefault to match TriggerButtonClick semantics — if any stale
            // hit-test entries linger from a prior frame, the newest one wins.
            var hit = checkboxDetails.LastOrDefault(v =>
                x >= v.Value.x && x <= v.Value.x + v.Value.w &&
                y >= v.Value.y && y <= v.Value.y + v.Value.h);

            if (hit.Key.item == null) return;
            if (rowIndex < 0 || rowIndex >= itemsource.Count) return;

            // Take bindingPath from the hit (correct — describes WHICH column was clicked),
            // but take the row item from itemsource[rowIndex] — NEVER from the dictionary
            // key. The dictionary key can reference a row that has been removed from the
            // source collection, causing the toggle + CheckboxToggledCommand to fire
            // against a deleted item. (Symmetry with TriggerButtonClick above.)
            var bindingPath = hit.Key.bindingPath;
            object currentItem = itemsource[rowIndex];

            var reflectionHelper = new Helpers.ReflectionHelper();
            var currentVal = reflectionHelper.GetPropValue(currentItem, bindingPath);
            if (currentVal is bool boolVal)
            {
                reflectionHelper.SetPropValue(currentItem, bindingPath, !boolVal);
                ExecuteCommand(_control.CheckboxToggledCommand, (currentItem, bindingPath, !boolVal));
            }
        }

        /// <summary>
        /// True when a button or a checkbox occupies the given content-space point — i.e. the press
        /// was on an in-cell control, not on the row itself.
        /// </summary>
        private bool HitTestInteractive(double x, double y)
        {
            foreach (var v in _control.ButtonDetails)
                if (x >= v.Value.x && x <= v.Value.x + v.Value.width &&
                    y >= v.Value.y && y <= v.Value.y + v.Value.height) return true;

            var checkboxes = _control.SkiaRenderer?.RenderContext?.CheckboxDetails;
            if (checkboxes != null)
                foreach (var v in checkboxes)
                    if (x >= v.Value.x && x <= v.Value.x + v.Value.w &&
                        y >= v.Value.y && y <= v.Value.y + v.Value.h) return true;

            return false;
        }

        private static void ExecuteCommand(System.Windows.Input.ICommand? command, object? parameter)
        {
            if (command != null && command.CanExecute(parameter))
                command.Execute(parameter);
        }
    }
}

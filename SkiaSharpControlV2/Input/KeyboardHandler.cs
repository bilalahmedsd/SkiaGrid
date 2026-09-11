
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Managers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace SkiaSharpControlV2.Input
{
    /// <summary>
    /// Handles keyboard input (arrow keys, Shift+arrow, Ctrl+A, Ctrl+C) for the grid.
    /// Extracted from SkiaGridViewV2.SkiaCanvas_KeyDown (Step 10 refactoring).
    /// </summary>
    internal class KeyboardHandler
    {
        private readonly Func<List<object>> _getItems;
        private readonly Func<ObservableCollection<object>?> _getSelectedItems;
        private readonly SelectionManager _selectionManager;
        private readonly Func<float> _getScrollOffsetY;
        private readonly Func<float> _getRowHeight;
        private readonly Func<int> _getTotalRows;
        private readonly Func<double?> _getViewportHeight;
        private readonly Action<double> _scrollToVerticalOffset;
        private readonly Action _selectAllRows;
        private readonly Func<string> _exportSelected;
        private readonly Action _invalidateVisual;
        private readonly Func<UIElement> _getFocusElement;
        private readonly Func<Action<Key>?> _getOnPreviewKeyDown;
        private readonly Action? _notifySelectionChanged;

        public KeyboardHandler(
            Func<List<object>> getItems,
            Func<ObservableCollection<object>?> getSelectedItems,
            SelectionManager selectionManager,
            Func<float> getScrollOffsetY,
            Func<float> getRowHeight,
            Func<int> getTotalRows,
            Func<double?> getViewportHeight,
            Action<double> scrollToVerticalOffset,
            Action selectAllRows,
            Func<string> exportSelected,
            Action invalidateVisual,
            Func<UIElement> getFocusElement,
            Func<Action<Key>?> getOnPreviewKeyDown,
            Action? notifySelectionChanged = null)
        {
            _getItems = getItems;
            _getSelectedItems = getSelectedItems;
            _selectionManager = selectionManager;
            _getScrollOffsetY = getScrollOffsetY;
            _getRowHeight = getRowHeight;
            _getTotalRows = getTotalRows;
            _getViewportHeight = getViewportHeight;
            _scrollToVerticalOffset = scrollToVerticalOffset;
            _selectAllRows = selectAllRows;
            _exportSelected = exportSelected;
            _invalidateVisual = invalidateVisual;
            _getFocusElement = getFocusElement;
            _getOnPreviewKeyDown = getOnPreviewKeyDown;
            _notifySelectionChanged = notifySelectionChanged;
        }

        public void HandleKeyDown(object sender, KeyEventArgs e)
        {
            using var _metrics = SkiaSharpControlV2.Diagnostics.GridMetrics.Measure(SkiaSharpControlV2.Diagnostics.GridMetrics.InputKeyDown);
            var items = _getItems();
            if (items.Count == 0) return;

            var selectedItems = _getSelectedItems();
            if (selectedItems == null || selectedItems.Count == 0) return;

            bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (selectedItems.Count == 1)
                _selectionManager.SelectionAnchorIndex = _selectionManager.LastSelectedRowIndex;

            int currentIndex = items.IndexOf(selectedItems.Last());

            // Guard: if selected item is no longer in visible items (e.g., group collapsed), reset selection
            if (currentIndex < 0)
            {
                _selectionManager.SelectionAnchorIndex = null;
                return;
            }

            if (!isShift)
                _selectionManager.SelectionAnchorIndex = null;

            float scrollY = _getScrollOffsetY();
            float rowHeight = _getRowHeight();
            int totalRows = _getTotalRows();
            int firstVisibleRow = Math.Max(0, (int)(scrollY / rowHeight));
            int visibleRowCount = Math.Min((int?)(_getViewportHeight() / rowHeight - 3) ?? 0, totalRows - firstVisibleRow);
            var focusEl = _getFocusElement();

            if (isShift && e.Key == Key.Up)
                HandleShiftUp(items, selectedItems, currentIndex, firstVisibleRow, scrollY, rowHeight, focusEl, e);
            else if (isShift && e.Key == Key.Down)
                HandleShiftDown(items, selectedItems, currentIndex, firstVisibleRow, visibleRowCount, scrollY, rowHeight, focusEl, e);
            else if (e.Key == Key.Up)
                HandleUp(items, selectedItems, currentIndex, firstVisibleRow, scrollY, rowHeight, focusEl, e);
            else if (e.Key == Key.Down)
                HandleDown(items, selectedItems, currentIndex, firstVisibleRow, visibleRowCount, scrollY, rowHeight, focusEl, e);
            else if (isCtrl && e.Key == Key.A)
                _selectAllRows();
            else if (isCtrl && e.Key == Key.C)
                Clipboard.SetText(_exportSelected());

            _getOnPreviewKeyDown()?.Invoke(e.Key);

            // Arrow / Shift+arrow / Ctrl+A all mutate selection — raise UIA selection
            // events (change-gated in the peer, so Ctrl+C and no-op keys raise nothing).
            _notifySelectionChanged?.Invoke();
        }

        private void HandleShiftUp(List<object> items, ObservableCollection<object> sel, int cur, int firstVisible, float scrollY, float rh, UIElement focus, KeyEventArgs e)
        {
            if (cur == 0) { e.Handled = true; Keyboard.Focus(focus); return; }
            if (_selectionManager.SelectionAnchorIndex == null)
                _selectionManager.SelectionAnchorIndex = cur;
            if (cur > 0)
            {
                int newIdx = cur - 1;
                if (newIdx < _selectionManager.SelectionAnchorIndex)
                    sel.Add(items[newIdx]);
                else
                    sel.Remove(items[cur]);
                e.Handled = true;
                Keyboard.Focus(focus);
                if (cur <= firstVisible)
                    _scrollToVerticalOffset(scrollY - rh);
                _invalidateVisual();
            }
        }

        private void HandleShiftDown(List<object> items, ObservableCollection<object> sel, int cur, int firstVisible, int visibleCount, float scrollY, float rh, UIElement focus, KeyEventArgs e)
        {
            if (cur == items.Count - 1) { e.Handled = true; Keyboard.Focus(focus); return; }
            if (_selectionManager.SelectionAnchorIndex == null)
                _selectionManager.SelectionAnchorIndex = cur;
            if (cur < items.Count - 1)
            {
                int newIdx = cur + 1;
                if (newIdx > _selectionManager.SelectionAnchorIndex)
                    sel.Add(items[newIdx]);
                else
                    sel.Remove(items[cur]);
                e.Handled = true;
                Keyboard.Focus(focus);
                if (cur >= firstVisible + visibleCount)
                    _scrollToVerticalOffset(scrollY + rh);
                _invalidateVisual();
            }
        }

        private void HandleUp(List<object> items, ObservableCollection<object> sel, int cur, int firstVisible, float scrollY, float rh, UIElement focus, KeyEventArgs e)
        {
            if (cur == 0) { e.Handled = true; Keyboard.Focus(focus); return; }
            if (cur > 0)
            {
                sel.Clear();
                sel.Add(items[cur - 1]);
                _selectionManager.SelectionAnchorIndex = cur - 1;
                _selectionManager.LastSelectedRowIndex = cur - 1;
                e.Handled = true;
                Keyboard.Focus(focus);
                if (cur <= firstVisible)
                    _scrollToVerticalOffset(scrollY - rh);
                _invalidateVisual();
            }
        }

        private void HandleDown(List<object> items, ObservableCollection<object> sel, int cur, int firstVisible, int visibleCount, float scrollY, float rh, UIElement focus, KeyEventArgs e)
        {
            if (cur == items.Count - 1) { e.Handled = true; Keyboard.Focus(focus); return; }
            if (cur < items.Count - 1)
            {
                sel.Clear();
                sel.Add(items[cur + 1]);
                _selectionManager.SelectionAnchorIndex = cur + 1;
                _selectionManager.LastSelectedRowIndex = cur + 1;
                e.Handled = true;
                Keyboard.Focus(focus);
                if (cur >= firstVisible + visibleCount)
                    _scrollToVerticalOffset(scrollY + rh);
                _invalidateVisual();
            }
        }
    }
}

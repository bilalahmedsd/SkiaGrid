using SkiaSharpControlV2.Diagnostics;
using System.Collections;
using System.Collections.ObjectModel;

namespace SkiaSharpControlV2.Managers
{
    /// <summary>
    /// Manages selection state: SelectedItems, lastSelectedRowIndex, selectionAnchorIndex.
    /// Provides methods for single click, Ctrl+click (toggle), Shift+click (range),
    /// arrow key navigation, select all, and clear.
    /// Extracted from SkiaGridViewV2 (Step 9 refactoring).
    /// </summary>
    internal class SelectionManager
    {
        private ObservableCollection<object>? _selectedItems;
        private int _lastSelectedRowIndex;
        private int? _selectionAnchorIndex;
        private readonly Action _invalidateVisual;
        private readonly Action<ObservableCollection<object>> _updateRendererSelectedItems;

        public SelectionManager(
            Action invalidateVisual,
            Action<ObservableCollection<object>> updateRendererSelectedItems)
        {
            _invalidateVisual = invalidateVisual;
            _updateRendererSelectedItems = updateRendererSelectedItems;
        }

        // ── State Access ────────────────────────────────────────────────

        public ObservableCollection<object>? SelectedItems
        {
            get => _selectedItems;
            set
            {
                _selectedItems = value;
                if (value != null)
                    _updateRendererSelectedItems(value);
            }
        }

        public int LastSelectedRowIndex
        {
            get => _lastSelectedRowIndex;
            set => _lastSelectedRowIndex = value;
        }

        public int? SelectionAnchorIndex
        {
            get => _selectionAnchorIndex;
            set => _selectionAnchorIndex = value;
        }

        // ── Ensure SelectedItems initialized ────────────────────────────

        public void EnsureInitialized()
        {
            if (_selectedItems == null)
            {
                _selectedItems = new ObservableCollection<object>();
                _updateRendererSelectedItems(_selectedItems);
            }
        }

        // ── Click Handlers ──────────────────────────────────────────────

        /// <summary>Single click — clear previous, select one item.</summary>
        public void HandleSingleClick(List<dynamic> items, int rowIndex)
        {
            using var _metrics = SkiaSharpControlV2.Diagnostics.GridMetrics.Measure(SkiaSharpControlV2.Diagnostics.GridMetrics.SelectionUpdate);
            EnsureInitialized();
            if (rowIndex >= items.Count) return;

            _selectedItems!.Clear();
            _selectedItems.Add(items[rowIndex]);
            _lastSelectedRowIndex = rowIndex;
            // Per-click logging removed — causes debug lag
        }

        /// <summary>Ctrl+click — toggle item in selection.</summary>
        public void HandleCtrlClick(List<dynamic> items, int rowIndex)
        {
            EnsureInitialized();
            if (rowIndex >= items.Count) return;

            if (_selectedItems!.Any(x => x.Equals(items[rowIndex])))
            {
                _selectedItems.Remove(items[rowIndex]);
            }
            else
            {
                _selectedItems.Add(items[rowIndex]);
                _lastSelectedRowIndex = rowIndex;
            }
        }

        /// <summary>Shift+click — range selection from anchor to clicked row.</summary>
        public void HandleShiftClick(List<dynamic> items, int rowIndex)
        {
            EnsureInitialized();
            if (_selectedItems!.Count == 0) return;
            if (rowIndex >= items.Count) return;

            var anchorRow = _lastSelectedRowIndex;
            var targetIndex = items.IndexOf(items[rowIndex]);

            _selectedItems.Clear();
            int start = Math.Min(anchorRow, targetIndex);
            int end = Math.Max(anchorRow, targetIndex);
            for (int i = start; i <= end; i++)
            {
                if (i < items.Count)
                    _selectedItems.Add(items[i]);
            }
        }

        /// <summary>
        /// UIA AddToSelection — add the row to the selection WITHOUT toggling (idempotent).
        /// Mirrors the "add" branch of <see cref="HandleCtrlClick"/> but never removes.
        /// Returns true when the item was actually added.
        /// </summary>
        public bool AddToSelection(List<dynamic> items, int rowIndex)
        {
            EnsureInitialized();
            if (rowIndex < 0 || rowIndex >= items.Count) return false;

            var item = items[rowIndex];
            if (_selectedItems!.Any(x => x.Equals(item))) return false;
            _selectedItems.Add(item);
            _lastSelectedRowIndex = rowIndex;
            return true;
        }

        /// <summary>
        /// UIA RemoveFromSelection — remove the row from the selection if present.
        /// Returns true when an item was actually removed.
        /// </summary>
        public bool RemoveFromSelection(List<dynamic> items, int rowIndex)
        {
            EnsureInitialized();
            if (rowIndex < 0 || rowIndex >= items.Count) return false;

            var item = items[rowIndex];
            for (int i = _selectedItems!.Count - 1; i >= 0; i--)
            {
                if (_selectedItems[i].Equals(item))
                {
                    _selectedItems.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Right-click — select if not already selected.</summary>
        public void HandleRightClick(List<dynamic> items, int rowIndex)
        {
            EnsureInitialized();
            if (rowIndex >= items.Count) return;

            if (!_selectedItems!.Contains(items[rowIndex]))
            {
                _selectedItems.Clear();
                _selectedItems.Add(items[rowIndex]);
                _lastSelectedRowIndex = rowIndex;
            }
        }

        // ── Select All ──────────────────────────────────────────────────

        public void SelectAll(List<object> items)
        {
            EnsureInitialized();
            _selectedItems!.Clear();
            foreach (var item in items)
                _selectedItems.Add(item);

            _lastSelectedRowIndex = items.Count - 1;
            _selectionAnchorIndex = 0;
            _invalidateVisual();
        }

        // ── Clear ───────────────────────────────────────────────────────

        public void Clear()
        {
            _selectedItems?.Clear();
            _lastSelectedRowIndex = 0;
            _selectionAnchorIndex = null;
        }

        /// <summary>Clear and invalidate visual.</summary>
        public void ClearAndRefresh()
        {
            Clear();
            _invalidateVisual();
        }

        // ── Query ───────────────────────────────────────────────────────

        public bool IsSelected(object? item)
        {
            if (_selectedItems == null || item == null) return false;
            foreach (var sel in _selectedItems)
            {
                if (item.Equals(sel)) return true;
            }
            return false;
        }

        public bool HasSelection => _selectedItems != null && _selectedItems.Count > 0;

        /// <summary>
        /// True when <paramref name="item"/> is selected AND it is not the only selected row.
        /// Row drag uses this to decide whether a plain press should keep the selection alive
        /// (so the whole block can be dragged) instead of collapsing it to the pressed row.
        /// </summary>
        public bool IsPartOfMultiSelection(object? item)
            => item != null && _selectedItems != null && _selectedItems.Count > 1 && IsSelected(item);
    }
}

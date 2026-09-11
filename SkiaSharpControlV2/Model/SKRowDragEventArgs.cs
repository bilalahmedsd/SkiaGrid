using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace SkiaSharpControlV2
{
    /// <summary>
    /// The payload a <see cref="SKRowDragMode.DragOut"/> drag puts on the clipboard-style
    /// <see cref="DataObject"/>, under the <see cref="Format"/> key. In-process drop targets
    /// (another window in the same app) get the real row objects through this; out-of-process
    /// targets get the tab-separated text that travels alongside it.
    /// </summary>
    public class SKRowDragData
    {
        /// <summary>
        /// The <see cref="DataObject"/> format key. Check for it in your drop target:
        /// <c>e.Data.GetDataPresent(SKRowDragData.Format)</c>, then
        /// <c>(SKRowDragData)e.Data.GetData(SKRowDragData.Format)</c>.
        /// </summary>
        public const string Format = "SkiaGridViewV2.Rows";

        public SKRowDragData(SkiaGridViewV2 source, IReadOnlyList<object> rows,
                             SKGridViewColumn? column, string? cellText, string text)
        {
            Source = source;
            Rows = rows;
            Column = column;
            CellText = cellText;
            Text = text;
        }

        /// <summary>The grid the drag started from — lets a target ignore its own drags.</summary>
        public SkiaGridViewV2 Source { get; }

        /// <summary>
        /// The dragged row data items, in view order. The whole selection when the pressed row was
        /// part of a multi-row selection, otherwise the single pressed row.
        /// </summary>
        public IReadOnlyList<object> Rows { get; }

        /// <summary>The column the press landed in, or null if it could not be resolved.</summary>
        public SKGridViewColumn? Column { get; }

        /// <summary>
        /// The formatted text of the single cell that was pressed (row + <see cref="Column"/>) —
        /// what the user actually saw there. Null when no column resolved. This is the field to read
        /// for "drag this symbol", as opposed to "drag these rows".
        /// </summary>
        public string? CellText { get; }

        /// <summary>Tab-separated text for the dragged rows, header row included — the same thing that goes on the DataObject as text.</summary>
        public string Text { get; }
    }

    /// <summary>
    /// Raised once, when a left-drag on a row passes the system drag threshold and BEFORE the grid
    /// does anything else. Set <see cref="CancelEventArgs.Cancel"/> to true to refuse the drag.
    /// <para>
    /// In <see cref="SKRowDragMode.DragOut"/>, <see cref="Data"/> and <see cref="AllowedEffects"/>
    /// are the drag about to be started — add your own formats to <see cref="Data"/> (or replace the
    /// text) and the target will see them. In the in-grid modes both are null / unused.
    /// </para>
    /// <para>
    /// Mouse capture is not held while this runs, so a handler in a non-DragOut mode may start its
    /// own <c>DragDrop.DoDragDrop</c> — such a handler should also set <c>Cancel = true</c>.
    /// </para>
    /// </summary>
    public class SKRowDragStartingEventArgs : CancelEventArgs
    {
        public SKRowDragStartingEventArgs(IReadOnlyList<object> items, int rowIndex,
                                          SKGridViewColumn? column = null, string? cellText = null,
                                          DataObject? data = null)
        {
            Items = items;
            RowIndex = rowIndex;
            Column = column;
            CellText = cellText;
            Data = data;
        }

        /// <summary>
        /// The rows being dragged. This is the current selection when the pressed row is part of a
        /// multi-row selection, otherwise just the pressed row.
        /// </summary>
        public IReadOnlyList<object> Items { get; }

        /// <summary>Index of the pressed row in view order (what the user sees, group rows included).</summary>
        public int RowIndex { get; }

        /// <summary>The column the press landed in, or null if it could not be resolved.</summary>
        public SKGridViewColumn? Column { get; }

        /// <summary>Formatted text of the pressed cell, or null when no column resolved.</summary>
        public string? CellText { get; }

        /// <summary>
        /// The data being dragged, in <see cref="SKRowDragMode.DragOut"/> only (null otherwise).
        /// Already populated with <see cref="SKRowDragData"/> and text — add or overwrite formats
        /// here to control exactly what a drop target sees.
        /// </summary>
        public DataObject? Data { get; }

        /// <summary>
        /// Effects offered to the drop target, in <see cref="SKRowDragMode.DragOut"/> only.
        /// Defaults to <c>Copy | Move</c>; narrow it to e.g. <c>Copy</c> to stop a target reporting
        /// a move the grid's data cannot honour.
        /// </summary>
        public DragDropEffects AllowedEffects { get; set; } = DragDropEffects.Copy | DragDropEffects.Move;
    }

    /// <summary>
    /// Raised after a <see cref="SKRowDragMode.DragOut"/> operation finishes, with whatever the drop
    /// target decided. <see cref="DragDropEffects.None"/> means nothing accepted it.
    /// <para>
    /// The grid never removes rows itself, not even on <see cref="DragDropEffects.Move"/> — only the
    /// consumer knows whether the source collection should give the row up. Handle this event to do
    /// that.
    /// </para>
    /// </summary>
    public class SKRowDragCompletedEventArgs : EventArgs
    {
        public SKRowDragCompletedEventArgs(IReadOnlyList<object> items, DragDropEffects effects,
                                           SKGridViewColumn? column, string? cellText)
        {
            Items = items;
            Effects = effects;
            Column = column;
            CellText = cellText;
        }

        /// <summary>The rows that were dragged.</summary>
        public IReadOnlyList<object> Items { get; }

        /// <summary>What the drop target applied. <c>None</c> = the drag was rejected or abandoned.</summary>
        public DragDropEffects Effects { get; }

        /// <summary>The column the drag started in, or null.</summary>
        public SKGridViewColumn? Column { get; }

        /// <summary>Formatted text of the cell the drag started in, or null.</summary>
        public string? CellText { get; }
    }

    /// <summary>
    /// Raised when an in-grid drag (<see cref="SKRowDragMode.Reorder"/> /
    /// <see cref="SKRowDragMode.Notify"/>) is released over the grid. In <c>Reorder</c> the grid
    /// performs the source move AFTER this event unless <see cref="Handled"/> was set.
    /// </summary>
    public class SKRowDroppedEventArgs : EventArgs
    {
        public SKRowDroppedEventArgs(IReadOnlyList<object> items, int insertIndex, object? targetItem)
        {
            Items = items;
            InsertIndex = insertIndex;
            TargetItem = targetItem;
        }

        /// <summary>The rows that were dragged, in view order.</summary>
        public IReadOnlyList<object> Items { get; }

        /// <summary>
        /// The gap the rows were dropped into, in view order: 0 = above the first row,
        /// <c>RowCount</c> = below the last row. This is a GAP index, not a row index.
        /// </summary>
        public int InsertIndex { get; }

        /// <summary>
        /// The row the dragged rows land immediately ABOVE, or null when dropped past the last row.
        /// This is the stable way to express the drop — it survives filtering, where a view index
        /// does not map to a source index.
        /// </summary>
        public object? TargetItem { get; }

        /// <summary>
        /// Set to true to tell the grid NOT to move anything itself. Ignored in
        /// <see cref="SKRowDragMode.Notify"/> (which never moves anything anyway).
        /// </summary>
        public bool Handled { get; set; }
    }
}

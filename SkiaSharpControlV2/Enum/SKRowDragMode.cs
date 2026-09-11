namespace SkiaSharpControlV2
{
    /// <summary>
    /// How the grid reacts to dragging a row with the left mouse button.
    /// Default is <see cref="None"/> — row drag is entirely off unless a consumer opts in,
    /// so a grid that never sets <c>RowDragMode</c> behaves exactly as it did before 2.18.0.
    /// </summary>
    public enum SKRowDragMode
    {
        /// <summary>
        /// No row drag. A left-drag over the canvas does hover / tooltip work only.
        /// </summary>
        None = 0,

        /// <summary>
        /// Drag to reorder WITHIN the grid. Draws an insertion indicator and, on drop, moves the
        /// dragged row(s) inside the collection assigned to <c>ItemsSource</c> (which must implement
        /// <see cref="System.Collections.IList"/>).
        /// The built-in move is SKIPPED while a sort or a grouping is active — view order would not
        /// survive it — but <c>RowDropped</c> still fires so the consumer can decide.
        /// Set <c>SKRowDroppedEventArgs.Handled = true</c> to suppress the built-in move.
        /// </summary>
        Reorder = 1,

        /// <summary>
        /// Drag within the grid and report only. Identical tracking, indicator and events as
        /// <see cref="Reorder"/>, but the grid mutates nothing — handle <c>RowDropped</c> yourself.
        /// </summary>
        Notify = 2,

        /// <summary>
        /// Drag the row (or the pressed cell) OUT of the grid: the grid starts a real WPF
        /// drag-and-drop operation, so any drop target in this app — another window, another
        /// control — or another application can receive it. This is the mode to use for
        /// "drag a symbol onto an order ticket".
        /// <para>
        /// No insertion indicator is drawn and nothing inside the grid moves; WPF owns the cursor
        /// and the drop feedback while the operation runs. The payload carries
        /// <see cref="SKRowDragData"/> under <see cref="SKRowDragData.Format"/> for in-process
        /// targets plus tab-separated text for everything else — see <c>RowDragStarting</c> to add
        /// or replace formats, and <c>RowDragCompleted</c> for the effect the target applied.
        /// </para>
        /// </summary>
        DragOut = 3,
    }
}

namespace SkiaSharpControlV2
{
    /// <summary>
    /// Payload for <see cref="SkiaGridViewV2.ExpandChanged"/> and <c>ExpandChangedCommand</c>.
    /// Raised when a group header (grouped mode) or a parent row (tree / <c>ChildProperty</c> mode)
    /// changes expand state — user glyph click, <see cref="SkiaGridViewV2.ToggleGroup"/> /
    /// <see cref="SkiaGridViewV2.ToggleRow"/>, or a bulk expand/collapse.
    /// <para>
    /// Intended for per-row market-data subscribe/unsubscribe: <see cref="AffectedItems"/> is exactly
    /// the set of data rows that became visible (<see cref="IsExpanded"/> = <c>true</c>) or hidden
    /// (<c>false</c>) as a result of the change.
    /// </para>
    /// </summary>
    public sealed class SkExpandChangedEventArgs : EventArgs
    {
        internal SkExpandChangedEventArgs(
            bool isExpanded,
            SkExpandChangeReason reason,
            string? groupName,
            object? parentItem,
            IReadOnlyList<object> affectedItems)
        {
            IsExpanded = isExpanded;
            Reason = reason;
            GroupName = groupName;
            ParentItem = parentItem;
            AffectedItems = affectedItems;
        }

        /// <summary>New state: <c>true</c> = expanded (rows now visible), <c>false</c> = collapsed (rows now hidden).</summary>
        public bool IsExpanded { get; }

        /// <summary>What triggered the change.</summary>
        public SkExpandChangeReason Reason { get; }

        /// <summary>
        /// Group whose state changed (grouped mode). <c>null</c> in tree mode and for bulk
        /// notifications (<see cref="IsBulk"/>), where every group changed at once.
        /// </summary>
        public string? GroupName { get; }

        /// <summary>
        /// Parent data item whose children were shown/hidden (tree / <c>ChildProperty</c> mode).
        /// <c>null</c> in grouped mode and for bulk notifications.
        /// </summary>
        public object? ParentItem { get; }

        /// <summary>
        /// Data rows that became visible or hidden. Grouped mode: the group's data rows (group
        /// header / subtotal rows are never included). Tree mode: the parent's child rows.
        /// Bulk notifications: every data row / every child row in the view. Never <c>null</c>.
        /// </summary>
        public IReadOnlyList<object> AffectedItems { get; }

        /// <summary>
        /// <c>true</c> for <see cref="SkExpandChangeReason.ExpandAll"/> /
        /// <see cref="SkExpandChangeReason.CollapseAll"/>: ONE notification covering every group /
        /// parent row, with <see cref="GroupName"/> and <see cref="ParentItem"/> both <c>null</c>.
        /// </summary>
        public bool IsBulk => Reason == SkExpandChangeReason.ExpandAll || Reason == SkExpandChangeReason.CollapseAll;
    }
}

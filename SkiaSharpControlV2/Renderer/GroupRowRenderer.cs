using SkiaSharp;

using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;
using System.Collections;

namespace SkiaSharpControlV2.Renderer
{
    /// <summary>
    /// Renders group rows (HeaderSubTotal, GroupHeader, GroupSubTotal, normal data within groups).
    /// Extracted from SkiaRenderer.Draw() (Step 6 refactoring).
    /// Receives SkiaRenderer reference to call back into existing delegates (Draw cell, GetSetterValues, etc.)
    /// This back-reference will be removed in Step 14 when CurrentContext is eliminated.
    /// </summary>
    internal class GroupRowRenderer
    {
        private readonly SkiaRenderer _renderer;

        public GroupRowRenderer(SkiaRenderer renderer)
        {
            _renderer = renderer;
        }

        /// <summary>
        /// Render a single group cell at the given row/column position.
        /// Called from SkiaRenderer.Draw() main loop when GroupItems != null.
        /// </summary>
        public void RenderGroupCell(
            SKCanvas canvas,
            int row, int colIndex,
            float columnWidth, float currentX, float currentY, float rowHeight,
            List<GroupModel> groupItems,
            List<GroupModel> groupItemSource,
            List<SKGridViewColumn> visibleColumns,
            SKGroupDefinition? group,
            SKPaint rowColor,
            ReflectionHelper reflectionHelper,
            SKPaint fontColor, SKPaint? groupFontColor, SKPaint? groupRowBgColor,
            SKFont symbolFont, SKFont unicodeFont, float fontSize,
            IEnumerable? selectedItems,
            SKRowTemplate? rowTemplate, SKCellTemplate? cellTemplate,
            Dictionary<string, (bool IsExpended, float x, float y, float height, float width)> groupToggleDetails)
        {
            var col = visibleColumns[colIndex];
            var gi = groupItems[row];
            // For group headers/subtotals, Item is null — check GroupModel itself as the selectable object
            bool isSelected = _renderer._cellRenderer.HighlightSelected(gi.Item ?? (object)gi, selectedItems);

            if (gi.IsHeaderSubTotal)
            {
                RenderHeaderSubTotal(canvas, row, colIndex, columnWidth, currentX, currentY, rowHeight,
                    gi, groupItemSource, col, group, rowColor, reflectionHelper,
                    fontColor, groupFontColor, groupRowBgColor, symbolFont, isSelected);
            }
            else if (gi.IsGroupHeader)
            {
                RenderGroupHeader(canvas, row, colIndex, columnWidth, currentX, currentY, rowHeight,
                    gi, groupItemSource, visibleColumns, col, group, rowColor, reflectionHelper,
                    fontColor, groupFontColor, groupRowBgColor, symbolFont, unicodeFont, fontSize,
                    isSelected, groupToggleDetails);
            }
            else if (gi.IsGroupSubTotal)
            {
                RenderGroupSubTotal(canvas, row, colIndex, columnWidth, currentX, currentY, rowHeight,
                    gi, groupItemSource, col, group, rowColor, reflectionHelper,
                    fontColor, groupFontColor, groupRowBgColor, symbolFont, isSelected);
            }
            else
            {
                RenderGroupDataRow(canvas, row, colIndex, columnWidth, currentX, currentY, rowHeight,
                    gi, col, rowColor, reflectionHelper, fontColor, symbolFont, isSelected,
                    rowTemplate, cellTemplate, visibleColumns);
            }
        }

        // ── HeaderSubTotal ──────────────────────────────────────────────

        private void RenderHeaderSubTotal(
            SKCanvas canvas, int row, int colIndex, float w, float x, float y, float rh,
            GroupModel gi, List<GroupModel> source, SKGridViewColumn col,
            SKGroupDefinition? group, SKPaint rowColor, ReflectionHelper rh2,
            SKPaint fontColor, SKPaint? groupFontColor, SKPaint? groupRowBgColor,
            SKFont symbolFont, bool isSelected)
        {
            string value = Convert.ToString(gi.GroupName!);

            if (group?.Target != null && group.Target == col.Name)
            {
                var (bg, fg, _) = ResolveGroupStyle(gi, source, group, rh2, rowColor, fontColor, groupFontColor, groupRowBgColor);
                _renderer.DrawCellPublic(canvas, colIndex, row, "Total " + value, fg, symbolFont, bg, null, w, x, y, col.ContentAlignment, rh, isSelected);
            }
            else if (MatchesHeaderField(group, col, out var header))
            {
                var valuesForTotal = FilterDataRows(source).Where(r => rh2.ReadCurrentItemWithTypes(r.Item, gi.BindingPath).Value == gi.GroupName);
                var agg = _renderer._setterResolver.CalculateGroupAggregation(valuesForTotal, rh2, header!.BindingPath, header.Aggregation);
                var formatted = Helper.ApplyFormat(typeof(double), agg?.ToString(), col.Format, col.ShowBracketOnNegative, col.FormatWithAcronym);
                var (bg, fg, _) = ResolveHeaderFieldStyle(gi, valuesForTotal, header, group, rh2, rowColor, fontColor, groupFontColor, groupRowBgColor);
                _renderer.DrawCellPublic(canvas, colIndex, row, formatted, fg, symbolFont, bg, null, w, x, y, col.ContentAlignment, rh, isSelected);
            }
            else
            {
                _renderer.DrawCellPublic(canvas, colIndex, row, "", groupFontColor ?? fontColor, symbolFont, groupRowBgColor ?? rowColor, null, w, x, y, CellContentAlignment.Center, rh, isSelected);
            }
        }

        // ── GroupHeader ─────────────────────────────────────────────────

        private void RenderGroupHeader(
            SKCanvas canvas, int row, int colIndex, float w, float x, float y, float rh,
            GroupModel gi, List<GroupModel> source, List<SKGridViewColumn> visCols, SKGridViewColumn col,
            SKGroupDefinition? group, SKPaint rowColor, ReflectionHelper rh2,
            SKPaint fontColor, SKPaint? groupFontColor, SKPaint? groupRowBgColor,
            SKFont symbolFont, SKFont unicodeFont, float fontSize,
            bool isSelected,
            Dictionary<string, (bool IsExpended, float x, float y, float height, float width)> toggleDetails)
        {
            string value = Convert.ToString(gi.GroupName!);

            if (group?.Target != null && group.Target == col.Name)
            {
                var (bg, fg, _) = ResolveGroupStyle(gi, source, group, rh2, rowColor, fontColor, groupFontColor, groupRowBgColor, filterByGroupName: true);
                _renderer.DrawCellPublic(canvas, colIndex, row, value, fg, symbolFont, bg, null, w, x, y, col.ContentAlignment, rh, isSelected);
            }
            else if (group?.ToggleSymbol?.TargetColumns == col.Name)
            {
                // Update toggle coordinates
                if (toggleDetails.ContainsKey(value!))
                {
                    var vals = toggleDetails[value];
                    vals.x = x; vals.y = y; vals.width = w; vals.height = rh;
                    toggleDetails[value] = vals;
                }

                var toggleText = gi.IsExpanded ? (group?.ToggleSymbol?.Expand ?? "") : (group?.ToggleSymbol?.Collapse ?? "");
                // Use cached SKPaint instead of creating new per frame (memory leak fix)
                SKPaint? toggleBg = !string.IsNullOrEmpty(group?.ToggleSymbol?.BackgroundColor) ? SKPaintCache.Get(group.ToggleSymbol.BackgroundColor) : null;
                SKPaint? toggleFg = !string.IsNullOrEmpty(group?.ToggleSymbol?.ForegroundColor) ? SKPaintCache.Get(group.ToggleSymbol.ForegroundColor) : null;

                if (group?.ToggleSymbol?.ShowGroupDetail == true)
                {
                    _renderer.DrawCellPublic(canvas, colIndex, row, toggleText, toggleFg ?? groupFontColor ?? fontColor, unicodeFont, toggleBg ?? groupRowBgColor ?? rowColor, null, fontSize + 10, x, y, CellContentAlignment.Left, rh, isSelected);
                    _renderer.DrawCellPublic(canvas, colIndex, row, gi.GroupName, toggleFg ?? groupFontColor ?? fontColor, symbolFont, toggleBg ?? groupRowBgColor ?? rowColor, null, w, x + fontSize + 10, y, CellContentAlignment.Left, rh, isSelected);
                }
                else
                {
                    _renderer.DrawCellPublic(canvas, colIndex, row, toggleText, groupFontColor ?? fontColor, unicodeFont, groupRowBgColor ?? rowColor, null, w, x, y, CellContentAlignment.Center, rh, isSelected);
                }
            }
            else if (MatchesHeaderField(group, col, out var header))
            {
                var valuesForTotal = FilterDataRows(source).Where(r => r.GroupName == gi.GroupName);
                var agg = _renderer._setterResolver.CalculateGroupAggregation(valuesForTotal, rh2, header!.BindingPath, header.Aggregation);
                var formatted = Helper.ApplyFormat(typeof(double), agg?.ToString(), col.Format, col.ShowBracketOnNegative, col.FormatWithAcronym);
                var (bg, fg, _) = ResolveHeaderFieldStyle(gi, valuesForTotal, header, group, rh2, rowColor, fontColor, groupFontColor, groupRowBgColor);
                _renderer.DrawCellPublic(canvas, colIndex, row, col.ShowGroupAggregateData ? formatted : "", fg, symbolFont, bg, null, w, x, y, col.ContentAlignment, rh, isSelected);
            }
            else
            {
                _renderer.DrawCellPublic(canvas, colIndex, row, "", groupFontColor ?? fontColor, symbolFont, groupRowBgColor ?? rowColor, null, w, x, y, CellContentAlignment.Center, rh, isSelected);
            }
        }

        // ── GroupSubTotal ───────────────────────────────────────────────

        private void RenderGroupSubTotal(
            SKCanvas canvas, int row, int colIndex, float w, float x, float y, float rh,
            GroupModel gi, List<GroupModel> source, SKGridViewColumn col,
            SKGroupDefinition? group, SKPaint rowColor, ReflectionHelper rh2,
            SKPaint fontColor, SKPaint? groupFontColor, SKPaint? groupRowBgColor,
            SKFont symbolFont, bool isSelected)
        {
            string value = Convert.ToString(gi.SubTotalGroupName!);

            if (group?.Target != null && group.Target == col.Name)
            {
                var (bg, fg, _) = ResolveGroupSubTotalStyle(gi, source, group, rh2, rowColor, fontColor, groupFontColor, groupRowBgColor);
                _renderer.DrawCellPublic(canvas, colIndex, row, "Total " + value, fg, symbolFont, bg, null, w, x, y, col.ContentAlignment, rh, isSelected);
            }
            else if (MatchesHeaderField(group, col, out var header))
            {
                var valuesForTotal = FilterDataRows(source).Where(r =>
                    rh2.ReadCurrentItemWithTypes(r.Item, gi.BindingPath).Value == gi.SubTotalGroupName &&
                    (group?.GroupBy != null ? rh2.ReadCurrentItemWithTypes(r.Item, group.GroupBy).Value == gi.GroupName : true));
                var agg = _renderer._setterResolver.CalculateGroupAggregation(valuesForTotal, rh2, header!.BindingPath, header.Aggregation);
                var formatted = Helper.ApplyFormat(typeof(double), agg?.ToString(), col.Format, col.ShowBracketOnNegative, col.FormatWithAcronym);
                var (bg, fg, _) = ResolveHeaderFieldStyle(gi, valuesForTotal, header, group, rh2, rowColor, fontColor, groupFontColor, groupRowBgColor);
                _renderer.DrawCellPublic(canvas, colIndex, row, formatted, fg, symbolFont, bg, null, w, x, y, col.ContentAlignment, rh, isSelected);
            }
            else
            {
                _renderer.DrawCellPublic(canvas, colIndex, row, "", groupFontColor ?? fontColor, symbolFont, groupRowBgColor ?? rowColor, null, w, x, y, CellContentAlignment.Center, rh, isSelected);
            }
        }

        // ── Normal Data Row within Group ────────────────────────────────

        private void RenderGroupDataRow(
            SKCanvas canvas, int row, int colIndex, float w, float x, float y, float rh,
            GroupModel gi, SKGridViewColumn col, SKPaint rowColor, ReflectionHelper rh2,
            SKPaint fontColor, SKFont symbolFont, bool isSelected,
            SKRowTemplate? rowTemplate, SKCellTemplate? cellTemplate,
            List<SKGridViewColumn> visCols)
        {
            var propVal = rh2.ReadCurrentItemWithTypes(gi.Item, col.BindingPath);
            var val = Helper.ApplyFormat(propVal.Type, propVal.Value, col.Format, col.ShowBracketOnNegative, col.FormatWithAcronym);

            // 6-step style cascade: RowTemplate → CellTemplate → Column.CellTemplate
            var s1 = SetterResolver.GetSetterValues(rh2, rowTemplate?.Setters, gi.Item);
            SKPaint bg = s1.BackgroundColor ?? rowColor;
            SKPaint fg = s1.Foregroundcolor ?? fontColor;
            SKPaint? border = s1.BorderColor;

            var s2 = TriggerEvaluator.GetTriggerTemplate(gi.Item, rh2, rowTemplate?.Triggers);
            bg = s2.BackgroundColor ?? bg; fg = s2.Foregroundcolor ?? fg; border = s2.BorderColor ?? border;

            var s3 = SetterResolver.GetSetterValues(rh2, cellTemplate?.Setters, gi.Item);
            bg = s3.BackgroundColor ?? bg; fg = s3.Foregroundcolor ?? fg; border = s3.BorderColor ?? border;

            var s4 = TriggerEvaluator.GetTriggerTemplate(gi.Item, rh2, cellTemplate?.Triggers);
            bg = s4.BackgroundColor ?? bg; fg = s4.Foregroundcolor ?? fg; border = s4.BorderColor ?? border;

            var s5 = SetterResolver.GetSetterValues(rh2, col.CellTemplate?.Setters, gi.Item);
            bg = s5.BackgroundColor ?? bg; fg = s5.Foregroundcolor ?? fg; border = s5.BorderColor ?? border;

            var s6 = TriggerEvaluator.GetTriggerTemplate(gi.Item, rh2, col.CellTemplate?.Triggers);
            bg = s6.BackgroundColor ?? bg; fg = s6.Foregroundcolor ?? fg; border = s6.BorderColor ?? border;

            _renderer.DrawCellPublic(canvas, colIndex, row, col.DataVisible ? val : "", fg, symbolFont, bg, border, w, x, y, col.ContentAlignment, rh, isSelected, col.CellTemplate, gi.Item);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static IEnumerable<GroupModel> FilterDataRows(List<GroupModel> source)
            => source.Where(r => !r.IsGroupHeader && !r.IsGroupSubTotal && !r.IsHeaderSubTotal);

        private static bool MatchesHeaderField(SKGroupDefinition? group, SKGridViewColumn col, out SKGroupField? field)
        {
            field = group?.HeaderFields?.FirstOrDefault(f => f.TargetColumns != null && f.TargetColumns == col.Name);
            return field != null;
        }

        private (SKPaint bg, SKPaint fg, SKPaint? border) ResolveGroupStyle(
            GroupModel gi, List<GroupModel> source, SKGroupDefinition? group,
            ReflectionHelper rh, SKPaint rowColor, SKPaint fontColor, SKPaint? groupFontColor, SKPaint? groupRowBgColor,
            bool filterByGroupName = false)
        {
            var defaults = SetterResolver.GetSetterValues(rh, group?.GroupCellTemplate?.Setters, gi.Item);
            SKPaint bg = defaults.BackgroundColor ?? groupRowBgColor ?? rowColor;
            SKPaint fg = defaults.Foregroundcolor ?? groupFontColor ?? fontColor;

            if (group?.GroupCellTemplate?.Triggers?.Count > 0)
            {
                var vals = filterByGroupName
                    ? FilterDataRows(source).Where(r => r.GroupName == gi.GroupName)
                    : FilterDataRows(source).Where(r => rh.ReadCurrentItemWithTypes(r.Item, gi.BindingPath).Value == gi.GroupName);
                var trigger = _renderer._triggerEvaluator.GetGroupTriggerTemplate(rh, vals, group?.GroupCellTemplate?.Triggers);
                bg = trigger.BackgroundColor ?? bg;
                fg = trigger.ForegroundColor ?? fg;
            }
            return (bg, fg, null);
        }

        private (SKPaint bg, SKPaint fg, SKPaint? border) ResolveGroupSubTotalStyle(
            GroupModel gi, List<GroupModel> source, SKGroupDefinition? group,
            ReflectionHelper rh, SKPaint rowColor, SKPaint fontColor, SKPaint? groupFontColor, SKPaint? groupRowBgColor)
        {
            var defaults = SetterResolver.GetSetterValues(rh, group?.GroupCellTemplate?.Setters, gi.Item);
            SKPaint bg = defaults.BackgroundColor ?? groupRowBgColor ?? rowColor;
            SKPaint fg = defaults.Foregroundcolor ?? groupFontColor ?? fontColor;

            if (group?.GroupCellTemplate?.Triggers?.Count > 0)
            {
                var vals = FilterDataRows(source).Where(r =>
                    rh.ReadCurrentItemWithTypes(r.Item, gi.BindingPath).Value == gi.SubTotalGroupName &&
                    rh.ReadCurrentItemWithTypes(r.Item, group.GroupBy).Value == gi.GroupName);
                var trigger = _renderer._triggerEvaluator.GetGroupTriggerTemplate(rh, vals, group?.GroupCellTemplate?.Triggers);
                bg = trigger.BackgroundColor ?? bg;
                fg = trigger.ForegroundColor ?? fg;
            }
            return (bg, fg, null);
        }

        private (SKPaint bg, SKPaint fg, SKPaint? border) ResolveHeaderFieldStyle(
            GroupModel gi, IEnumerable<GroupModel> valuesForTotal, SKGroupField header,
            SKGroupDefinition? group, ReflectionHelper rh,
            SKPaint rowColor, SKPaint fontColor, SKPaint? groupFontColor, SKPaint? groupRowBgColor)
        {
            var defaults = SetterResolver.GetSetterValues(rh, header?.GroupCellTemplate?.Setters, gi.Item);
            SKPaint bg = defaults.BackgroundColor ?? groupRowBgColor ?? rowColor;
            SKPaint fg = defaults.Foregroundcolor ?? groupFontColor ?? fontColor;

            if (header?.GroupCellTemplate?.Triggers?.Count > 0)
            {
                var trigger = _renderer._triggerEvaluator.GetGroupTriggerTemplate(rh, valuesForTotal, header?.GroupCellTemplate?.Triggers);
                bg = trigger.BackgroundColor ?? bg;
                fg = trigger.ForegroundColor ?? fg;
            }
            return (bg, fg, null);
        }
    }
}

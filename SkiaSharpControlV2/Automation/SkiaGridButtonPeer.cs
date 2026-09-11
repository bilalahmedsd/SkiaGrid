using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace SkiaSharpControlV2.Automation
{
    /// <summary>
    /// Automation peer for a button rendered inside a grid cell.
    /// Exposes button name/text via IValueProvider and click via IInvokeProvider.
    /// </summary>
    public class SkiaGridButtonPeer : SkiaGridItemPeerBase, IInvokeProvider, IValueProvider
    {
        private readonly SkiaGridViewV2AutomationPeer _gridPeer;
        private readonly AutomationDataProvider _data;
        private readonly SkButton _button;
        private readonly int _rowIndex;
        private readonly int _colIndex;

        internal SkiaGridButtonPeer(SkiaGridViewV2AutomationPeer gridPeer, AutomationDataProvider data,
            SkButton button, int rowIndex, int colIndex)
        {
            _gridPeer = gridPeer;
            _data = data;
            _button = button;
            _rowIndex = rowIndex;
            _colIndex = colIndex;
        }

        // ── AutomationPeer overrides ────────────────────────────────────

        protected override string GetClassNameCore() => "SkiaGridButton";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetNameCore() => _button.Text ?? _button.Name ?? "Button";
        protected override string GetAutomationIdCore() => $"Button_{_rowIndex}_{_colIndex}_{_button.Name}";
        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
        protected override List<AutomationPeer>? GetChildrenCore() => null;
        protected override Rect GetBoundingRectangleCore() => _data.GetCellBounds(_rowIndex, _colIndex);
        protected override bool IsOffscreenCore() => false;

        protected override object? GetPatternInternal(PatternInterface patternInterface)
        {
            return patternInterface switch
            {
                PatternInterface.Invoke => this,
                PatternInterface.Value => this,
                _ => null
            };
        }

        // ── IInvokeProvider (click the button) ──────────────────────────

        public void Invoke()
        {
            var item = _data.GetDataItem(_rowIndex);
            if (item == null) return;

            // Fire ICommand if available
            if (_button.Command != null)
            {
                var param = _button.CommandParameter ?? item;
                if (_button.Command.CanExecute(param))
                    _button.Command.Execute(param);
            }

            // Fire Action delegate if available
            _button.OnClicked?.Invoke(_button, item);
        }

        // ── IValueProvider ──────────────────────────────────────────────

        public string Value => _button.Text ?? _button.Name ?? "";
        public bool IsReadOnly => true;
        public void SetValue(string value) => throw new InvalidOperationException("Button is read-only");
    }
}

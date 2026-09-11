using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace SkiaSharpControlV2.Automation
{
    /// <summary>
    /// Base class for virtual (non-visual) automation peers in SkiaGridViewV2.
    /// Provides default implementations for all AutomationPeer abstract members.
    /// Subclasses override only what they need.
    /// </summary>
    public abstract class SkiaGridItemPeerBase : AutomationPeer
    {
        protected override string GetAcceleratorKeyCore() => "";
        protected override string GetAccessKeyCore() => "";
        protected override string GetHelpTextCore() => "";
        protected override string GetItemStatusCore() => "";
        protected override string GetItemTypeCore() => "";
        protected override AutomationPeer? GetLabeledByCore() => null;
        protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;
        protected override Point GetClickablePointCore() => new(double.NaN, double.NaN);
        protected override bool HasKeyboardFocusCore() => false;
        protected override bool IsEnabledCore() => true;
        protected override bool IsKeyboardFocusableCore() => false;
        protected override bool IsPasswordCore() => false;
        protected override bool IsRequiredForFormCore() => false;
        protected override void SetFocusCore() { }

        public override object GetPattern(PatternInterface patternInterface)
            => GetPatternInternal(patternInterface)!;

        /// <summary>Override this in subclasses to return pattern providers.</summary>
        protected virtual object? GetPatternInternal(PatternInterface patternInterface) => null;
    }
}

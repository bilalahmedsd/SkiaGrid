using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;

using SkiaSharpControlV2;

namespace SkiaSharpControlV2.Tests.Automation;

/// <summary>
/// Verifies what an ALREADY-ATTACHED UIA client sees after the column-header row is toggled at
/// runtime — through the real client-side API, not the provider-side peers.
/// <para>
/// Note on what this does and does not prove. An earlier version of this test merely waited for a
/// StructureChanged event on <c>TreeScope.Subtree</c> and asserted it fired. That passed with AND
/// without the fix: hiding the header row changes <c>DataListView.Visibility</c>, and WPF raises its
/// own structure events for that subtree, so "an event arrived" says nothing about our raise. This
/// version asserts the thing the ticket actually asks for — that the client's view of the tree no
/// longer contains the header elements.
/// </para>
/// <para>
/// Measured limits of this test, so nobody reads more into a green run than is there: it passes with
/// the <c>InvalidateAutomationCache()</c> call in <c>OnColumnHeaderVisibleChanged</c> REMOVED. The
/// gate lives in <c>GetChildrenCore()</c> and is evaluated on every walk, so a stale
/// <c>_headerPeerCache</c> cannot resurrect headers on its own — the cached peers simply stop being
/// added. The invalidation is still correct (it keeps the cache honest across column changes and
/// drives the structure notification), but it is NOT what this test proves. Nor does this prove that
/// our own StructureChanged raise is what reaches the client: WPF raises its own structure events for
/// the DataGrid whose Visibility we flip, and a client re-walk gets a fresh tree either way.
/// </para>
/// </summary>
public class StructureChangedRaiseTests
{
    [Fact]
    public void TogglingHeaders_AnAttachedClientStopsSeeingTheHeaderElements()
    {
        Exception? captured = null;
        int headersBefore = -1, headersAfter = -1;

        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var rows = new List<object>();
                for (int i = 0; i < 300; i++) rows.Add(new { Symbol = "S" + i, Price = (double)i });

                var grid = new SkiaGridViewV2 { ItemsSource = rows, ColumnHeaderVisible = true };
                grid.Columns.Add(new SKGridViewColumn { Header = "Symbol", BindingPath = "Symbol", Width = 120 });
                grid.Columns.Add(new SKGridViewColumn { Header = "Price", BindingPath = "Price", Width = 120 });

                window = new Window { Width = 320, Height = 140, Left = -4000, Top = -4000, Content = grid };
                window.Show();
                window.UpdateLayout();

                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                var root = AutomationElement.FromHandle(hwnd);

                // Attach BEFORE the toggle — the whole point is the already-attached client.
                headersBefore = CountHeaders(root);

                grid.ColumnHeaderVisible = false;
                window.UpdateLayout();
                PumpFor(TimeSpan.FromMilliseconds(800));   // let layout + automation work drain

                headersAfter = CountHeaders(root);
            }
            catch (Exception ex) { captured = ex; }
            finally { try { window?.Close(); } catch { } }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(60));

        if (captured is not null) ExceptionDispatchInfo.Capture(captured).Throw();

        Assert.True(headersBefore >= 2, $"precondition: headers should be visible to the client first (saw {headersBefore})");
        Assert.Equal(0, headersAfter);
    }

    /// <summary>Client-side walk for our header elements by AutomationId.</summary>
    private static int CountHeaders(AutomationElement root)
    {
        int found = 0;
        foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
        {
            var id = e.Current.AutomationId;
            if (id != null && id.StartsWith("Header_", StringComparison.Ordinal)) found++;
        }
        return found;
    }

    private static void PumpFor(TimeSpan duration)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(
            duration, System.Windows.Threading.DispatcherPriority.Background,
            (s, e) => frame.Continue = false, System.Windows.Threading.Dispatcher.CurrentDispatcher);
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        timer.Stop();
    }
}

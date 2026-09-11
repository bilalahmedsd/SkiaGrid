using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

using SkiaSharpControlV2;

namespace SkiaSharpControlV2.Tests.Automation;

/// <summary>
/// Verifies what a UIA client can actually REACH, by realizing a window and walking
/// <c>GetChildren()</c> down from the root peer.
/// <para>
/// The walk is the whole point. <c>UIElementAutomationPeer.CreatePeerForElement(bar)</c> succeeds
/// today and always did — that is exactly what made the scroll-bar gap look fixed when it was not.
/// A client holds no object reference; it can only get to an element by descending from the root,
/// and the grid's peer hand-builds its child list. So these tests assert reachability from the ROOT.
/// </para>
/// </summary>
public class AutomationTreeVisibilityTests
{
    /// <summary>WPF controls require STA; xUnit's worker is MTA.</summary>
    private static void Sta(Action body)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured is not null) ExceptionDispatchInfo.Capture(captured).Throw();
    }

    private sealed class Row
    {
        public string Symbol { get; set; } = "";
        public double Price { get; set; }
    }

    /// <summary>
    /// Realizes an off-screen window around a grid sized so the vertical bar is needed, and hands the
    /// caller the grid plus a "walk the tree from the root" function.
    /// </summary>
    private static void WithRealizedGrid(bool headersVisible, Action<SkiaGridViewV2, Func<List<AutomationPeer>>, Window> body)
    {
        var rows = new List<Row>();
        for (int i = 0; i < 300; i++) rows.Add(new Row { Symbol = "SYM" + i, Price = 100 + i });

        var grid = new SkiaGridViewV2
        {
            ItemsSource = rows,
            ColumnHeaderVisible = headersVisible,
            VerticalScrollBarVisible = SKScrollBarVisibility.Auto,
            HorizontalScrollBarVisible = SKScrollBarVisibility.Hidden,
        };
        grid.Columns.Add(new SKGridViewColumn { Header = "Symbol", BindingPath = "Symbol", Width = 120 });
        grid.Columns.Add(new SKGridViewColumn { Header = "Price", BindingPath = "Price", Width = 120 });

        // Off-screen, and small enough that 300 rows overflow so the vertical bar shows.
        var window = new Window { Width = 320, Height = 140, Left = -4000, Top = -4000, Content = grid };
        window.Show();
        window.UpdateLayout();

        try
        {
            var rootPeer = UIElementAutomationPeer.CreatePeerForElement(window)
                           ?? throw new InvalidOperationException("no root peer");

            List<AutomationPeer> Walk()
            {
                var all = new List<AutomationPeer>();
                void Recurse(AutomationPeer p)
                {
                    all.Add(p);
                    foreach (var c in p.GetChildren() ?? new List<AutomationPeer>()) Recurse(c);
                }
                rootPeer.ResetChildrenCache();
                Recurse(rootPeer);
                return all;
            }

            body(grid, Walk, window);
        }
        finally
        {
            // Never let a closing window take the Application down with it — with the default
            // ShutdownMode.OnLastWindowClose every later window reports an empty tree, which reads
            // exactly like "the peers are gone".
            window.Close();
        }
    }

    private static bool HasId(List<AutomationPeer> peers, string automationId) =>
        peers.Any(p => p.GetAutomationId() == automationId);

    private static int CountClass(List<AutomationPeer> peers, string className) =>
        peers.Count(p => p.GetClassName() == className);

    // ── Item 1: the scroll bars ─────────────────────────────────────────

    [Fact]
    public void VerticalScrollBar_IsReachableFromTheRootWalk_WhileShowing()
    {
        Sta(() => WithRealizedGrid(headersVisible: true, (grid, walk, _) =>
        {
            var peers = walk();

            Assert.True(HasId(peers, "ScrollBar_Vertical"),
                "the vertical scroll bar was not reachable by walking down from the root");
            Assert.True(CountClass(peers, "ScrollBar") >= 1,
                "no ScrollBar control type anywhere in the walked tree");
        }));
    }

    [Fact]
    public void HiddenScrollBar_IsAbsentFromTheTree()
    {
        Sta(() => WithRealizedGrid(headersVisible: true, (grid, walk, _) =>
        {
            grid.VerticalScrollBarVisible = SKScrollBarVisibility.Hidden;
            grid.UpdateLayout();

            Assert.False(HasId(walk(), "ScrollBar_Vertical"),
                "the vertical scroll bar is hidden but still exposed to automation");
        }));
    }

    // ── Item 2: the column headers ──────────────────────────────────────

    [Fact]
    public void Headers_ArePresent_WhenTheHeaderRowIsShown()
    {
        Sta(() => WithRealizedGrid(headersVisible: true, (grid, walk, _) =>
        {
            var peers = walk();
            Assert.True(HasId(peers, "Header_0"));
            Assert.True(HasId(peers, "Header_1"));
        }));
    }

    [Fact]
    public void Headers_AreAbsent_WhenTheHeaderRowIsHidden()
    {
        Sta(() => WithRealizedGrid(headersVisible: false, (grid, walk, _) =>
        {
            var peers = walk();
            Assert.False(HasId(peers, "Header_0"),
                "the header row is hidden but Header_0 is still in the tree");
            Assert.False(HasId(peers, "Header_1"));
        }));
    }

    /// <summary>
    /// The case an already-attached client hits: the peers are cached, so a toggle that does not drop
    /// the cache leaves the client looking at headers the user can no longer see.
    /// </summary>
    [Fact]
    public void TogglingHeadersAtRuntime_RefreshesTheTree()
    {
        Sta(() => WithRealizedGrid(headersVisible: true, (grid, walk, _) =>
        {
            Assert.True(HasId(walk(), "Header_0"), "precondition: headers should start visible");

            grid.ColumnHeaderVisible = false;
            grid.UpdateLayout();
            Assert.False(HasId(walk(), "Header_0"),
                "headers were hidden at runtime but the cached peers were still served");

            grid.ColumnHeaderVisible = true;
            grid.UpdateLayout();
            Assert.True(HasId(walk(), "Header_0"),
                "headers were shown again but did not come back");
        }));
    }

    /// <summary>
    /// ITableProvider reads the same cache as the tree walk, so it needs the same gate — otherwise a
    /// client that asks the table provider still gets headers the user cannot see.
    /// </summary>
    [Fact]
    public void TableProvider_ColumnHeaders_FollowTheSameGate()
    {
        Sta(() => WithRealizedGrid(headersVisible: true, (grid, _, __) =>
        {
            var peer = (SkiaSharpControlV2.Automation.SkiaGridViewV2AutomationPeer)
                       UIElementAutomationPeer.CreatePeerForElement(grid)!;
            var table = (System.Windows.Automation.Provider.ITableProvider)peer;

            Assert.NotEmpty(table.GetColumnHeaders());

            grid.ColumnHeaderVisible = false;
            grid.UpdateLayout();

            Assert.Empty(table.GetColumnHeaders());
        }));
    }
}

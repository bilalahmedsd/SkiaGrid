using SkiaSharpControlV2;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    /// <summary>
    /// Dedicated, standalone demo window that mirrors the P1 Global Position Summary (GPS) MAIN grid:
    /// grouped by Account with Sum aggregates (Position / DailyPl / Value), a Distinct HeaderField on
    /// InstrumentType targeting the Inst column, and the Inst column flagged ShowSubTotalOnSort.
    ///
    /// It opens ALREADY grouped (GroupSettings declared in XAML, exactly as P1 does) AND already sorted
    /// by the Inst column (the sort is applied in <see cref="OnLoaded"/> — the approved code-behind
    /// grouping/sort-setup exception), so the distinct-subtotal flatten path is exercised on open with
    /// no user interaction. The intended, just-fixed behavior is then visible immediately:
    ///   - the grand totals (Total Equity / Total Options, summed across ALL accounts) appear ONCE at
    ///     the very top, and
    ///   - each Account group shows exactly one subtotal per instrument type right under its header,
    ///     with NO duplicate grand-total block above the 2nd / 3rd group.
    /// Data shaping lives in <see cref="GpsDemoViewModel"/>; the window only wires the sort on load.
    /// </summary>
    public partial class GpsDemoWindow : Window
    {
        private readonly GpsDemoViewModel _vm = new();
        private readonly DispatcherTimer _refreshTimer;

        public GpsDemoWindow()
        {
            DataContext = _vm;
            InitializeComponent();

            // Refresh timer keeps the Skia canvas in sync (repaint is consumer-driven).
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _refreshTimer.Tick += (s, e) => skiaGrid.Refresh();
            _refreshTimer.Start();

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        /// <summary>
        /// Grouping is declared in XAML (GroupSettings), so the grid is already grouped by Account when
        /// it first paints — matching P1. Here we apply the Inst-column sort (ShowSubTotalOnSort) that
        /// turns on the distinct per-instrument subtotals, expand every group so the subtotals + data
        /// rows are visible, and force a repaint. This is the approved code-behind grouping/sort-setup
        /// exception; no data shaping or business logic lives here.
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Clear any stray sort, then sort ascending by the Inst column. With the Distinct HeaderField
            // on InstrumentType + ShowSubTotalOnSort on this column, this is exactly what triggers the
            // grand-totals-at-top + one-subtotal-per-group flatten (DataFlatteningService).
            foreach (var col in skiaGrid.Columns)
                col.GridViewColumnSort = SkGridViewColumnSort.None;

            var instCol = skiaGrid.Columns.FirstOrDefault(c => c.Name == "InstCol");
            if (instCol != null)
                instCol.GridViewColumnSort = SkGridViewColumnSort.Ascending;

            skiaGrid.ExpandAll();
            skiaGrid.Refresh();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _refreshTimer.Stop();
        }
    }

    /// <summary>
    /// A single GPS position row. Generic demo data only — no real / company data. Numeric fields
    /// (Position / DailyPl / Value) are what the group Sum aggregates roll up; InstrumentType is the
    /// Distinct field that drives the per-instrument subtotals.
    /// </summary>
    public class GpsPosition
    {
        public string Account { get; set; } = "";
        public string InstrumentType { get; set; } = "";   // "Equity" or "Options"
        public string InstCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string Symbol { get; set; } = "";
        public double Position { get; set; }
        public double Value { get; set; }
        public double DailyPl { get; set; }
    }

    /// <summary>
    /// View model for <see cref="GpsDemoWindow"/>. Owns the GPS-like sample data: 3 accounts (3 groups),
    /// each with several positions spanning 2 instrument types (Equity / Options), so both the
    /// grand-total-at-top rows and the per-group subtotals are clearly visible.
    /// </summary>
    public class GpsDemoViewModel
    {
        public ObservableCollection<GpsPosition> Positions { get; } = new();
        public ObservableCollection<object> SelectedItems { get; set; } = new();

        public GpsDemoViewModel()
        {
            foreach (var p in BuildSampleData())
                Positions.Add(p);
        }

        private static GpsPosition[] BuildSampleData() => new[]
        {
            // ── ACCT-100 : 2 Equity + 2 Options ──────────────────────────
            new GpsPosition { Account = "ACCT-100", InstrumentType = "Equity",  InstCode = "STK", Symbol = "AAPL",     Description = "Apple Inc",              Position =  500, Value =  92500.00, DailyPl =  1250.00 },
            new GpsPosition { Account = "ACCT-100", InstrumentType = "Equity",  InstCode = "STK", Symbol = "MSFT",     Description = "Microsoft Corp",         Position =  300, Value = 126000.00, DailyPl =   840.00 },
            new GpsPosition { Account = "ACCT-100", InstrumentType = "Options", InstCode = "OPT", Symbol = "AAPL 190C", Description = "AAPL Jan 190 Call",      Position =   10, Value =   4500.00, DailyPl =  -220.00 },
            new GpsPosition { Account = "ACCT-100", InstrumentType = "Options", InstCode = "OPT", Symbol = "TSLA 250P", Description = "TSLA Feb 250 Put",       Position =   -5, Value =  -1800.00, DailyPl =   310.00 },

            // ── ACCT-200 : 2 Equity + 2 Options ──────────────────────────
            new GpsPosition { Account = "ACCT-200", InstrumentType = "Equity",  InstCode = "STK", Symbol = "NVDA",     Description = "NVIDIA Corp",            Position =  200, Value =  88000.00, DailyPl =  2100.00 },
            new GpsPosition { Account = "ACCT-200", InstrumentType = "Equity",  InstCode = "STK", Symbol = "AMZN",     Description = "Amazon.com Inc",         Position = -150, Value = -27000.00, DailyPl =  -450.00 },
            new GpsPosition { Account = "ACCT-200", InstrumentType = "Options", InstCode = "OPT", Symbol = "NVDA 900C", Description = "NVDA Mar 900 Call",      Position =    8, Value =   6400.00, DailyPl =   540.00 },
            new GpsPosition { Account = "ACCT-200", InstrumentType = "Options", InstCode = "OPT", Symbol = "SPY 500P",  Description = "SPY Apr 500 Put",       Position =   12, Value =   3600.00, DailyPl =  -180.00 },

            // ── ACCT-300 : 2 Equity + 2 Options ──────────────────────────
            new GpsPosition { Account = "ACCT-300", InstrumentType = "Equity",  InstCode = "STK", Symbol = "GOOG",     Description = "Alphabet Inc",           Position =  120, Value =  21600.00, DailyPl =   360.00 },
            new GpsPosition { Account = "ACCT-300", InstrumentType = "Equity",  InstCode = "STK", Symbol = "META",     Description = "Meta Platforms Inc",     Position =  250, Value = 118750.00, DailyPl =  1875.00 },
            new GpsPosition { Account = "ACCT-300", InstrumentType = "Options", InstCode = "OPT", Symbol = "META 500C", Description = "META Jun 500 Call",      Position =    6, Value =   2700.00, DailyPl =   -90.00 },
            new GpsPosition { Account = "ACCT-300", InstrumentType = "Options", InstCode = "OPT", Symbol = "QQQ 440C",  Description = "QQQ May 440 Call",       Position =   15, Value =   5250.00, DailyPl =   225.00 },
        };
    }
}

using SkiaSharpControlV2;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class GroupedGridDemo : UserControl
    {
        private readonly GroupedGridViewModel _vm = new();

        public GroupedGridDemo()
        {
            DataContext = _vm;
            InitializeComponent();

            foreach (var item in RandomDataGenerator.Generate(40))
            {
                // Give some items children for tree display
                if (item.Id % 3 == 0)
                {
                    item.Details = RandomDataGenerator.Generate(2);
                    foreach (var child in item.Details)
                        child.Name = "";
                }
                _vm.Items.Add(item);
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, e) => skiaGrid.Refresh();
            timer.Start();

            // ── ExpandChanged: subscribe/unsubscribe market data per visible row ──
            // Collapsing a group unsubscribes exactly that group's rows; expanding re-subscribes them.
            skiaGrid.ExpandChanged += OnExpandChanged;
            skiaGrid.Loaded += (s, e) => ResyncSubscriptions();
        }

        /// <summary>Stands in for a market-data subscription set (one entry per subscribed row).</summary>
        private readonly HashSet<object> _subscribed = new();

        private void OnExpandChanged(object? sender, SkExpandChangedEventArgs e)
        {
            foreach (var item in e.AffectedItems)
            {
                if (e.IsExpanded) _subscribed.Add(item);
                else _subscribed.Remove(item);
            }

            var scope = e.IsBulk ? "ALL" : (e.GroupName ?? e.ParentItem?.ToString() ?? "?");
            txtSubscriptions.Text =
                $"Subscribed: {_subscribed.Count} rows  (last: {(e.IsExpanded ? "+" : "-")}{e.AffectedItems.Count} {scope}, {e.Reason})";
        }

        /// <summary>Full re-sync — use after a data refresh changes which rows exist.</summary>
        private void ResyncSubscriptions()
        {
            _subscribed.Clear();
            foreach (var item in skiaGrid.GetVisibleDataItems())
                _subscribed.Add(item);
            txtSubscriptions.Text = $"Subscribed: {_subscribed.Count} rows  (initial sync)";
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e) => skiaGrid.ExpandAll();
        private void CollapseAll_Click(object sender, RoutedEventArgs e) => skiaGrid.CollapseAll();

        private void AddToGroup_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RandomDataGenerator.Generate(3))
            {
                item.Name = "Item 1";
                _vm.Items.Add(item);
            }
        }

        private void UpdatePrices_Click(object sender, RoutedEventArgs e)
        {
            var rand = new Random();
            foreach (var item in _vm.Items)
                item.Price = rand.Next(-1000, 1000);
        }

        private void SelectionStyle_Changed(object sender, RoutedEventArgs e)
        {
            skiaGrid.SelectionStyle = chkBorderSelect.IsChecked == true
                ? SKSelectionStyle.Border : SKSelectionStyle.Fill;
        }
    }

    public class GroupedGridViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MyData> Items { get; set; } = new();
        public ObservableCollection<object> SelectedItems { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

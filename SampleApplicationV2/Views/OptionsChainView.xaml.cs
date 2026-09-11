using SkiaSharpControlV2;
using SampleApplicationV2.Models;


using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class OptionsChainView : UserControl
    {
        private readonly OptionsChainViewModel _vm = new();
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _dataTimer;

        public OptionsChainView()
        {
            DataContext = _vm;
            InitializeComponent();

            var options = MarketDataSimulator.GenerateOptionsChain("AAPL", 178.50);
            foreach (var opt in options)
            {
                if (opt.OptionType == "Call")
                    _vm.Calls.Add(opt);
                else
                    _vm.Puts.Add(opt);
            }

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _refreshTimer.Tick += (s, e) =>
            {
                callsGrid.Refresh();
                putsGrid.Refresh();
            };
            _refreshTimer.Start();

            _dataTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _dataTimer.Tick += (s, e) =>
            {
                MarketDataSimulator.UpdateOptionsChain(_vm.Calls);
                MarketDataSimulator.UpdateOptionsChain(_vm.Puts);
            };
            _dataTimer.Start();
        }

        private void UpdatePrices_Click(object sender, RoutedEventArgs e)
        {
            MarketDataSimulator.UpdateOptionsChain(_vm.Calls);
            MarketDataSimulator.UpdateOptionsChain(_vm.Puts);
        }

        private void FilterITM_Click(object sender, RoutedEventArgs e)
        {
            var filter = new Filter
            {
                Column = "IsITM",
                FilterType = FilterType.Value,
                Value = ("Equals", "True", typeof(bool))
            };
            callsGrid.AddOrUpdateFilter(filter);
            putsGrid.AddOrUpdateFilter(filter);
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            var filter = new Filter { Column = "IsITM" };
            callsGrid.RemoveFilter(filter);
            putsGrid.RemoveFilter(filter);
        }
    }

    public class OptionsChainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<OptionItem> Calls { get; set; } = new();
        public ObservableCollection<OptionItem> Puts { get; set; } = new();
        public ObservableCollection<object> SelectedCalls { get; set; } = new();
        public ObservableCollection<object> SelectedPuts { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

using System.ComponentModel;

namespace SampleApplicationV2.Models
{
    public class PositionItem : INotifyPropertyChanged
    {
        private string _account = "";
        private string _symbol = "";
        private int _position;
        private double _avgCost;
        private double _lastPrice;
        private double _marketValue;
        private double _unrealizedPnl;
        private double _realizedPnl;
        private double _totalPnl;
        private string _currency = "USD";
        private string _assetClass = "";
        private string _exchange = "";
        private string _pnlColor = "#FFFFFF";

        public string Account { get => _account; set { _account = value; OnPropertyChanged(nameof(Account)); } }
        public string Symbol { get => _symbol; set { _symbol = value; OnPropertyChanged(nameof(Symbol)); } }
        public int Position { get => _position; set { _position = value; OnPropertyChanged(nameof(Position)); } }
        public double AvgCost { get => _avgCost; set { _avgCost = value; OnPropertyChanged(nameof(AvgCost)); } }
        public double LastPrice { get => _lastPrice; set { _lastPrice = value; RecalcPnl(); OnPropertyChanged(nameof(LastPrice)); } }
        public double MarketValue { get => _marketValue; set { _marketValue = value; OnPropertyChanged(nameof(MarketValue)); } }
        public double UnrealizedPnl { get => _unrealizedPnl; set { _unrealizedPnl = value; UpdatePnlColor(); OnPropertyChanged(nameof(UnrealizedPnl)); } }
        public double RealizedPnl { get => _realizedPnl; set { _realizedPnl = value; OnPropertyChanged(nameof(RealizedPnl)); } }
        public double TotalPnl { get => _totalPnl; set { _totalPnl = value; OnPropertyChanged(nameof(TotalPnl)); } }
        public string Currency { get => _currency; set { _currency = value; OnPropertyChanged(nameof(Currency)); } }
        public string AssetClass { get => _assetClass; set { _assetClass = value; OnPropertyChanged(nameof(AssetClass)); } }
        public string Exchange { get => _exchange; set { _exchange = value; OnPropertyChanged(nameof(Exchange)); } }
        public string PnlColor { get => _pnlColor; set { _pnlColor = value; OnPropertyChanged(nameof(PnlColor)); } }

        private void RecalcPnl()
        {
            MarketValue = _position * _lastPrice;
            UnrealizedPnl = (_lastPrice - _avgCost) * _position;
            TotalPnl = UnrealizedPnl + RealizedPnl;
        }

        private void UpdatePnlColor()
        {
            PnlColor = _unrealizedPnl >= 0 ? "#00FF00" : "#FF0000";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

using System.ComponentModel;

namespace SampleApplicationV2.Models
{
    /// <summary>Nested instrument details — demonstrates dot-notation BindingPath (e.g., "Instrument.Exchange").</summary>
    public class InstrumentInfo
    {
        public string Exchange { get; set; } = "";
        public string Currency { get; set; } = "USD";
    }

    public class OrderRecord : INotifyPropertyChanged
    {
        private string _orderId = "";
        private string _symbol = "";
        private string _side = "";
        private string _type = "";
        private double _price;
        private int _quantity;
        private int _filled;
        private int _remaining;
        private string _status = "";
        private string _account = "";
        private DateTime _time;
        private string _route = "";
        private double _avgPrice;
        private string _statusColor = "#FFFFFF";
        private string _sideColor = "#FFFFFF";

        public string OrderId { get => _orderId; set { _orderId = value; OnPropertyChanged(nameof(OrderId)); } }
        public string Symbol { get => _symbol; set { _symbol = value; OnPropertyChanged(nameof(Symbol)); } }
        public string Side { get => _side; set { _side = value; UpdateSideColor(); OnPropertyChanged(nameof(Side)); } }
        public string Type { get => _type; set { _type = value; OnPropertyChanged(nameof(Type)); } }
        public double Price { get => _price; set { _price = value; OnPropertyChanged(nameof(Price)); } }
        public int Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(nameof(Quantity)); } }
        public int Filled { get => _filled; set { _filled = value; OnPropertyChanged(nameof(Filled)); } }
        public int Remaining { get => _remaining; set { _remaining = value; OnPropertyChanged(nameof(Remaining)); } }
        public string Status { get => _status; set { _status = value; UpdateStatusColor(); OnPropertyChanged(nameof(Status)); } }
        public string Account { get => _account; set { _account = value; OnPropertyChanged(nameof(Account)); } }
        public DateTime Time { get => _time; set { _time = value; OnPropertyChanged(nameof(Time)); } }
        public string Route { get => _route; set { _route = value; OnPropertyChanged(nameof(Route)); } }
        public double AvgPrice { get => _avgPrice; set { _avgPrice = value; OnPropertyChanged(nameof(AvgPrice)); } }
        public string StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); } }
        public string SideColor { get => _sideColor; set { _sideColor = value; OnPropertyChanged(nameof(SideColor)); } }

        /// <summary>Nested instrument info — demonstrates dot-notation BindingPath.</summary>
        public InstrumentInfo? Instrument { get; set; }

        /// <summary>Legs for spread/complex orders. Null for simple orders.</summary>
        public List<OrderRecord>? Legs { get; set; }

        /// <summary>True if this is a spread/complex order with multiple legs.</summary>
        public bool IsSpread => Legs != null && Legs.Count > 0;

        private void UpdateSideColor()
        {
            SideColor = _side switch
            {
                "BUY" => "#4A90D9",
                "SELL" => "#FF4444",
                "SHORT" => "#FF6666",
                "SPREAD" => "#9966FF",
                _ => "#FFFFFF"
            };
        }

        private void UpdateStatusColor()
        {
            StatusColor = _status switch
            {
                "Filled" => "#00CC00",
                "Part Fill" => "#FFAA00",
                "Open" => "#4A90D9",
                "Cancelled" => "#888888",
                "Pending Cancel" => "#FF4444",
                "Rejected" => "#FF0000",
                _ => "#FFFFFF"
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

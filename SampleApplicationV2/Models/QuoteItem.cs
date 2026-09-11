using System.ComponentModel;

namespace SampleApplicationV2.Models
{
    public class QuoteItem : INotifyPropertyChanged
    {
        private string _symbol = "";
        private double _last;
        private double _change;
        private double _changePct;
        private double _bid;
        private double _ask;
        private int _bidSize;
        private int _askSize;
        private double _high;
        private double _low;
        private double _open;
        private double _close;
        private long _volume;
        private string _exchange = "";
        private string _changeColor = "#FFFFFF";
        private string _lastColor = "#FFFFFF";
        private string _sector = "";

        public string Symbol { get => _symbol; set { _symbol = value; OnPropertyChanged(nameof(Symbol)); } }
        public double Last { get => _last; set { _last = value; OnPropertyChanged(nameof(Last)); } }
        public double Change { get => _change; set { _change = value; UpdateChangeColor(); OnPropertyChanged(nameof(Change)); } }
        public double ChangePct { get => _changePct; set { _changePct = value; OnPropertyChanged(nameof(ChangePct)); } }
        public double Bid { get => _bid; set { _bid = value; OnPropertyChanged(nameof(Bid)); } }
        public double Ask { get => _ask; set { _ask = value; OnPropertyChanged(nameof(Ask)); } }
        public int BidSize { get => _bidSize; set { _bidSize = value; OnPropertyChanged(nameof(BidSize)); } }
        public int AskSize { get => _askSize; set { _askSize = value; OnPropertyChanged(nameof(AskSize)); } }
        public double High { get => _high; set { _high = value; OnPropertyChanged(nameof(High)); } }
        public double Low { get => _low; set { _low = value; OnPropertyChanged(nameof(Low)); } }
        public double Open { get => _open; set { _open = value; OnPropertyChanged(nameof(Open)); } }
        public double Close { get => _close; set { _close = value; OnPropertyChanged(nameof(Close)); } }
        public long Volume { get => _volume; set { _volume = value; OnPropertyChanged(nameof(Volume)); } }
        public string Exchange { get => _exchange; set { _exchange = value; OnPropertyChanged(nameof(Exchange)); } }
        public string ChangeColor { get => _changeColor; set { _changeColor = value; OnPropertyChanged(nameof(ChangeColor)); } }
        public string LastColor { get => _lastColor; set { _lastColor = value; OnPropertyChanged(nameof(LastColor)); } }
        public string Sector { get => _sector; set { _sector = value; OnPropertyChanged(nameof(Sector)); } }

        private void UpdateChangeColor()
        {
            ChangeColor = _change >= 0 ? "#00FF00" : "#FF0000";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

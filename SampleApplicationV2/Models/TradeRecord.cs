using System.ComponentModel;

namespace SampleApplicationV2.Models
{
    public class TradeRecord : INotifyPropertyChanged
    {
        private string _symbol = "";
        private DateTime _time;
        private double _price;
        private int _size;
        private string _market = "";
        private string _condition = "";
        private double _bid;
        private double _ask;
        private string _side = "";
        private string _bidAskColor = "#FFFFFF";

        public string Symbol { get => _symbol; set { _symbol = value; OnPropertyChanged(nameof(Symbol)); } }
        public DateTime Time { get => _time; set { _time = value; OnPropertyChanged(nameof(Time)); } }
        public double Price { get => _price; set { _price = value; OnPropertyChanged(nameof(Price)); } }
        public int Size { get => _size; set { _size = value; OnPropertyChanged(nameof(Size)); } }
        public string Market { get => _market; set { _market = value; OnPropertyChanged(nameof(Market)); } }
        public string Condition { get => _condition; set { _condition = value; OnPropertyChanged(nameof(Condition)); } }
        public double Bid { get => _bid; set { _bid = value; OnPropertyChanged(nameof(Bid)); } }
        public double Ask { get => _ask; set { _ask = value; OnPropertyChanged(nameof(Ask)); } }
        public string Side { get => _side; set { _side = value; OnPropertyChanged(nameof(Side)); } }
        public string BidAskColor { get => _bidAskColor; set { _bidAskColor = value; OnPropertyChanged(nameof(BidAskColor)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

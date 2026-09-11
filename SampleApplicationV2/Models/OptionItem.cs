using System.ComponentModel;

namespace SampleApplicationV2.Models
{
    public class OptionItem : INotifyPropertyChanged
    {
        private string _symbol = "";
        private double _strike;
        private string _expiry = "";
        private string _optionType = "";
        private double _bid;
        private double _ask;
        private double _last;
        private int _volume;
        private int _openInterest;
        private double _impliedVol;
        private double _delta;
        private double _gamma;
        private double _theta;
        private double _vega;
        private bool _isITM;
        private double _underlyingPrice;
        private string _itmColor = "Transparent";
        private string _trendColor = "#FFFFFF";

        public string Symbol { get => _symbol; set { _symbol = value; OnPropertyChanged(nameof(Symbol)); } }
        public double Strike { get => _strike; set { _strike = value; OnPropertyChanged(nameof(Strike)); } }
        public string Expiry { get => _expiry; set { _expiry = value; OnPropertyChanged(nameof(Expiry)); } }
        public string OptionType { get => _optionType; set { _optionType = value; OnPropertyChanged(nameof(OptionType)); } }
        public double Bid { get => _bid; set { _bid = value; OnPropertyChanged(nameof(Bid)); } }
        public double Ask { get => _ask; set { _ask = value; OnPropertyChanged(nameof(Ask)); } }
        public double Last { get => _last; set { _last = value; OnPropertyChanged(nameof(Last)); } }
        public int Volume { get => _volume; set { _volume = value; OnPropertyChanged(nameof(Volume)); } }
        public int OpenInterest { get => _openInterest; set { _openInterest = value; OnPropertyChanged(nameof(OpenInterest)); } }
        public double ImpliedVol { get => _impliedVol; set { _impliedVol = value; OnPropertyChanged(nameof(ImpliedVol)); } }
        public double Delta { get => _delta; set { _delta = value; OnPropertyChanged(nameof(Delta)); } }
        public double Gamma { get => _gamma; set { _gamma = value; OnPropertyChanged(nameof(Gamma)); } }
        public double Theta { get => _theta; set { _theta = value; OnPropertyChanged(nameof(Theta)); } }
        public double Vega { get => _vega; set { _vega = value; OnPropertyChanged(nameof(Vega)); } }
        public bool IsITM { get => _isITM; set { _isITM = value; UpdateItmColor(); OnPropertyChanged(nameof(IsITM)); } }
        public double UnderlyingPrice { get => _underlyingPrice; set { _underlyingPrice = value; OnPropertyChanged(nameof(UnderlyingPrice)); } }
        public string ItmColor { get => _itmColor; set { _itmColor = value; OnPropertyChanged(nameof(ItmColor)); } }
        public string TrendColor { get => _trendColor; set { _trendColor = value; OnPropertyChanged(nameof(TrendColor)); } }

        private void UpdateItmColor()
        {
            ItmColor = _isITM ? "#2A3A2A" : "Transparent";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

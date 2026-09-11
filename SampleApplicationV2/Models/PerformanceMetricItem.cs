using System.ComponentModel;

namespace SampleApplicationV2.Models
{
    public class PerformanceMetricItem : INotifyPropertyChanged
    {
        private string _operation = "";
        private long _calls;
        private double _avgMs;
        private double _minMs;
        private double _maxMs;
        private double _lastMs;
        private double _totalMs;
        private string _status = "OK";
        private string _statusColor = "#00CC00";
        private double _thresholdMs;

        public string Operation { get => _operation; set { _operation = value; OnPropertyChanged(nameof(Operation)); } }
        public long Calls { get => _calls; set { _calls = value; OnPropertyChanged(nameof(Calls)); } }
        public double AvgMs { get => _avgMs; set { _avgMs = value; UpdateStatus(); OnPropertyChanged(nameof(AvgMs)); } }
        public double MinMs { get => _minMs; set { _minMs = value; OnPropertyChanged(nameof(MinMs)); } }
        public double MaxMs { get => _maxMs; set { _maxMs = value; OnPropertyChanged(nameof(MaxMs)); } }
        public double LastMs { get => _lastMs; set { _lastMs = value; OnPropertyChanged(nameof(LastMs)); } }
        public double TotalMs { get => _totalMs; set { _totalMs = value; OnPropertyChanged(nameof(TotalMs)); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public string StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); } }
        public double ThresholdMs { get => _thresholdMs; set { _thresholdMs = value; OnPropertyChanged(nameof(ThresholdMs)); } }

        private void UpdateStatus()
        {
            if (_thresholdMs <= 0) { Status = "—"; StatusColor = "#888888"; return; }

            if (_avgMs <= _thresholdMs * 0.6)
            {
                Status = "OK";
                StatusColor = "#00CC00";
            }
            else if (_avgMs <= _thresholdMs)
            {
                Status = "WARN";
                StatusColor = "#FFAA00";
            }
            else
            {
                Status = "SLOW";
                StatusColor = "#FF4444";
            }
        }

        public void UpdateFrom(SkiaSharpControlV2.Diagnostics.MetricEntrySnapshot entry)
        {
            Calls = entry.CallCount;
            TotalMs = entry.TotalMs;
            MinMs = entry.MinMs;
            MaxMs = entry.MaxMs;
            LastMs = entry.LastMs;
            AvgMs = entry.AvgMs;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Threshold Presets ──

        public static Dictionary<string, double> DefaultThresholds => new()
        {
            // Per-frame rendering
            ["Render.Draw"] = 5.0,
            ["Render.PaintSurface"] = 8.0,
            // Per-batch data operations
            ["CollectionView.Refresh"] = 2.0,
            ["Data.UpdateCollection"] = 3.0,
            ["Data.FlattenGrouped"] = 1.0,
            ["Data.FlattenRows"] = 1.0,
            ["Data.InsertNewItem"] = 1.0,
            // Per-user-action
            ["Sort.Apply"] = 2.0,
            ["Filter.Apply"] = 3.0,
            ["Group.Aggregate"] = 1.0,
            ["Column.Update"] = 5.0,
            ["Export.Data"] = 10.0,
            // Per-input-event (should be near-instant)
            ["Input.MouseClick"] = 10.0,
            ["Input.MouseWheel"] = 2.0,
            ["Input.KeyDown"] = 5.0,
            ["Selection.Update"] = 5.0,
            ["Scroll.Update"] = 2.0,
        };
    }
}

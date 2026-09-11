using SkiaSharpControlV2;


using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class BasicGridDemo : UserControl
    {
        private readonly BasicGridViewModel _vm = new();

        public BasicGridDemo()
        {
            DataContext = _vm;
            InitializeComponent();

            foreach (var item in RandomDataGenerator.Generate(50))
                _vm.Items.Add(item);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, e) => skiaGrid.Refresh();
            timer.Start();
        }

        private void AddItems_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RandomDataGenerator.Generate(5))
                _vm.Items.Add(item);
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _vm.SelectedItems.ToList())
                _vm.Items.Remove((MyData)item);
        }

        private void InsertRow_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedItems.Count != 1) return;
            var selected = (MyData)_vm.SelectedItems[0];
            var index = _vm.Items.IndexOf(selected);
            if (index >= 0)
            {
                _vm.Items.Insert(index, new MyData
                {
                    Id = _vm.Items.Count + 1,
                    Name = "** New Row **",
                    Price = 0,
                    Category = "New",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedItems.Count != 1) return;
            var selected = (MyData)_vm.SelectedItems[0];
            var index = _vm.Items.IndexOf(selected);
            if (index > 0)
                _vm.Items.Move(index, index - 1);
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedItems.Count != 1) return;
            var selected = (MyData)_vm.SelectedItems[0];
            var index = _vm.Items.IndexOf(selected);
            if (index >= 0 && index < _vm.Items.Count - 1)
                _vm.Items.Move(index, index + 1);
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e) => skiaGrid.SelectAllRows();

        private void FilterActive_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.AddValueFilter("IsActive", "Equals", "True", typeof(bool));
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.RemoveFilter("IsActive");
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var text = skiaGrid.ExportData(SKExportType.Selected);
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        private void SelectionStyle_Changed(object sender, RoutedEventArgs e)
        {
            skiaGrid.SelectionStyle = chkBorderSelect.IsChecked == true
                ? SKSelectionStyle.Border
                : SKSelectionStyle.Fill;
        }
    }

    public class BasicGridViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MyData> Items { get; set; } = new();
        public ObservableCollection<object> SelectedItems { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}


using SkiaSharpControlV2;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SampleApplicationV2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel viewModel = new();
        public MainWindow()
        {
            DataContext = viewModel;
            InitializeComponent();
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(250);
            timer.Tick += (s, e) =>
            {
                skiaGrid.Refresh();
            };
            timer.Start();
            //skiaGrid.Font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("Arial"), 18);

            img = new BitmapImage(new Uri("P1_logo_low.jpg", UriKind.Relative));
            img2 = new BitmapImage(new Uri("logop1.jpg", UriKind.Relative));

            
            Loaded += MainWindow_Loaded;
        }
        ImageSource img;
        ImageSource img2;
        int i = 0;
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var item in RandomDataGenerator.Generate(10))
            {
                if (i > 1)
                {
                    item.Details = RandomDataGenerator.Generate(2);
                    foreach (var item1 in item.Details)
                    {
                        item1.Name = "";
                    }
                }
                i++;
                viewModel.Items.Add(item);
            }
            var priceColumn = skiaGrid.Columns.FirstOrDefault(c => c.Name == "Price");
            if (priceColumn?.CellTemplate is SKCellTemplate template)
            {
                template.DrawButton = ButtonFactory; // direct hook
            }
        }

        private void CopyAllData_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(skiaGrid.ExportData(SKExportType.All));
        }
        private void CopySelectedData_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(skiaGrid.ExportData(SKExportType.Selected));
        }

        private void MoveRowUp_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.SelectedItems.Count != 1) return;
            var item = viewModel.SelectedItems[0];
            var index = viewModel.Items.IndexOf((MyData)item);
            if (index > 0)
                viewModel.Items.Move(index, index - 1);
        }

        private void MoveRowDown_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.SelectedItems.Count != 1) return;
            var item = viewModel.SelectedItems[0];
            var index = viewModel.Items.IndexOf((MyData)item);
            if (index >= 0 && index < viewModel.Items.Count - 1)
                viewModel.Items.Move(index, index + 1);
        }

        private void InsertBlankRow_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.SelectedItems.Count != 1) return;
            var item = viewModel.SelectedItems[0];
            var index = viewModel.Items.IndexOf((MyData)item);
            if (index >= 0)
                viewModel.Items.Insert(index, new MyData());
        }

        private void DeleteBlankRow_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in viewModel.SelectedItems.ToList())
            {
                if (item is MyData d && string.IsNullOrEmpty(d.Description))
                {
                    viewModel.Items.Remove(d);
                }
            }
        }
        public Func<object, List<SkButton>> ButtonFactory => (object o) =>
     {
         if (o is MyData d)
         {
             if (d.Name == "Item 36")
             {
                 return new List<SkButton>
                     {
                    new SkButton { Text = "+", Name = "OkBtn",Width=20,MarginLeft=5,MarginRight=5,OnClicked = OnButtonClick ,ImageSource = img2,BackgroundColor="#000000"},
                     };
             }
             else
             {
                 return new List<SkButton>
                     {
                    new SkButton { Text = "+", Name = "OkBtn",Width=20,MarginLeft=5,MarginRight=5,OnClicked = OnButtonClick ,ImageSource = img},
                    new SkButton { Text = "x", Name = "CancelBtn",Width=20,OnClicked = OnButtonClick  }
                     };
             }
         }
         return null;
     };


        public Action<SkButton, object> OnButtonClick => (button, obj) =>
        {
            if (obj is MyData d)
            {
                MessageBox.Show($"{d.Name} is clicked!");
            }
        };

        private void skiaGrid_ColumnReordering(object sender, System.Windows.Controls.DataGridColumnReorderingEventArgs e)
        {
            e.Cancel = true; // disable column reordering   
        }
        public bool IsExtended { get; set; } = true;
        private void ToggleItem_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ToggleGroup("Item 1", IsExtended);
            IsExtended = !IsExtended;
        }
        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.CollapseAll();
        }
        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.ExpandAll();
        }
    }
}
using SkiaSharp;
using SkiaSharpControls.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp1.UserControls;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel viewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = viewModel;
            DispatcherTimer dispatcher = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            Loaded += (s, e) =>
            {
                skiaGrid.Font = font;
            };
            dispatcher.Tick += (s, e) =>
            {
                RefreshSkia();
            };
            dispatcher.Start();
        }
        public static SKFont font = new SKFont() { Size = 12, Typeface = SKTypeface.FromFamilyName("Arial") };


        public void RefreshSkia()
        {
            skiaGrid.Refresh();
        }

        private void AddDummyRow(object sender, RoutedEventArgs e)
        {
            //viewModel.MyDataCollection.Add(new MyData
            //{
            //    Age = 36,
            //    Id = 7,
            //    Name = "KKK"
            //});

            var textbox = new TextBoxContainer()
            {
                Width = 50,
                Height = 18,
                AllowAlphabet = true,
                UseCaps = true,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
            };

            textbox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
                {
                    MessageBox.Show(textbox.TextContent);
                }
            };

            skiaGrid.AddWpfElement(textbox);
        }

        private void ToggleGridLines(object sender, RoutedEventArgs e)
        {
            viewModel.ShowGridLines = !viewModel.ShowGridLines;
            skiaGrid.Refresh();
        }

        private void ToggleColumnHeader(object sender, RoutedEventArgs e)
        {
            skiaGrid.ColumnHeaderVisible = !skiaGrid.ColumnHeaderVisible;
        }

        private void ChangeColumnsPosition(object sender, RoutedEventArgs e)
        {
            skiaGrid.Columns = new List<SkGridViewColumn>() {
                    new SkGridViewColumn() { Header = "Age" , DisplayHeader="Age (= 20)" },
                    new SkGridViewColumn() { Header = "Id" },
                    new SkGridViewColumn() { Header = "Trend" },
                    new SkGridViewColumn() { Header = "Trend2" },
                    new SkGridViewColumn() { Header = "Name",GridViewColumnSort=SkiaSharpControls.Enum.SkGridViewColumnSort.Descending },
                    new SkGridViewColumn() { Header = "IsToggle" },
                    new SkGridViewColumn() { Header = "Highlighted" }
        };
        }
        private void ChangeColumnsPosition2(object sender, RoutedEventArgs e)
        {
            skiaGrid.Columns = new List<SkGridViewColumn>() {
                    new SkGridViewColumn() { Header = "Age"  },
                    new SkGridViewColumn() { Header = "Id",IsVisible =false },
                    new SkGridViewColumn() { Header = "Trend" },
                    new SkGridViewColumn() { Header = "Trend2" },
                    new SkGridViewColumn() { Header = "Name" },
                    new SkGridViewColumn() { Header = "IsToggle" },
                    new SkGridViewColumn() { Header = "Highlighted" }
            };
        }

        private void ToggleResize(object sender, RoutedEventArgs e)
        {
            skiaGrid.CanUserResizeColumns = !skiaGrid.CanUserResizeColumns;
        }

        private void ToggleSort(object sender, RoutedEventArgs e)
        {
            skiaGrid.CanUserSortColumns = !skiaGrid.CanUserSortColumns;
        }

        private void ToggleReorder(object sender, RoutedEventArgs e)
        {
            skiaGrid.CanUserReorderColumns = !skiaGrid.CanUserReorderColumns;
        }

        private void ChangeFont(object sender, RoutedEventArgs e)
        {
            font = new SKFont() { Size = 18, Typeface = SKTypeface.FromFamilyName("Arial",SKFontStyle.Bold) };
            skiaGrid.Font = font;
        }

        private void ToggleVerticalScrollBar(object sender, RoutedEventArgs e)
        {
            skiaGrid.VerticalScrollBarVisible = !skiaGrid.VerticalScrollBarVisible;
        }
    }
}

using SkiaSharpControlV2;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;


namespace SampleApplicationV2
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MyData> Items { get; set; }

        public MainViewModel()
        {
            Columns = new Columns();

            Items = new ObservableCollection<MyData>();
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(250);
            timer.Tick += (s, e) =>
            {

                foreach (var item in RandomDataGenerator.Generate(5))
                {
                    if (Items.Count >= 150)
                    {
                        // Items.RemoveAt(Items.Count - 1);
                    }
                    item.Description = null;
                    Items.Insert(0, item);
                }

            };
            // timer.Start();

        }
     

        private bool isLiveSort = true;
        public bool IsLiveSort { get => isLiveSort; set { isLiveSort = value; OnPropertyChanged(nameof(IsLiveSort)); } }

        private bool isTimerBaseSort = false;
        public bool IsTimerBaseSort { get => isTimerBaseSort; set { isTimerBaseSort = value; OnPropertyChanged(nameof(IsTimerBaseSort)); } }

        private int sortEvery = 0;
        public int SortEvery
        {
            get => sortEvery;
            set
            {
                sortEvery = value;
                IsTimerBaseSort = !(value == 0);

                OnPropertyChanged(nameof(SortEvery));
            }
        }
        public Columns Columns { get; set; }

        #region Commands
        public ICommand ChangeColumns1 => new RelayCommand(() =>
        {
            Columns.Price.BackColor = null;
            Columns.Name.IsVisible = true;
            Columns.Quantity.IsVisible = false;
            Columns.Id.IsVisible = false;
        });
        public ICommand ChangeColumns2 => new RelayCommand(() =>
        {
            Columns.Id.IsVisible = true;
            Columns.Price.BackColor = "Red";
            Columns.IsActive.IsVisible = false;
            Columns.IsDeleted.IsVisible = false;
            Columns.UpdatedAt.IsVisible = false;
            Columns.Category.IsVisible = false;
            Columns.Category.IsVisible = false;
            Columns.Id.Width = 50;

        });

        public ICommand ChangeColumns3 => new RelayCommand(() =>
        {
            Columns = new();

            OnPropertyChanged(nameof(Columns));

        });
        public ICommand InsertNewItems => new RelayCommand(() =>
        {
            //foreach (var item in Items.ToList())
            //{
            //    Items.Remove(item);
            //}
            foreach (var item in RandomDataGenerator.Generate(5))
            {
                item.Name = "Item 1";
                Items.Insert(1, item);
            }

        });

        public ObservableCollection<object> SelectedItems { get; set; } = new ObservableCollection<object>();
        public ICommand UpdateItemsValues => new RelayCommand(() =>
        {
            var item = RandomDataGenerator.Generate(100);
            Random rand = new();
            foreach (var items in Items.Where(x => x.Name == "Item 1"))
            {
                var randomnumber = rand.Next(1, 100);
                items.Id = item[randomnumber].Id;
                items.Price = item[randomnumber].Price;
                items.Rating = item[randomnumber].Rating;
                items.CreatedAt = item[randomnumber].CreatedAt;
                items.Description = item[randomnumber].Description;
                items.Quantity = item[randomnumber].Quantity;
                items.Discount = item[randomnumber].Discount;
            }
        });
        public ICommand EnableTimerBaseSorting => new RelayCommand(() =>
        {
            if (SortEvery == 0)
                SortEvery = 5;
            else
                SortEvery = 0;
        });
        public ICommand RemoveSelectedItems => new RelayCommand(() =>
        {
            foreach (var item in SelectedItems)
            {
                Items.Remove((MyData)item);
            }
        });

        public ICommand UpdateLiveSort => new RelayCommand(() =>
        {
            IsLiveSort = !IsLiveSort;
        });
        #endregion 

        public Action<SkButton, object> OnButtonClick => (button, obj) =>
        {
            if (obj is MyData d)
            {
                MessageBox.Show($"{d.Name} is clicked!");
            }
        };


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

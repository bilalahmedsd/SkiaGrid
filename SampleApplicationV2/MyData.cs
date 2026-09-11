



using SkiaSharpControlV2;
using SkiaSharpControlV2.Model;
using System.ComponentModel;

namespace SampleApplicationV2
{
    public class MyData
    {
        public string GroupColumn { get; set; }
        public int Id { get; set; }
        public string? Name { get; set; }
        public double Price { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? Description { get; set; }
        public int Quantity { get; set; }
        public double Discount { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string? Category { get; set; }
        public int Rating { get; set; }
        public double Weight { get; set; }
        public bool InStock { get; set; }
        public DateTime ExpiryDate { get; set; }

        public string? SupplierName { get; set; }
        public int ReorderLevel { get; set; }
        public double Height { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime LastOrdered { get; set; }

        public string? Barcode { get; set; }
        public int Views { get; set; }
        public double Width { get; set; }
        public bool IsTrending { get; set; }
        public DateTime ReleaseDate { get; set; }

        public List<MyData> Details { get; set; } = null;

        /// <summary>
        /// Last 10 tick directions (true = up, false = down). Drives the Trend column's
        /// CustomDraw delegate — 10 small red/green boxes per row, left = oldest, right = newest.
        /// </summary>
        public List<bool> Trend { get; set; } = new();



        public override string ToString()
        {
            return $"{Name} - {Price}";
        }
    }

    public class MyDataDetails
    {
        public double Price { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? Description { get; set; }
        public int Quantity { get; set; }
        public double Discount { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string? Category { get; set; }
        public int Rating { get; set; }
        public double Weight { get; set; }
        public bool InStock { get; set; }
        public DateTime ExpiryDate { get; set; }

        public string? SupplierName { get; set; }
        public int ReorderLevel { get; set; }
        public double Height { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime LastOrdered { get; set; }

        public string? Barcode { get; set; }
        public int Views { get; set; }
        public double Width { get; set; }
        public bool IsTrending { get; set; }
        public DateTime ReleaseDate { get; set; }
    }

    public static class RandomDataGenerator
    {
        private static readonly Random rand = new();

        public static List<MyData> Generate(int count = 50)
        {
            var list = new List<MyData>();
            for (int i = 0; i < count; i++)
            {
                list.Add(new MyData
                {
                    Id = i + 1,
                    Name = $"Item {rand.Next(0, 50)}",
                    Price = rand.Next(-1000, 1000),
                    IsActive = rand.Next(2) == 1,
                    CreatedAt = RandomDate(),
                    Description = $"Desc {Guid.NewGuid().ToString()[..8]}",
                    Quantity = rand.Next(1, 500),
                    Discount = rand.NextDouble() * 20,
                    IsDeleted = rand.Next(2) == 1,
                    UpdatedAt = RandomDate(),
                    Category = $"Category {rand.Next(1, 5)}",
                    Rating = rand.Next(1, 10),
                    Weight = rand.NextDouble() * 100,
                    InStock = rand.Next(2) == 1,
                    ExpiryDate = RandomDate(),
                    SupplierName = $"Supplier {rand.Next(1, 10)}",
                    ReorderLevel = rand.Next(1, 100),
                    Height = rand.NextDouble() * 50,
                    IsFeatured = rand.Next(2) == 1,
                    LastOrdered = RandomDate(),
                    Barcode = $"{rand.Next(100000, 999999)}",
                    Views = rand.Next(0, 5000),
                    Width = rand.NextDouble() * 60,
                    IsTrending = rand.Next(2) == 1,
                    ReleaseDate = RandomDate(),
                    // Seed 10 random up/down ticks for the Trend column's CustomDraw demo.
                    Trend = Enumerable.Range(0, 10).Select(_ => rand.Next(2) == 1).ToList(),
                });
            }
            return list;
        }

        private static DateTime RandomDate()
        {
            return DateTime.Today.AddDays(-rand.Next(0, 1000));
        }
    }

    public class Columns
    {
        public ColProperties IsExpandable { get; set; } = new() { Width = 40 };
        public ColProperties GroupColumn { get; set; } = new() { Width = 100 };
        public ColProperties Id { get; set; } = new() { Width = 200, SortDirection = SkGridViewColumnSort.Ascending };
        public ColProperties Name { get; set; } = new() { IsVisible = true };
        public ColProperties Price { get; set; } = new();
        public ColProperties IsActive { get; set; } = new();
        public ColProperties CreatedAt { get; set; } = new();
        public ColProperties Description { get; set; } = new();
        public ColProperties Quantity { get; set; } = new();
        public ColProperties Discount { get; set; } = new();
        public ColProperties IsDeleted { get; set; } = new();
        public ColProperties UpdatedAt { get; set; } = new();
        public ColProperties Category { get; set; } = new();
        public ColProperties Rating { get; set; } = new();
        public ColProperties Weight { get; set; } = new();
        public ColProperties InStock { get; set; } = new();
        public ColProperties ExpiryDate { get; set; } = new();
        public ColProperties SupplierName { get; set; } = new();
        public ColProperties ReorderLevel { get; set; } = new();
        public ColProperties Height { get; set; } = new();
        public ColProperties IsFeatured { get; set; } = new();
        public ColProperties LastOrdered { get; set; } = new();
        public ColProperties Barcode { get; set; } = new();
        public ColProperties Views { get; set; } = new();
        public ColProperties Width { get; set; } = new();
        public ColProperties IsTrending { get; set; } = new();
        public ColProperties ReleaseDate { get; set; } = new();
    }
    public class ColProperties : INotifyPropertyChanged
    {
        private double width = 100;
        public double Width
        {
            get => width;
            set { width = value; OnPropertyChanged(nameof(Width)); }
        }

        private bool isVisible = true;
        public bool IsVisible
        {
            get => isVisible;
            set
            {
                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }

        private int? displayIndex = null;
        public int? DisplayIndex
        {
            get => displayIndex;
            set
            {
                displayIndex = value;
                OnPropertyChanged(nameof(DisplayIndex));
            }
        }

        private string? backColor;
        public string? BackColor
        {
            get => backColor;
            set
            {
                backColor = value;
                OnPropertyChanged(nameof(BackColor));
            }
        }

        private SkGridViewColumnSort? sortDirection = SkGridViewColumnSort.None;
        public SkGridViewColumnSort? SortDirection
        {
            get => sortDirection;
            set
            {
                sortDirection = value;
                OnPropertyChanged(nameof(SortDirection));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}

namespace WpfApp1
{
    public class MyData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public bool IsToggledOn { get; set; }

        public override string ToString()
        {
            return $"{Id} {Name} {Age}";
        }
    }
}
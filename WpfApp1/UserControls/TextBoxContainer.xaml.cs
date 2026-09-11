using System.ComponentModel;
using System.Windows.Controls;

namespace WpfApp1.UserControls
{
    /// <summary>
    /// Interaction logic for TextBoxContainer.xaml
    /// </summary>
    public partial class TextBoxContainer : UserControl, INotifyPropertyChanged
    {
        public TextBoxContainer()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (obj, e) =>
            {
                tb.Focus();
                if (UseCaps)
                {
                    tb.CharacterCasing = CharacterCasing.Upper;
                }
            };
        }

        private string textContent;

        public string TextContent
        {
            get { return textContent; }
            set
            {
                if (!Validate(value))
                {
                    return;
                }

                textContent = AllowSpace ? value : value.Trim();
                OnPropertyChanged(nameof(TextContent));
            }
        }

        private bool Validate(string input)
        {
            if (input.Trim() == string.Empty)
            {
                return true;
            }

            if (AllowNumbersOnly)
            {
                return double.TryParse(input, out _);
            }

            if (AllowAlphabetOnly)
            {
                return input.All(char.IsLetter);
            }

            if (AllowSpecialCharsOnly)
            {
                return !input.All(char.IsLetterOrDigit);
            }

            if (AllowAlphabetAndNumbersOnly)
            {
                return input.All(char.IsLetterOrDigit);
            }

            if (AllowAlphabetAndSpecialCharsOnly)
            {
                return !input.Any(char.IsDigit);
            }

            if (AllowNumbersAndSpecialCharsOnly)
            {
                return !input.Any(char.IsLetter);
            }

            return true;
        }

        private bool useCaps;

        public bool UseCaps
        {
            get { return useCaps; }
            set
            {
                useCaps = value;
                OnPropertyChanged(nameof(UseCaps));
            }
        }

        public bool AllowNumbers { get; set; }
        public bool AllowAlphabet { get; set; }
        public bool AllowSpecialChars { get; set; }
        public bool AllowSpace { get; set; }

        private bool AllowNumbersOnly => AllowNumbers && !AllowAlphabet && !AllowSpecialChars;
        private bool AllowAlphabetOnly => AllowAlphabet && !AllowNumbers && !AllowSpecialChars;
        private bool AllowSpecialCharsOnly => AllowSpecialChars && !AllowNumbers && !AllowAlphabet;

        private bool AllowAlphabetAndNumbersOnly => AllowAlphabet && AllowNumbers && !AllowSpecialChars;
        private bool AllowAlphabetAndSpecialCharsOnly => AllowAlphabet && !AllowNumbers && AllowSpecialChars;
        private bool AllowNumbersAndSpecialCharsOnly => !AllowAlphabet && AllowNumbers && AllowSpecialChars;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

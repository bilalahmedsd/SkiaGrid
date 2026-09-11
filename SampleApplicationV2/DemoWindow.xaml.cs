using System.Windows;
using System.Windows.Controls;

namespace SampleApplicationV2
{
    public partial class DemoWindow : Window
    {
        private readonly UIElement[] _tabs;

        public DemoWindow()
        {
            InitializeComponent();
            _tabs = new UIElement[] { tab0, tab1, tab2, tab3, tab4, tab5, tab6 };
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tabs == null) return;

            var index = tabControl.SelectedIndex;
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

using System.Windows;
using System.Windows.Controls;

namespace SampleApplicationV2.Views
{
    /// <summary>
    /// Generic host window that displays a single UserControl (any of the demo views).
    /// Used by MultiWindowTestView to open multiple live grids side-by-side, so the
    /// fix for shared-static-state selection flicker can be visually verified.
    /// </summary>
    public partial class GridHostWindow : Window
    {
        public GridHostWindow(string title, UserControl content, double left, double top, double width, double height)
        {
            InitializeComponent();
            Title = title;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            HostContent.Content = content;
        }
    }
}

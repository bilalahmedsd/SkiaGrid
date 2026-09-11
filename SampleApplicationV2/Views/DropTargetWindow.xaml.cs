using System.Text;
using System.Windows;
using System.Windows.Media;

using SkiaSharpControlV2;

namespace SampleApplicationV2.Views
{
    /// <summary>
    /// A separate window that receives rows dragged OUT of a SkiaGrid in
    /// <see cref="SKRowDragMode.DragOut"/>. This is the whole receiver contract — there is nothing
    /// SkiaGrid-specific about it beyond the format key:
    ///
    ///   AllowDrop="True", handle DragOver to set e.Effects, handle Drop to read e.Data.
    ///
    /// In-process it gets the live row objects via <see cref="SKRowDragData"/>; the grid also puts
    /// tab-separated text on the DataObject, so the same drag works into Excel or a text box.
    /// </summary>
    public partial class DropTargetWindow : Window
    {
        private static readonly Brush Idle = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x77));
        private static readonly Brush Armed = new SolidColorBrush(Color.FromRgb(0x00, 0xE0, 0x7A));

        public DropTargetWindow()
        {
            InitializeComponent();
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            bool ours = e.Data.GetDataPresent(SKRowDragData.Format);
            bool text = e.Data.GetDataPresent(DataFormats.UnicodeText);

            e.Effects = ours || text ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;

            DropZone.BorderBrush = e.Effects == DragDropEffects.None ? Idle : Armed;
            DropHint.Text = e.Effects == DragDropEffects.None ? "Not something I take" : "Release to drop";
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            DropZone.BorderBrush = Idle;
            DropHint.Text = "Drop rows here";
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone.BorderBrush = Idle;
            DropHint.Text = "Drop rows here";

            var sb = new StringBuilder();
            sb.AppendLine($"--- drop at {DateTime.Now:HH:mm:ss.fff} ---");

            if (e.Data.GetData(SKRowDragData.Format) is SKRowDragData payload)
            {
                sb.AppendLine($"rows      : {payload.Rows.Count}");
                sb.AppendLine($"column    : {payload.Column?.Header ?? "(none)"}");
                sb.AppendLine($"cell text : {payload.CellText ?? "(none)"}");
                sb.AppendLine($"source    : {payload.Source.GetType().Name} (live object reference)");
                sb.AppendLine();
                sb.AppendLine("the row objects themselves:");
                foreach (var row in payload.Rows)
                    sb.AppendLine("  " + row);
            }
            else
            {
                sb.AppendLine("(no SkiaGrid payload — falling back to text)");
            }

            if (e.Data.GetData(DataFormats.UnicodeText) is string text)
            {
                sb.AppendLine();
                sb.AppendLine("text format (what Excel / a text box would get):");
                sb.AppendLine(text.TrimEnd());
            }

            ReceivedText.Text = sb.ToString();
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }
}

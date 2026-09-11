
using System.Collections;


namespace SkiaSharpControlV2.Data.Models
{
    public interface ITreeItem
    {
        /// <summary>
        /// Child collection (may be null or empty).
        /// </summary>
        IEnumerable? Children { get; }

        /// <summary>
        /// When true, children should be visible in view.
        /// </summary>
        bool IsExpanded { get; set; }
    }
}

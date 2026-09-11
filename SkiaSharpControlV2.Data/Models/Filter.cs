using SkiaSharpControlV2.Data.Enum;

namespace SkiaSharpControlV2.Data.Models
{
    public class Filter
    {
        public string? Column { get; set; }
        public FilterType FilterType { get; set; }
        public string? Text { get; set; }
        public List<string>? List { get; set; }
        public (string Operator, string Value, Type DataType) Value { get; set; }

        public override string ToString()
        {
            if (FilterType == FilterType.Text)
                return string.Format("({0})", Text);
            else if (FilterType == FilterType.List)
            {
                if (List == null || List.Count == 0)
                    return string.Empty;
                else
                {
                    if (List.Count == 1)
                        return string.Format("({0})", List[0]);
                    else
                        return "(...)";

                }
            }
            else
                return string.Format("({0} {1})", Value.Operator, Value.Value);
        }
    }
}

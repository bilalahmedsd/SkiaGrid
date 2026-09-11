
namespace SkiaSharpControlV2.Helpers
{
    public static class SkBindingHelper
    {
        public static object? GetNestedPropertyValue(object obj, string path)
        {
            foreach (var part in path.Split('.'))
            {
                if (obj == null) return null;
                var prop = obj.GetType().GetProperty(part);
                if (prop == null) return null;
                obj = prop.GetValue(obj);
            }
            return obj;
        }
    }
}

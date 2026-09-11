using SkiaSharp;


namespace SkiaSharpControlV2.Helpers
{
    internal static class SkFontFactory
    {
        public static SKFont CreateSkFont(string fontFamily, string styleName, float fontSize)
        {
            var typeface = SKTypeface.FromFamilyName(fontFamily, MapFontStyle(styleName));
            return new SKFont(typeface, fontSize);
        }

        private static SKFontStyle MapFontStyle(string styleName)
        { 
            return styleName.ToLowerInvariant() switch
            {
                "normal" => SKFontStyle.Normal,
                "bold"  => SKFontStyle.Bold,
                "italic" => SKFontStyle.Italic,
                "bolditalic" => SKFontStyle.BoldItalic,
                _ => SKFontStyle.Normal
            };
        }


        //private static (SKFontStyleWeight, SKFontStyleWidth, SKFontStyleSlant) MapFontStyle(string styleName)
        //{
        //    styleName = styleName.ToLowerInvariant();

        //    //  Default values
        //    SKFontStyleWeight weight = SKFontStyleWeight.Normal;
        //    SKFontStyleWidth width = SKFontStyleWidth.Normal;
        //    SKFontStyleSlant slant = SKFontStyleSlant.Upright;

        //    // Weight mapping
        //    if (styleName.Contains("thin")) weight = SKFontStyleWeight.Thin;
        //    else if (styleName.Contains("extralight") || styleName.Contains("ultralight")) weight = SKFontStyleWeight.ExtraLight;
        //    else if (styleName.Contains("light")) weight = SKFontStyleWeight.Light;
        //    else if (styleName.Contains("semibold")) weight = SKFontStyleWeight.SemiBold;
        //    else if (styleName.Contains("medium")) weight = SKFontStyleWeight.Medium;
        //    else if (styleName.Contains("bold")) weight = SKFontStyleWeight.Bold;
        //    else if (styleName.Contains("extrabold") || styleName.Contains("black")) weight = SKFontStyleWeight.Black;

        //    //  Width mapping
        //    if (styleName.Contains("ultracondensed")) width = SKFontStyleWidth.UltraCondensed;
        //    else if (styleName.Contains("extracondensed")) width = SKFontStyleWidth.ExtraCondensed;
        //    else if (styleName.Contains("condensed")) width = SKFontStyleWidth.Condensed;
        //    else if (styleName.Contains("semiexpanded")) width = SKFontStyleWidth.SemiExpanded;
        //    else if (styleName.Contains("expanded")) width = SKFontStyleWidth.Expanded;

        //    //
        //    // .Slant mapping
        //    if (styleName.Contains("italic")) slant = SKFontStyleSlant.Italic;
        //    else if (styleName.Contains("oblique")) slant = SKFontStyleSlant.Oblique;

        //    return (weight, width, slant);
        //}
    }
}

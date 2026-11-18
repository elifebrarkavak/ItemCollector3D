using UnityEngine;

namespace ItemCollector3D.EditorTools
{
    /// <summary>The Item Collector 3D UI palette (Concept Document logo): cave navy, wood brown, gold, natural green, cream.</summary>
    public static class UiPalette
    {
        public static readonly Color CaveNavy = Hex("#1D293B");
        public static readonly Color WoodBrown = Hex("#704630");
        public static readonly Color Gold = Hex("#F2B83F");
        public static readonly Color NaturalGreen = Hex("#37856B");
        public static readonly Color Cream = Hex("#FFF0D3");

        // supporting shades derived from the five brand colours
        public static readonly Color Outline = Hex("#0E1623");
        public static readonly Color NavyLight = Hex("#31445F");
        public static readonly Color NavyDeep = Hex("#141D2B");
        public static readonly Color WoodLight = Hex("#9A6844");
        public static readonly Color WoodMid = Hex("#7F5037");
        public static readonly Color WoodDark = Hex("#4A2B1B");
        public static readonly Color WoodDeepest = Hex("#2A170E");
        public static readonly Color GoldLight = Hex("#FFD974");
        public static readonly Color GoldDark = Hex("#C4861A");
        public static readonly Color GoldDeep = Hex("#8E5A0E");
        public static readonly Color GreenLight = Hex("#58AE8F");
        public static readonly Color GreenDark = Hex("#1F5F4A");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        public static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}

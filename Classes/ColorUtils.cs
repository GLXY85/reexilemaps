using System;
using System.Drawing;
using ExileCore2.PoEMemory.Components;

namespace ExileMaps.Classes
{
    public static class ColorUtils
    {
        public static Color InterpolateColor(Color color1, Color color2, float fraction)
        {
            float r = color1.R + (color2.R - color1.R) * fraction;
            float g = color1.G + (color2.G - color1.G) * fraction;
            float b = color1.B + (color2.B - color1.B) * fraction;
            float a = color1.A + (color2.A - color1.A) * fraction;

            // Restrict RGBA values to 0-255
            int iR = Math.Max(Math.Min((int)r, 255), 0);
            int iG = Math.Max(Math.Min((int)g, 255), 0);
            int iB = Math.Max(Math.Min((int)b, 255), 0);
            int iA = Math.Max(Math.Min((int)a, 255), 0);
            
            return Color.FromArgb(iA, iR, iG, iB);
        }
    }
}
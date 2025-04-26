using System;
using System.Drawing;

namespace ReExileMaps.Classes
{
    public class Biome
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public float Weight { get; set; } = 0;
        public Color Color { get; set; } = Color.White;
        
        public override string ToString()
        {
            return Name;
        }
    }
}


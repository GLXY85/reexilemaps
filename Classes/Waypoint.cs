using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameOffsets2.Native;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using ExileCore2.Shared.Enums;
using static ExileMaps.ExileMapsCore;
using Newtonsoft.Json;

namespace ExileMaps.Classes
{
    public class Waypoint
    {
        
        public string ID { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public bool Show { get; set; }
        public bool Line { get; set; }
        public bool Arrow { get; set; }
        public float Scale { get; set; } = 1;

        [JsonConverter(typeof(Vector2iConverter))]
        public Vector2i Coordinates;
        public MapIconsIndex Icon { get; set; }
        public Color Color { get; set; }

        [JsonIgnore]
        public string CoordinatesString
        {
            get => $"{Coordinates.X},{Coordinates.Y}";

        }
        
        public long Address { get; set; }

        
        public AtlasNodeDescription MapNode () {
            if (Main.AtlasPanel == null) return null;
            return Main.AtlasPanel.Descriptions.FirstOrDefault(x => x.Coordinate.ToString() == Coordinates.ToString()) ?? null;
        }
    
    }
}
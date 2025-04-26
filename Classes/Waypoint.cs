using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameOffsets.Native;
using ExileCore.PoEMemory.Elements.Atlas;
using ExileCore.Shared.Enums;
using static ReExileMaps.ReExileMapsCore;
using Newtonsoft.Json;

// Использование псевдонима для совместимости с новой структурой
using AtlasNodeDescription = ExileCore.PoEMemory.Elements.Atlas.AtlasNode;

namespace ReExileMaps.Classes
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
        public ExileCore2.Shared.Enums.MapIcon Icon { get; set; }
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


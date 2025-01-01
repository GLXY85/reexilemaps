using System;
using System.Collections.Generic;
using System.Drawing;
using ExileCore2;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExileMaps.Classes;



public class Map
{
    public string Name { get; set; }
    public string ID { get; set; }
    public string RealID { get; set; }
    public string[] Biomes { get; set; }

    public string[] Content { get; set; }

    [JsonConverter(typeof(JsonColorConverter))]
    public Color NameColor { get; set; } = Color.FromArgb(255, 255, 255, 255);

    [JsonConverter(typeof(JsonColorConverter))]
    public Color BackgroundColor { get; set; } = Color.FromArgb(200, 0, 0, 0);

    [JsonConverter(typeof(JsonColorConverter))]
    public Color NodeColor { get; set; } = Color.FromArgb(200, 155, 155, 155);

    public bool DrawLine { get; set; } = false;

    public bool Highlight { get; set; } = false;

    public int Count { get; set; } = 0;

    public float Weight { get; set; } = 1.0f;
}

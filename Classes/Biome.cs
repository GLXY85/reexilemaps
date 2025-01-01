using System;
using System.Drawing;
using System.Text.Json.Serialization;

namespace ExileMaps.Classes;

public class Biome
{
    public string Name { get; set; }
    
    public float Weight { get; set; } = 1.0f;

    [JsonConverter(typeof(JsonColorConverter))]
    public Color Color { get; set; } = Color.FromArgb(255, 255, 255, 255);

    public bool Highlight { get; set; } = true;

}
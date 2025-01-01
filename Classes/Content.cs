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
public class Content
{
    public string Name { get; set; }
    public float Weight { get; set; } = 1.0f;

    [JsonConverter(typeof(JsonColorConverter))]
    public Color Color { get; set; } = Color.FromArgb(255, 255, 255, 255);

    public bool Highlight { get; set; } = true;


}

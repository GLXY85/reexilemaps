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
public class Mod
{
    public int ModID { get; set; }
    public string Description { get; set; }
    public float Weight { get; set; }
}
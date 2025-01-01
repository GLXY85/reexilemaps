using System;
using System.Collections.Generic;
using System.Drawing;
using ExileCore2;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameOffsets2.Native;

namespace ExileMaps.Classes;



public class Node
{
    public string Address { get; set; }
   
    public bool IsVisited { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsVisible { get; set; }
    public bool IsHighlighted { get; set; }
    public bool IsActive { get; set; }
    public List<string> Content { get; set; }
    public List<Node> Neighbors { get; protected set; }
    public Vector2i Coordinate { get; set; }
    public (Vector2i, Vector2i, Vector2i, Vector2i, Vector2i) Connections { get; set; }
    public Map Area { get; set; }

    
    

}
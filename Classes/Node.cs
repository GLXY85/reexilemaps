using System.Collections.Generic;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using System.Text;
using GameOffsets2.Native;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ExileMaps.Classes;

public class Node
{
    public string Name { get; set; }
    public string Id { get; set; }

    [JsonIgnore]
    public bool IsUnlocked;
    
    [JsonIgnore]
    public bool IsVisible;
    [JsonIgnore]
    public bool IsActive;
    [JsonIgnore]
    public bool IsVisited;
    [JsonIgnore]
    public bool IsWaypoint;
    [JsonIgnore]
    public bool IsFailed => !IsUnlocked && IsVisited;
    [JsonIgnore]
    public bool IsAttempted => !IsUnlocked && IsVisited;
    [JsonIgnore]
    public bool IsTower => MapType.IsTower();
    [JsonIgnore]
    public (Vector2i, Vector2i, Vector2i, Vector2i) NeighborCoordinates { get; set; } 
    [JsonIgnore]
    public Vector2i Coordinates { get; set; }
    [JsonIgnore]
    public Dictionary<Vector2i, Node> Neighbors { get; set; } = [];
    public Dictionary<string, Biome> Biomes { get; set; } = [];
    [JsonIgnore]
    public Dictionary<string, Content> Content { get; set; } = [];
    [JsonIgnore]
    public Map MapType { get; set; }
    [JsonIgnore]
    public float Weight { get; set; }
    [JsonIgnore]
    public Dictionary<string, Effect> Effects { get; set; } = [];
    [JsonIgnore]
    public bool DrawTowers { get; set; }

    public long Address { get; set; }
    public long ParentAddress { get; set; }

    [JsonIgnore]
    public string[] EffectText => [.. Effects.Select(x => x.Value.ToString())];

    [JsonIgnore]
    public AtlasNodeDescription MapNode { get; set; }

    public bool MatchID(string id) {
        return MapType.MatchID(id);
    }
    public Waypoint ToWaypoint() {
        return new Waypoint {
            Name = Name,
            ID = Id,
            Address = Address,
            Coordinates = Coordinates,
            Show = true
        };
    }

    public void RecalculateWeight() {
        if (IsVisited || (IsVisited && !IsUnlocked)) {
            Weight = 500;
            return;
        }
        
        Weight = MapType.Weight;
        
        foreach (var effect in Effects.Values) {
            effect.RecalculateWeight();
            Weight += effect.Weight;
        }

        foreach (var content in Content) 
            Weight += content.Value.Weight;
        

        foreach (var biome in Biomes) 
            Weight += biome.Value.Weight;
        
    }
        
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Name: {Name}");
        sb.AppendLine($"Id: {Id}");
        sb.AppendLine($"Address: {Address}");
        sb.AppendLine($"IsVisited: {IsVisited}");
        sb.AppendLine($"IsUnlocked: {IsUnlocked}");
        sb.AppendLine($"IsVisible: {IsVisible}");
        sb.AppendLine($"IsActive: {IsActive}");
        sb.AppendLine($"IsWaypoint: {IsWaypoint}");
        sb.AppendLine($"Coordinate: {Coordinates}");
        sb.AppendLine($"Weight: {Weight}");
        sb.AppendLine($"Neighbors: {NeighborCoordinates.Item1}, {NeighborCoordinates.Item2}, {NeighborCoordinates.Item3}, {NeighborCoordinates.Item4}");
        sb.AppendLine($"Biomes: {string.Join(", ", Biomes.Where(x => x.Value != null).Select(x => x.Value.Name))}");
        sb.AppendLine($"Content: {string.Join(", ", Content.Select(x => x.Value.Name))}");
        sb.AppendLine($"Effects: {string.Join(", ", Effects.Select(x => x.Value.ToString()))}");
        
        return sb.ToString();
    }

    public string DebugText() {
        
        StringBuilder sb = new();
        sb.AppendLine($"Id: {Id}"); 
        sb.AppendLine($"ParentAddress: {ParentAddress:X}");
        sb.AppendLine($"Weight: {Weight}");
        sb.AppendLine($"Coordinates: {Coordinates}");
        sb.AppendLine($"Biomes: {string.Join(", ", Biomes.Where(x => x.Value != null).Select(x => x.Value.Name))}");
        sb.AppendLine($"Content: {string.Join(", ", Content.Select(x => x.Value.Name))}");

        var towers = Effects.SelectMany(x => x.Value.Sources).Distinct().Count();
        if (towers > 0) {
            sb.AppendLine($"Towers: {towers}");
            var effects = Effects.Distinct().Count();
            if (effects > 0)
                sb.AppendLine($"Effects: {effects}");
        }

        return sb.ToString();
    }

    // Use regex to match EffectText
    public bool MatchEffect(string regex) {
        return EffectText.Any(x => Regex.IsMatch(x, regex, RegexOptions.IgnoreCase));
    }

    public List<Vector2i> GetNeighborCoordinates() {
        return [NeighborCoordinates.Item1, NeighborCoordinates.Item2, NeighborCoordinates.Item3, NeighborCoordinates.Item4];
    }
}

using System.Collections.Generic;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using System.Text;
using GameOffsets2.Native;
using System.ComponentModel;
using System.Linq;
using static ExileMaps.ExileMapsCore;

namespace ExileMaps.Classes;

public class Node
{
    public bool IsUnlocked;
    public bool IsVisible;
    public bool IsActive;
    public bool IsVisited;
    public bool IsWaypoint;
    public bool IsFailed => !IsUnlocked && IsVisited;
    public bool IsAttempted => !IsUnlocked && IsVisited;
    public (Vector2i, Vector2i, Vector2i, Vector2i) NeighborCoordinates;
    public Vector2i Coordinates;
    public Dictionary<Vector2i, Node> Neighbors = [];
    public Dictionary<string, Biome> Biomes = [];
    public Dictionary<string, Content> Content = [];
    public Map MapType { get; set; }
    public float Weight;
    public Dictionary<string, Effect> Effects = [];
    public bool DrawTowers { get; set; }
    public long Address { get; set; }
    public long ParentAddress { get; set; }
    public string Name { get; set; }
    public string Id { get; set; }

    public AtlasNodeDescription MapNode { get; set; }

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
        sb.AppendLine($"Address: {ParentAddress.ToString()}");
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

    public List<Vector2i> GetNeighborCoordinates() {
        return [NeighborCoordinates.Item1, NeighborCoordinates.Item2, NeighborCoordinates.Item3, NeighborCoordinates.Item4];
    }
}

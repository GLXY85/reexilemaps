using System;
using System.Drawing;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using ImGuiNET;
using System.Numerics;
using System.Drawing;
using ExileMaps.Classes;

using static ExileMaps.ExileMapsCore;

namespace ExileMaps;

public class ExileMapsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Toggle Features")]
    public FeatureSettings Features { get; set; } = new FeatureSettings();
    
    // [Menu("Pathfinding")]
    // public PathfindingSettings Pathfinding { get; set; } = new PathfindingSettings();

    [Menu("Map Node Labelling")]
    public LabelSettings Labels { get; set; } = new LabelSettings();

    [Menu("Map Content Highlighting")]
    public HighlightSettings Highlights { get; set; } = new HighlightSettings();

    [Menu("Graphics, Colors, and Performance Settings")]    
    public GraphicSettings Graphics { get; set; } = new GraphicSettings();

    [Menu("Map Settings")]
    public MapSettings Maps { get; set; } = new MapSettings();

    [Menu("Biome Settings")]
    public BiomeSettings Biomes { get; set; } = new BiomeSettings();

    [Menu("Content Settings")]
    public ContentSettings MapContent { get; set; } = new ContentSettings();
}

[Submenu(CollapsedByDefault = false)]
public class FeatureSettings
{
    [Menu("Atlas Range", "Range (from your current viewpoint) to process atlas nodes.")]
    public RangeNode<int> AtlasRange { get; set; } = new(2000, 100, 10000);
    [Menu("Use Atlas Range for Node Connections", "Drawing node connections is performance intensive. By default it uses a range of 1000, but you can change it to use the Atlas range.")]
    public ToggleNode UseAtlasRange { get; set; } = new ToggleNode(false);

    [Menu("Process Unlocked Map Nodes")]
    public ToggleNode ProcessUnlockedNodes { get; set; } = new ToggleNode(true);

    [Menu("Process Locked Map Nodes")]
    public ToggleNode ProcessLockedNodes { get; set; } = new ToggleNode(true);

    [Menu("Draw Connections for Visible Map Nodes")]
    public ToggleNode DrawVisibleNodeConnections { get; set; } = new ToggleNode(true);
    
    [Menu("Process Hidden Map Nodes")]
    public ToggleNode ProcessHiddenNodes { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(ProcessHiddenNodes), true)]
    [Menu("Draw Connections for Hidden Map Nodes")]
    public ToggleNode DrawHiddenNodeConnections { get; set; } = new ToggleNode(true);

    [Menu("[NYI] Map Node Highlighting", "Draw colored circles for selected map types.")]
    public ToggleNode DrawNodeHighlights { get; set; } = new ToggleNode(true);

    [Menu("Map Content Highlighting", "Draw colored rings for map content.")]
    public ToggleNode DrawContentRings { get; set; } = new ToggleNode(true);

    [Menu("Draw Labels on Nodes", "Draw the name of map nodes on top of the node.")]
    public ToggleNode DrawNodeLabels { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(DrawNodeLabels), true)]
    [Menu("Map Name Highlighting", "Use custom text and background colors for selected map types.")]
    public ToggleNode NameHighlighting { get; set; } = new ToggleNode(true);

    [Menu("Draw Waypoint Lines", "Draw a line from your current screen position to selected map nodes.")]
    public ToggleNode DrawLines { get; set; } = new ToggleNode(true);
    
    [ConditionalDisplay(nameof(DrawLines), true)]
    [Menu("Limit Waypoints to Atlas range", "If enabled, Waypoints will only be drawn if they are within your Atlas range, otherwise all waypoints will be drawn. Disabling this may cause performance issues.")]
    public ToggleNode WaypointsUseAtlasRange { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(DrawLines), true)]
    [Menu("Draw Labels on Waypoint Lines", "Draw the name and distance to the node on the indicator lines, if enabled")]
    public ToggleNode DrawLineLabels { get; set; } = new ToggleNode(true);

    // [Menu("[NYI] Draw Tower Range", "Draw a ring around towers to indicate their range.")]
    // public ToggleNode DrawTowerRange { get; set; } = new ToggleNode(true);
    
    // [ConditionalDisplay(nameof(DrawTowerRange), true)]
    // [Menu("Draw Solid Circles for Tower Range")]
    // public ToggleNode DrawSolidTowerCircles { get; set; } = new ToggleNode(false);

    // [ConditionalDisplay(nameof(DrawTowerRange), true)]
    // [Menu("Draw Inactive Towers")]
    // public ToggleNode DrawInactiveTowers { get; set; } = new ToggleNode(true);

    // [ConditionalDisplay(nameof(DrawTowerRange), true)]
    // [Menu("Tower Range", "Tower effect range (shouldn't need to change this.)")]
    // public RangeNode<int> TowerEffectRange { get; set; } = new(500, 50, 2000);

    // [ConditionalDisplay(nameof(DrawTowerRange), true)]
    // [Menu("Tower Range Color", "Color of the tower range ring or circle on the Atlas")]
    // public ColorNode TowerColor { get; set; } = new ColorNode(Color.Orange);

    // [ConditionalDisplay(nameof(DrawTowerRange), true)]
    // [Menu("Tower Ring Width", "Tower ring width (if not using filled circle)")]
    // public RangeNode<int> TowerRingWidth { get; set; } = new(12, 1, 48);

    [Menu("Debug Mode", "Show node addresses on Atlas map")]
    public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
}
[Submenu(CollapsedByDefault = false)]
public class PathfindingSettings
{
    public HotkeyNode RefreshNodeCacheHotkey { get; set; } = new HotkeyNode(Keys.F13);
    public HotkeyNode SetCurrentLocationHotkey { get; set; } = new HotkeyNode(Keys.F13);
    public HotkeyNode AddWaypointHotkey { get; set; } = new HotkeyNode(Keys.F13);

}

[Submenu(CollapsedByDefault = false)]
public class LabelSettings
{
    [Menu("Label Unlocked Map Nodes")]
    public ToggleNode LabelUnlockedNodes { get; set; } = new ToggleNode(true);

    [Menu("Label Locked Map Nodes")]
    public ToggleNode LabelLockedNodes { get; set; } = new ToggleNode(true);
    
    [Menu("Label Hidden Map Nodes")]
    public ToggleNode LabelHiddenNodes { get; set; } = new ToggleNode(true);
}

[Submenu(CollapsedByDefault = true)]
public class HighlightSettings
{
    [Menu("Highlight Content in Unlocked Map Nodes")]
    public ToggleNode HighlightUnlockedNodes { get; set; } = new ToggleNode(true);

    [Menu("Highlight Content in Locked Map Nodes")]
    public ToggleNode HighlightLockedNodes { get; set; } = new ToggleNode(true);
    
    [Menu("Highlight Content in Hidden Map Nodes")]
    public ToggleNode HighlightHiddenNodes { get; set; } = new ToggleNode(true);

    [Menu("Highlight Breaches", "Highlight breaches with a ring on the Atlas")]
    public ToggleNode HighlightBreaches { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(HighlightBreaches), true)]
    [Menu("Breach Color", "Color of the ring around breaches on the Atlas")]
    public ColorNode breachColor { get; set; } = new ColorNode(Color.FromArgb(200, 143, 82, 246));
    
    [Menu("Highlight Delirium", "Highlight delirium with a ring on the Atlas")]
    public ToggleNode HighlightDelirium { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(HighlightDelirium), true)]
    [Menu("Delirium Color", "Color of the ring around delirium on the Atlas")]
    public ColorNode deliriumColor { get; set; } = new ColorNode(Color.FromArgb(200, 200, 200, 200));

    [Menu("Highlight Expedition", "Highlight expeditions with a ring on the Atlas")]
    public ToggleNode HighlightExpedition { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(HighlightExpedition), true)]
    [Menu("Expedition Color", "Color of the ring around expeditions on the Atlas")]
    public ColorNode expeditionColor { get; set; } = new ColorNode(Color.FromArgb(200, 101, 129, 172));

    [Menu("Highlight Rituals", "Highlight rituals with a ring on the Atlas")]
    public ToggleNode HighlightRitual { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(HighlightRitual), true)]
    [Menu("Ritual Color", "Color of the ring around rituals on the Atlas")]
    public ColorNode ritualColor { get; set; } = new ColorNode(Color.FromArgb(200, 252, 3, 3));
    [Menu("Highlight Bosses", "Highlight rituals with a ring on the Atlas")]
    public ToggleNode HighlightBosses { get; set; } = new ToggleNode(true);   

    [ConditionalDisplay(nameof(HighlightBosses), true)]
    [Menu("Boss Color", "Color of the ring around bosses on the Atlas")]
    public ColorNode bossColor { get; set; } = new ColorNode(Color.FromArgb(200, 195, 156, 105));

}

[Submenu(CollapsedByDefault = false)]
public class GraphicSettings
{
    [Menu("Render every N ticks", "Throttle the renderer to only re-render every Nth tick - can improve performance.")]
    public RangeNode<int> RenderNTicks { get; set; } = new RangeNode<int>(10, 1, 20);

    [Menu("Font Color", "Color of the text on the Atlas")]
    public ColorNode FontColor { get; set; } = new ColorNode(Color.White);

    [Menu("Background Color", "Color of the background on the Atlas")]
    public ColorNode BackgroundColor { get; set; } = new ColorNode(Color.FromArgb(100, 0, 0, 0));
    
    [Menu("Distance Marker Scale", "Interpolation factor for distance markers on lines")]
    public RangeNode<float> LabelInterpolationScale { get; set; } = new RangeNode<float>(0.2f, 0, 1);

    [Menu("Line Color", "Color of the map connection lines and waypoint lines when no map specific color is set")]
    public ColorNode LineColor { get; set; } = new ColorNode(Color.FromArgb(200, 255, 222, 222));

    [Menu("Line Width", "Width of the map connection lines and waypoint lines")]
    public RangeNode<float> MapLineWidth { get; set; } = new RangeNode<float>(4.0f, 0, 10);

    [Menu("Unlocked Line Color", "Color of the map connection lines when an adjacent node is unlocked.")]
    public ColorNode UnlockedLineColor { get; set; } = new ColorNode(Color.FromArgb(170, 90, 255, 90));

    [Menu("Locked Line Color", "Color of the map connection lines when no adjacent nodes are unlocked.")]
    public ColorNode LockedLineColor { get; set; } = new ColorNode(Color.FromArgb(170, 255, 90, 90));

}

[Submenu(CollapsedByDefault = true)]
public class MapSettings
{
    [JsonIgnore]
    public CustomNode CustomMapSettings { get; set; }

    public Dictionary<string, Map> Maps { get; set; }
    public MapSettings() {    

        CustomMapSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
                
                var updatingMaps = false;

                if (ImGui.Button("Update Maps") && !updatingMaps) {
                    var WorldMap = Main.Game.IngameState.IngameUi.WorldMap.AtlasPanel;
                    var screenCenter = Main.Game.Window.GetWindowRectangle().Center;
                    // if WorldMap isn't open, return
                    if (WorldMap == null) return;
                    
                    updatingMaps = true;
                    try
                    {
                        Main.LoadDefaultMaps();

                        var mapNodes = WorldMap.Descriptions                                
                            .GroupBy(x => x.Element.Area.Name)
                            .Select(g => g.First())
                            .ToList();

                        foreach (var mapNode in mapNodes)
                        {

                            var mapName = mapNode.Element.Area.Name.Trim();

                            var mapId = mapNode.Element.Area.Name.ToString().Replace(" ", "");
                            
                            if (Maps.ContainsKey(mapId)) {                              
                                Maps[mapId].Name = mapName;
                                Maps[mapId].RealID = mapNode.Element.Area.Id;                                    
                                Maps[mapId].Count = WorldMap.Descriptions.Count(x => x.Element.Area.Name.Trim() == mapName);
                            } else {
                                var map = new Map
                                {
                                    Name = mapName,
                                    ID = mapName.Replace(" ", ""),
                                    RealID = mapNode.Element.Area.Id,
                                    NameColor = Color.White,
                                    BackgroundColor = Color.FromArgb(100, 0, 0, 0),
                                    NodeColor = Color.White,
                                    DrawLine = false,
                                    Highlight = false,                                    
                                    Count = WorldMap.Descriptions.Count(x => x.Element.Area.Name == mapName)
                                };
                                Maps.Add(mapId, map);
                            }
                            
                            Main.LogMessage($"Added {mapName} to map settings");
                        
                        }

                        
                    }
                    catch (Exception ex)
                    {
                        Main.LogMessage($"Failed to refresh Atlas: Error finding GameState - Reloading the plugin should fix this.");
                    }
                    finally
                    {
                        updatingMaps = false;
                    }
                } else if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Add any/all new maps to the map list and update map counts. Atlas map must be open.");
                }
                
                if (Maps.Count == 0)   
                    Main.LoadDefaultMaps();

                ImGui.Spacing();
                ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
                ImGui.Spacing();

                if (ImGui.BeginTable("maps_table", 8, ImGuiTableFlags.SizingFixedFit|ImGuiTableFlags.Borders|ImGuiTableFlags.PadOuterX))
                {
  
                    ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed, 200);                                                              
                    ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100); 
                    ImGui.TableSetupColumn("Node", ImGuiTableColumnFlags.WidthFixed, 55);     
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 55);               
                    ImGui.TableSetupColumn("Background", ImGuiTableColumnFlags.WidthFixed, 55);
                    ImGui.TableSetupColumn("Line", ImGuiTableColumnFlags.WidthFixed, 55);                              
                    ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 55);
                    ImGui.TableSetupColumn("Biomes", ImGuiTableColumnFlags.WidthStretch, 300);   
                    ImGui.TableHeadersRow();

                    // Sort Maps alphabetically by Name
                    Maps = Maps.OrderBy(x => x.Value.Name).ToDictionary(x => x.Key, x => x.Value);

                    foreach (var map in Maps)
                    {
                        ImGui.PushID($"Map_{map.Key}");
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        bool isMapHighlighted = map.Value.Highlight;

                        if(ImGui.Checkbox($"##{map}_highlight", ref isMapHighlighted))
                            map.Value.Highlight = isMapHighlighted;

                        ImGui.SameLine();
                        ImGui.Text(map.Value.Name);

                        ImGui.TableNextColumn();
                        float weight = map.Value.Weight;
                        ImGui.SetNextItemWidth(100);
                        if(ImGui.SliderFloat($"##{map}_weight", ref weight, 0.1f, 5.0f))                        
                            map.Value.Weight = weight;

                        ImGui.TableNextColumn();

                        float controlWidth = 30.0f;
                        float availableWidth = ImGui.GetContentRegionAvail().X;
                        float cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                        ImGui.SetCursorPosX(cursorPosX);
                        Color nodeColor = map.Value.NodeColor;
                        Vector4 colorVector = new Vector4(nodeColor.R / 255.0f, nodeColor.G / 255.0f, nodeColor.B / 255.0f, nodeColor.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{map}_nodecolor", ref colorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                            map.Value.NodeColor = Color.FromArgb((int)(colorVector.W * 255), (int)(colorVector.X * 255), (int)(colorVector.Y * 255), (int)(colorVector.Z * 255));
                        
                        ImGui.TableNextColumn();
                        availableWidth = ImGui.GetContentRegionAvail().X;
                        cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                        ImGui.SetCursorPosX(cursorPosX);
                        Color nameColor = map.Value.NameColor;
                        Vector4 nameColorVector = new Vector4(nameColor.R / 255.0f, nameColor.G / 255.0f, nameColor.B / 255.0f, nameColor.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{map}_namecolor", ref nameColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                            map.Value.NameColor = Color.FromArgb((int)(nameColorVector.W * 255), (int)(nameColorVector.X * 255), (int)(nameColorVector.Y * 255), (int)(nameColorVector.Z * 255));
                        
                        ImGui.TableNextColumn();
                        availableWidth = ImGui.GetContentRegionAvail().X;
                        cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                        ImGui.SetCursorPosX(cursorPosX);
                        Color bgColor = map.Value.BackgroundColor;
                        Vector4 bgColorVector = new Vector4(bgColor.R / 255.0f, bgColor.G / 255.0f, bgColor.B / 255.0f, bgColor.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{map}_bgcolor", ref bgColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                            map.Value.BackgroundColor = Color.FromArgb((int)(bgColorVector.W * 255), (int)(bgColorVector.X * 255), (int)(bgColorVector.Y * 255), (int)(bgColorVector.Z * 255));
                        
                        ImGui.TableNextColumn();
                        availableWidth = ImGui.GetContentRegionAvail().X;
                        cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                        ImGui.SetCursorPosX(cursorPosX);
                        bool drawLine = map.Value.DrawLine;
                        if(ImGui.Checkbox($"##{map}_line", ref drawLine))
                            map.Value.DrawLine = drawLine;    

                        ImGui.TableNextColumn();                        
                        ImGui.Text(map.Value.Count.ToString());
                            
                        ImGui.TableNextColumn();
                        if (map.Value.Biomes == null)
                            continue;
                            
                        string[] biomes = map.Value.Biomes.Where(x => x != "").ToArray();
                        ImGui.Text(biomes.Length > 0 ? string.Join(", ", biomes) : "None");

                        ImGui.PopID();
                    }                
                }
                ImGui.EndTable();
                
            }
        };
    }
}

[Submenu(CollapsedByDefault = true)]
public class BiomeSettings
{
    [JsonIgnore]
    public CustomNode CustomBiomeSettings { get; set; }
    public Dictionary<string, Biome> Biomes { get; set; }
    public BiomeSettings() {    

        CustomBiomeSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.2f, 0.2f, 1));
                ImGui.TextWrapped("These settings are not yet implemented.");
                ImGui.PopStyleColor();

                ImGui.Spacing();
                ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
                ImGui.Spacing();

                if (ImGui.BeginTable("biomes_table", 5, ImGuiTableFlags.Borders|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Biome", ImGuiTableColumnFlags.WidthFixed, 200);                                                               
                    ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);     
                    ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("Highlight", ImGuiTableColumnFlags.WidthFixed, 70); 
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 50);
                    ImGui.TableHeadersRow();

                    foreach (var biome in Biomes)
                    {
                        ImGui.PushID($"Biome_{biome.Key}");

                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.Text(biome.Key);

                        ImGui.TableNextColumn();
                        float weight = biome.Value.Weight;                        
                        ImGui.SetNextItemWidth(100);
                        if(ImGui.SliderFloat($"##{biome}_weight", ref weight, 0.1f, 0.5f))                        
                            biome.Value.Weight = weight;
                        
                        ImGui.TableNextColumn();
                        float controlWidth = 30.0f;
                        float availableWidth = ImGui.GetContentRegionAvail().X;
                        float cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                        ImGui.SetCursorPosX(cursorPosX);
                        Color biomeColor = biome.Value.Color;
                        Vector4 biomeColorVector = new Vector4(biomeColor.R / 255.0f, biomeColor.G / 255.0f, biomeColor.B / 255.0f, biomeColor.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{biome}_color", ref biomeColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                            biome.Value.Color = Color.FromArgb((int)(biomeColorVector.W * 255), (int)(biomeColorVector.X * 255), (int)(biomeColorVector.Y * 255), (int)(biomeColorVector.Z * 255));
                        
                        ImGui.TableNextColumn();
                        availableWidth = ImGui.GetContentRegionAvail().X;
                        cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                        ImGui.SetCursorPosX(cursorPosX);
                        bool highlight = biome.Value.Highlight;
                        if(ImGui.Checkbox($"##{biome}_highlight", ref highlight))                        
                            biome.Value.Highlight = highlight;
                        
                        ImGui.PopID();
                    }
                }
                ImGui.EndTable();
            }
        };
    }
}

[Submenu(CollapsedByDefault = true)]
public class ContentSettings
{
    [JsonIgnore]
    public CustomNode CustomContentSettings { get; set; }
    public Dictionary<string, Content> ContentTypes { get; set; }

    public bool HighlightUnlockedNodes { get; set; } = true;
    public bool HighlightLockedNodes { get; set; } = true;
    public bool HighlightHiddenNodes { get; set; } = true;


    public ContentSettings() {    

        CustomContentSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
  
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.2f, 0.2f, 1));
                ImGui.TextWrapped("These settings are not yet implemented.");
                ImGui.PopStyleColor();

                if (ImGui.BeginTable("content_options_table", 2, ImGuiTableFlags.NoBordersInBody|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 40);                                                               
                    ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthStretch, 300);                     
        
                    ImGui.TableNextRow();


                    ImGui.TableNextColumn();
                    bool highlightUnlocked = HighlightUnlockedNodes;
                    if(ImGui.Checkbox($"##unlocked_nodes_highlight", ref highlightUnlocked))                        
                        HighlightUnlockedNodes = highlightUnlocked;

                    ImGui.TableNextColumn();
                    ImGui.Text("Highlight Content in Unlocked Map Nodes");

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool highlightLocked = HighlightLockedNodes;
                    if(ImGui.Checkbox($"##locked_nodes_highlight", ref highlightLocked))                        
                        HighlightLockedNodes = highlightLocked;

                    ImGui.TableNextColumn();
                    ImGui.Text("Highlight Content in Locked Map Nodes");

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool highlightHidden = HighlightHiddenNodes;
                    if(ImGui.Checkbox($"##hidden_nodes_highlight", ref highlightHidden))                        
                        HighlightHiddenNodes = highlightHidden;

                    ImGui.TableNextColumn();
                    ImGui.Text("Highlight Content in Hidden Map Nodes");                    
                }

                ImGui.EndTable();

                ImGui.Spacing();
                ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
                ImGui.Spacing();

                if (ImGui.BeginTable("content_table", 5, ImGuiTableFlags.Borders))
                {
                    ImGui.TableSetupColumn("Content Type", ImGuiTableColumnFlags.WidthFixed, 200);                                                               
                    ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);     
                    ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("Highlight", ImGuiTableColumnFlags.WidthFixed, 70); 
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 50);
                    ImGui.TableHeadersRow();

                    foreach (var content in ContentTypes)
                    {
                        ImGui.PushID($"Content_{content.Key}");
                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.Text(content.Key);

                        ImGui.TableNextColumn();
                        float weight = content.Value.Weight;                        
                        ImGui.SetNextItemWidth(100);
                        if(ImGui.SliderFloat($"##{content}_weight", ref weight, 0.1f, 0.5f)) 
                            content.Value.Weight = weight;
                        
                        ImGui.TableNextColumn();
                        float controlWidth = 30.0f;
                        float availableWidth = ImGui.GetContentRegionAvail().X;
                        float cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                        ImGui.SetCursorPosX(cursorPosX);
                        Color contentColor = content.Value.Color;
                        Vector4 contentColorVector = new Vector4(contentColor.R / 255.0f, contentColor.G / 255.0f, contentColor.B / 255.0f, contentColor.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{content}_color", ref contentColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                            content.Value.Color = Color.FromArgb((int)(contentColorVector.W * 255), (int)(contentColorVector.X * 255), (int)(contentColorVector.Y * 255), (int)(contentColorVector.Z * 255));
                        
                        ImGui.TableNextColumn();
                        availableWidth = ImGui.GetContentRegionAvail().X;
                        cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                        ImGui.SetCursorPosX(cursorPosX);
                        bool highlight = content.Value.Highlight;
                        if(ImGui.Checkbox($"##{content}_highlight", ref highlight))                        
                            content.Value.Highlight = highlight;
                        
                        ImGui.PopID();
                    }
                }
                ImGui.EndTable();
            }
        };
    }
}
        
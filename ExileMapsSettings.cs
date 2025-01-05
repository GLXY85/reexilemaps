using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Helpers;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using ExileCore2.Shared.Enums;
using ExileMaps.Classes;
using ImGuiNET;
using Newtonsoft.Json;
using GameOffsets2.Native;

using static ExileMaps.ExileMapsCore;

namespace ExileMaps;

public class ExileMapsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Toggle Features")]
    public FeatureSettings Features { get; set; } = new FeatureSettings();
    
    [Menu("Keybinds")]
    public HotkeySettings Keybinds { get; set; } = new HotkeySettings();

    [Menu("Map Node Labelling")]
    public LabelSettings Labels { get; set; } = new LabelSettings();

    [Menu("Graphics, Colors, and Performance Settings")]    
    public GraphicSettings Graphics { get; set; } = new GraphicSettings();

    [Menu("Map Settings")]
    public MapSettings Maps { get; set; } = new MapSettings();

    [Menu("Biome Settings")]
    public BiomeSettings Biomes { get; set; } = new BiomeSettings();

    [Menu("Content Settings")]
    public ContentSettings MapContent { get; set; } = new ContentSettings();

    [Menu("Atlas Mod Settings")]
    public MapModSettings MapMods { get; set; } = new MapModSettings();

    [Menu("Waypoint Settings")]
    public WaypointSettings Waypoints { get; set; } = new WaypointSettings();

}

[Submenu(CollapsedByDefault = false)]
public class FeatureSettings
{
    [Menu("Atlas Range", "Range (from your current viewpoint) to process atlas nodes.")]
    public RangeNode<int> AtlasRange { get; set; } = new(1500, 100, 20000);
    [Menu("Use Atlas Range for Node Connections", "Drawing node connections is performance intensive. By default it uses a range of 1000, but you can change it to use the Atlas range.")]
    public ToggleNode UseAtlasRange { get; set; } = new ToggleNode(false);

    [Menu("Process Unlocked Map Nodes")]
    public ToggleNode ProcessUnlockedNodes { get; set; } = new ToggleNode(true);

    [Menu("Process Locked Map Nodes")]
    public ToggleNode ProcessLockedNodes { get; set; } = new ToggleNode(true);

    [Menu("Draw Connections for Visited Map Nodes")]
    public ToggleNode DrawVisitedNodeConnections { get; set; } = new ToggleNode(true);

    [Menu("Process Hidden Map Nodes")]
    public ToggleNode ProcessHiddenNodes { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(ProcessHiddenNodes), true)]
    [Menu("Draw Connections for Hidden Map Nodes")]
    public ToggleNode DrawHiddenNodeConnections { get; set; } = new ToggleNode(true);

    [Menu("Draw Waypoint Lines", "Draw a line from your current screen position to selected map nodes.")]
    public ToggleNode DrawLines { get; set; } = new ToggleNode(true);
    
    [ConditionalDisplay(nameof(DrawLines), true)]
    [Menu("Limit Waypoints to Atlas range", "If enabled, Waypoints will only be drawn if they are within your Atlas range, otherwise all waypoints will be drawn. Disabling this may cause performance issues.")]
    public ToggleNode WaypointsUseAtlasRange { get; set; } = new ToggleNode(false);

    [ConditionalDisplay(nameof(DrawLines), true)]
    [Menu("Draw Labels on Waypoint Lines", "Draw the name and distance to the node on the indicator lines, if enabled")]
    public ToggleNode DrawLineLabels { get; set; } = new ToggleNode(true);

    [Menu("Debug Mode")]
    public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
    public HotkeyNode DebugKey { get; set; } = new HotkeyNode(Keys.F13);
}
[Submenu(CollapsedByDefault = false)]
public class HotkeySettings
{
    public HotkeyNode RefreshMapCacheHotkey { get; set; } = new HotkeyNode(Keys.F13);
    public HotkeyNode AddWaypointHotkey { get; set; } = new HotkeyNode(Keys.Oemcomma);
    public HotkeyNode DeleteWaypointHotkey { get; set; } = new HotkeyNode(Keys.OemPeriod);
    public HotkeyNode ToggleWaypointPanelHotkey { get; set; } = new HotkeyNode(Keys.Oem2);


}

[Submenu(CollapsedByDefault = false)]
public class LabelSettings
{
    [Menu("Draw Labels on Nodes", "Draw the name of map nodes on top of the node.")]
    public ToggleNode DrawNodeLabels { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(DrawNodeLabels), true)]
    [Menu("Label Unlocked Map Nodes")]
    public ToggleNode LabelUnlockedNodes { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(DrawNodeLabels), true)]
    [Menu("Label Locked Map Nodes")]
    public ToggleNode LabelLockedNodes { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(DrawNodeLabels), true)]
    [Menu("Label Hidden Map Nodes")]
    public ToggleNode LabelHiddenNodes { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(DrawNodeLabels), true)]
    [Menu("Highlight Map Names", "Use custom text and background colors for selected map types.")]
    public ToggleNode NameHighlighting { get; set; } = new ToggleNode(true);
}

[Submenu(CollapsedByDefault = false)]
public class GraphicSettings
{
    [Menu("Render every N ticks", "Throttle the renderer to only re-render every Nth tick - can improve performance.")]
    public RangeNode<int> RenderNTicks { get; set; } = new RangeNode<int>(5, 1, 20);

    [Menu("Map Cache Refresh Rate", "Throttle the map cache refresh rate. Default is 5 seconds.")]
    public RangeNode<int> MapCacheRefreshRate { get; set; } = new RangeNode<int>(5, 1, 60);

    [Menu("Font Color", "Color of the text on the Atlas")]
    public ColorNode FontColor { get; set; } = new ColorNode(Color.White);

    [Menu("Background Color", "Color of the background on the Atlas")]
    public ColorNode BackgroundColor { get; set; } = new ColorNode(Color.FromArgb(177, 0, 0, 0));
    
    [Menu("Distance Marker Scale", "Interpolation factor for distance markers on lines")]
    public RangeNode<float> LabelInterpolationScale { get; set; } = new RangeNode<float>(0.2f, 0, 1);

    [Menu("Line Color", "Color of the map connection lines and waypoint lines when no map specific color is set")]
    public ColorNode LineColor { get; set; } = new ColorNode(Color.FromArgb(200, 255, 222, 222));

    [Menu("Line Width", "Width of the map connection lines and waypoint lines")]
    public RangeNode<float> MapLineWidth { get; set; } = new RangeNode<float>(4.0f, 0, 10);

    [Menu("Visited Line Color", "Color of the map connection lines when an both nodes are visited.")]
    public ColorNode VisitedLineColor { get; set; } = new ColorNode(Color.FromArgb(80, 255, 255, 255));

    [Menu("Unlocked Line Color", "Color of the map connection lines when an adjacent node is unlocked.")]
    public ColorNode UnlockedLineColor { get; set; } = new ColorNode(Color.FromArgb(170, 90, 255, 90));

    [Menu("Locked Line Color", "Color of the map connection lines when no adjacent nodes are unlocked.")]
    public ColorNode LockedLineColor { get; set; } = new ColorNode(Color.FromArgb(170, 255, 90, 90));

    [Menu("Draw Lines as Gradients", "Draws lines as a gradient between the two colors. Performance intensive.")]
    public ToggleNode DrawGradientLines { get; set; } = new ToggleNode(false);

    [Menu("Content Ring Width", "Width of the rings used to indicate map content")]
    public RangeNode<float> RingWidth { get; set; } = new RangeNode<float>(7.0f, 0, 10);

    [Menu("Content Radius", "Radius of the rings used to indicate map content")]
    public RangeNode<float> RingRadius { get; set; } = new RangeNode<float>(1.25f, 0, 10);
    
    [Menu("Node Radius", "Radius of the circles used to highlight map nodes")]
    public RangeNode<float> NodeRadius { get; set; } = new RangeNode<float>(2.0f, 0, 10);
}

[Submenu(CollapsedByDefault = true)]
public class MapSettings
{
    [JsonIgnore]
    public CustomNode CustomMapSettings { get; set; }
    public bool HighlightMapNodes { get; set; } = true;
    public bool ColorNodesByWeight { get; set; } = true;
    public bool DrawWeightOnMap { get; set; } = false;

    public Color GoodNodeColor { get; set; } = Color.FromArgb(200, 50, 255, 50);
    public Color BadNodeColor { get; set; } = Color.FromArgb(200, 255, 50, 50);
    public ObservableDictionary<string, Map> Maps { get; set; }
    public MapSettings() {    
        CustomMapSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
                var updatingMaps = false;

                if (ImGui.BeginTable("map_options_table", 2, ImGuiTableFlags.NoBordersInBody|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 40);                                                               
                    ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthStretch, 300);                     
        
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool highlightNodes = HighlightMapNodes;
                    if(ImGui.Checkbox($"##map_nodes_highlight", ref highlightNodes))                        
                        HighlightMapNodes = highlightNodes;

                    ImGui.TableNextColumn();
                    ImGui.Text("Highlight Map Nodes");         

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool weightedColors = ColorNodesByWeight;
                    if(ImGui.Checkbox($"##weighted_colors", ref weightedColors))                        
                        ColorNodesByWeight = weightedColors;

                    ImGui.TableNextColumn();
                    
                    ImGui.Text("Color Nodes by Weight");       
                        
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();

                    Color goodColor = GoodNodeColor;
                    Vector4 colorVector = new Vector4(goodColor.R / 255.0f, goodColor.G / 255.0f, goodColor.B / 255.0f, goodColor.A / 255.0f);
                    if(ImGui.ColorEdit4($"##goodgoodcolor", ref colorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        GoodNodeColor = Color.FromArgb((int)(colorVector.W * 255), (int)(colorVector.X * 255), (int)(colorVector.Y * 255), (int)(colorVector.Z * 255));

                    ImGui.TableNextColumn();
                    ImGui.Text("Good Node Color");    

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();

                    Color badColor = BadNodeColor;
                    colorVector = new Vector4(badColor.R / 255.0f, badColor.G / 255.0f, badColor.B / 255.0f, badColor.A / 255.0f);
                    if(ImGui.ColorEdit4($"##goodbadcolor", ref colorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        BadNodeColor = Color.FromArgb((int)(colorVector.W * 255), (int)(colorVector.X * 255), (int)(colorVector.Y * 255), (int)(colorVector.Z * 255));

                    ImGui.TableNextColumn();
                    ImGui.Text("Bad Node Color");    
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool drawWeight = DrawWeightOnMap;
                    if(ImGui.Checkbox($"##draw_weight", ref drawWeight))                        
                        DrawWeightOnMap = drawWeight;

                    ImGui.TableNextColumn();
                    
                    ImGui.TextUnformatted("Draw Weight % on Map");  
                }

                ImGui.EndTable();
                
                ImGui.Spacing();
                if (ImGui.Button("Update Map Counts") && !updatingMaps) {
                    if (Main.GameController.Game.IngameState.IngameUi.WorldMap == null) return;
                    var WorldMap = Main.GameController.Game.IngameState.IngameUi.WorldMap.AtlasPanel;
                    var screenCenter = Main.Game.Window.GetWindowRectangle().Center;
                    // if WorldMap isn't open, return
                    if (WorldMap == null || !WorldMap.IsVisible) return;
                    
                    updatingMaps = true;

                    if (Maps == null || Maps.Count == 0)
                        Main.LoadDefaultMaps();

                    Dictionary<long, Node> mapNodes = Main.mapCache
                        .GroupBy(x => x.Value.Name)
                        .Select(g => g.First())
                        .ToDictionary();

                    foreach (var (key,mapNode) in mapNodes)
                    {
                        var mapName = mapNode.Name.Trim();

                        var mapId = mapNode.Name.ToString().Replace(" ", "");
                        
                        if (Maps.ContainsKey(mapId)) {                              
                            Maps[mapId].Name = mapName;
                            Maps[mapId].RealID = mapNode.Id;                                    
                            Maps[mapId].Count = Main.mapCache.Count(x => x.Value.Name.Trim() == mapName && !x.Value.IsVisited && x.Value.IsUnlocked);
                            Maps[mapId].LockedCount = Main.mapCache.Count(x => x.Value.Name.Trim() == mapName && !x.Value.IsVisited && !x.Value.IsUnlocked && x.Value.IsVisible);
                            Maps[mapId].FogCount = Main.mapCache.Count(x => x.Value.Name.Trim() == mapName && !x.Value.IsVisited && !x.Value.IsVisible);
                        } else {
                            var map = new Map
                            {
                                Name = mapName,
                                ID = mapName.Replace(" ", ""),
                                RealID = mapNode.Id,
                                NameColor = Color.White,
                                BackgroundColor = Color.FromArgb(200, 0, 0, 0),
                                NodeColor = Color.White,
                                DrawLine = false,
                                Highlight = false,                                    
                                Count = Main.mapCache.Count(x => x.Value.Name.Trim() == mapName && !x.Value.IsVisited && x.Value.IsUnlocked),
                                LockedCount = Main.mapCache.Count(x => x.Value.Name.Trim() == mapName && !x.Value.IsVisited && !x.Value.IsUnlocked && x.Value.IsVisible),
                                FogCount = Main.mapCache.Count(x => x.Value.Name.Trim() == mapName && !x.Value.IsVisited && !x.Value.IsVisible),
                            };
                            Maps.Add(mapId, map);
                        }                        
                    }                    
        
                    updatingMaps = false;
                    
                } else if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Add any/all new maps to the map list and update map counts. Atlas map must be open.");
                }
                
                if (Maps.Count == 0)   
                    Main.LoadDefaultMaps();

                ImGui.Spacing();
                ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
                ImGui.Spacing();
                if (ImGui.TreeNodeEx("Map Table", ImGuiTreeNodeFlags.DefaultOpen|ImGuiTreeNodeFlags.SpanFullWidth)) {
                    {

                    if (ImGui.BeginTable("maps_table", 10, ImGuiTableFlags.SizingFixedFit|ImGuiTableFlags.Borders|ImGuiTableFlags.PadOuterX))
                    {
    
                        ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed, 250);                                                              
                        ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100); 
                        ImGui.TableSetupColumn("Node", ImGuiTableColumnFlags.WidthFixed, 30);     
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 30);               
                        ImGui.TableSetupColumn("BG", ImGuiTableColumnFlags.WidthFixed, 30);
                        ImGui.TableSetupColumn("Line", ImGuiTableColumnFlags.WidthFixed, 30);                              
                        ImGui.TableSetupColumn("Unlocked", ImGuiTableColumnFlags.WidthFixed, 45);
                        ImGui.TableSetupColumn("Locked", ImGuiTableColumnFlags.WidthFixed, 45);
                        ImGui.TableSetupColumn("Hidden", ImGuiTableColumnFlags.WidthFixed, 45);
                        ImGui.TableSetupColumn("Biomes", ImGuiTableColumnFlags.WidthStretch, 200);   
                        ImGui.TableHeadersRow();

                        // Sort Maps alphabetically by Name
                        Maps = Maps.OrderBy(x => x.Value.Name).ToObservableDictionary();

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
                            if(ImGui.SliderFloat($"##{map}_weight", ref weight, -25.0f, 25.0f, "%.1f"))                        
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
                            ImGui.Text(map.Value.LockedCount.ToString());

                            ImGui.TableNextColumn();                        
                            ImGui.Text(map.Value.FogCount.ToString());
                                
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
                }
            }
        };
    }
}
[Submenu(CollapsedByDefault = true)]
public class BiomeSettings
{
    [JsonIgnore]
    public CustomNode CustomBiomeSettings { get; set; }
    public bool ShowBiomes { get; set; } = true;
    public ObservableDictionary<string, Biome> Biomes { get; set; }
    public BiomeSettings() {    

        CustomBiomeSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
                ImGui.Spacing();
                ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
                ImGui.Spacing();

                if (ImGui.BeginTable("biomes_table", 5, ImGuiTableFlags.Borders|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Biome", ImGuiTableColumnFlags.WidthFixed, 250);                                                               
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
                        if(ImGui.SliderFloat($"##{biome}_weight", ref weight, -5.0f, 5.0f, "%.2f"))                        
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
    public ObservableDictionary<string, Content> ContentTypes { get; set; }

    public bool HighlightUnlockedNodes { get; set; } = true;
    public bool HighlightLockedNodes { get; set; } = true;
    public bool HighlightHiddenNodes { get; set; } = true;


    public ContentSettings() {    

        CustomContentSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
  
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

                if (ImGui.BeginTable("content_table", 6, ImGuiTableFlags.Borders))
                {
                    ImGui.TableSetupColumn("Content Type", ImGuiTableColumnFlags.WidthFixed, 250);                                                               
                    ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);     
                    ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("Highlight", ImGuiTableColumnFlags.WidthFixed, 70); 
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 50);
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
                        if(ImGui.SliderFloat($"##{content}_weight", ref weight, -5.0f, 5.0f, "%.2f")) 
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
                        
                        ImGui.TableNextColumn();

                        // draw the image
                        ImGui.TableNextColumn();


                        


                        ImGui.PopID();
                    }
                }
                ImGui.EndTable();
            }
        };
    }
}


[Submenu(CollapsedByDefault = true)]
public class MapModSettings
{
    [JsonIgnore]
    public CustomNode ModSettings { get; set; }
    public ObservableDictionary<string, Mod> MapModTypes { get; set; }
    public bool ShowOnTowers { get; set; } = true;
    public bool ShowOnMaps { get; set; } = true;
    public bool OnlyDrawApplicableMods { get; set; } = true;
    public float MapModScale { get; set; } = 0.75f;
    public int MapModOffset { get; set; } = 25;

    public MapModSettings() {    

        ModSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.BeginTable("mod_options_table", 2, ImGuiTableFlags.NoBordersInBody|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 60);                                                               
                    ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthStretch, 300);                     
        
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool showOnTowers = ShowOnTowers;
                    if(ImGui.Checkbox($"##show_on_towers", ref showOnTowers))                        
                        ShowOnTowers = showOnTowers;

                    ImGui.TableNextColumn();
                    ImGui.Text("Display Tower Mods on Towers");

                    ImGui.TableNextRow();


                    ImGui.TableNextColumn();
                    bool showOnMaps = ShowOnMaps;
                    if(ImGui.Checkbox($"##show_on_maps", ref showOnMaps))                        
                        ShowOnMaps = showOnMaps;

                    ImGui.TableNextColumn();
                    ImGui.Text("Display Tower Mods on Maps");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    bool onlyApplicable = OnlyDrawApplicableMods;

                    if(ImGui.Checkbox($"##draw_applicable", ref onlyApplicable)) {                        
                        OnlyDrawApplicableMods = onlyApplicable;
                        if (Main != null)
                            Main.refreshCache = true;
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text("Only Draw Mods that Apply (e.g. no breach mods on non-breach maps)");

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    float scale = MapModScale;                        
                    ImGui.SetNextItemWidth(60);
                    if(ImGui.SliderFloat($"##mapmodscale", ref scale, 0.5f, 2.0f, "%.1f")) 
                        MapModScale = scale;

                    ImGui.TableNextColumn();
                    ImGui.Text("Map Mod Text Scale");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();

                    int offset = MapModOffset;                        
                    ImGui.SetNextItemWidth(60);
                    if(ImGui.SliderInt($"##mapmodoffset", ref offset, 10, 50)) 
                        MapModOffset = offset;

                    ImGui.TableNextColumn();
                    ImGui.Text("Map Mod Text Offset");
                
                }

                ImGui.EndTable();

                ImGui.Spacing();
                ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
                ImGui.TextWrapped("NOTE: All mod weights are multiplied by the mod value.");
                ImGui.Spacing();

                try {
                    if (ImGui.BeginTable("mod_table", 5, ImGuiTableFlags.Borders))
                    {
                        ImGui.TableSetupColumn("Mod Type", ImGuiTableColumnFlags.WidthFixed, 250);                                                               
                        ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);     
                        ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 50);
                        ImGui.TableSetupColumn("Show", ImGuiTableColumnFlags.WidthFixed, 50);
                        ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch, 300);
                        ImGui.TableHeadersRow();
                        foreach (var mod in MapModTypes)
                        {
                            ImGui.PushID($"Mod_{mod.Key}");
                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            // Add a space between each word in ModID: Example TowerDeliriumChance should become Tower Delirium Chance
                            string modID = mod.Value.ModID.Replace("Tower","");
                            modID = string.Concat(modID.Select(x => Char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');                        
                            ImGui.TextUnformatted(modID);

                            ImGui.TableNextColumn();
                            float weight = mod.Value.Weight;                        
                            ImGui.SetNextItemWidth(100);
                            if(ImGui.SliderFloat($"##{mod}_weight", ref weight, -1.00f, 5.00f, "%.3f")) 
                                mod.Value.Weight = weight;
                            
                            ImGui.TableNextColumn();
                            float controlWidth = 30.0f;
                            float availableWidth = ImGui.GetContentRegionAvail().X;
                            float cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                            ImGui.SetCursorPosX(cursorPosX);
                            Color modColor = mod.Value.Color;
                            Vector4 modColorVector = new Vector4(modColor.R / 255.0f, modColor.G / 255.0f, modColor.B / 255.0f, modColor.A / 255.0f);
                            if(ImGui.ColorEdit4($"##{mod}_color", ref modColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                                mod.Value.Color = Color.FromArgb((int)(modColorVector.W * 255), (int)(modColorVector.X * 255), (int)(modColorVector.Y * 255), (int)(modColorVector.Z * 255));


                            ImGui.TableNextColumn();
                            availableWidth = ImGui.GetContentRegionAvail().X;
                            cursorPosX = ImGui.GetCursorPosX() + (availableWidth - controlWidth) / 2.0f;
                            ImGui.SetCursorPosX(cursorPosX);                            
                            bool showOnMaps = mod.Value.ShowOnMap;
                            if(ImGui.Checkbox($"##show_on_maps", ref showOnMaps))                        
                            mod.Value.ShowOnMap = showOnMaps;

                            ImGui.TableNextColumn(); 
                                    
                            ImGui.PushStyleColor(ImGuiCol.Text, modColorVector);                            
                            ImGui.TextUnformatted(mod.Value.ToString().Replace("Inc.", "Increased").Replace("Dec.", "Decreased"));
                            ImGui.PopStyleColor();

                            ImGui.PopID();
                        }  
                    }              
                } catch (Exception ex) {
                    Main.LogMessage($"Error loading map mods table: {ex.Message}\n{ex.StackTrace}");
                } finally {
                    ImGui.EndTable();
                }
            }   
        };
    }
}
[Submenu(CollapsedByDefault = true)]
public class WaypointSettings
{
    [JsonIgnore]
    public CustomNode CustomWaypointSettings { get; set; }
    public bool PanelIsOpen { get; set; } = false;
    public bool ShowWaypoints { get; set; } = true;
    public bool ShowWaypointArrows { get; set; } = true;

    public ObservableDictionary<string, Waypoint> Waypoints { get; set; } = new ObservableDictionary<string, Waypoint>();
    public WaypointSettings() {    
        CustomWaypointSettings = new CustomNode
        {
            DrawDelegate = () =>
            {

                if (ImGui.BeginTable("waypoint_options_table", 2, ImGuiTableFlags.NoBordersInBody|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 60);                                                               
                    ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthStretch, 300);                     
        
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool _show = ShowWaypoints;
                    if(ImGui.Checkbox($"##show_waypoints", ref _show))                        
                        ShowWaypoints = _show;

                    ImGui.TableNextColumn();
                    ImGui.Text("Show Waypoints on Atlas");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    bool _showArrows = ShowWaypointArrows;
                    if(ImGui.Checkbox($"##show_arrows", ref _showArrows))                        
                        ShowWaypointArrows = _showArrows;

                    ImGui.TableNextColumn();
                    ImGui.Text("Show Waypoint Arrows on Atlas");

                    ImGui.TableNextRow();

                }
            ImGui.EndTable();
            }
        };
    }
}

                
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Numerics;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using ExileMaps.Classes;
using ImGuiNET;
using Newtonsoft.Json;
using GameOffsets2.Native;

using static ExileMaps.ExileMapsCore;

namespace ExileMaps;

public class ExileMapsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public FeatureSettings Features { get; set; } = new FeatureSettings();    
    public HotkeySettings Keybinds { get; set; } = new HotkeySettings();  
    public GraphicSettings Graphics { get; set; } = new GraphicSettings();

    [Menu("Map Settings")]
    public MapSettings MapTypes { get; set; } = new MapSettings();
    public BiomeSettings Biomes { get; set; } = new BiomeSettings();
    public ContentSettings MapContent { get; set; } = new ContentSettings();
    public MapModSettings MapMods { get; set; } = new MapModSettings();
    public WaypointSettings Waypoints { get; set; } = new WaypointSettings();

}

[Submenu(CollapsedByDefault = false)]
public class FeatureSettings
{
    [Menu("Atlas Range", "Range (from your current viewpoint) to process atlas nodes.")]
    public RangeNode<int> AtlasRange { get; set; } = new(1500, 100, 20000);
    [Menu("Use Atlas Range for Node Connections", "Drawing node connections is performance intensive. By default it uses a range of 1000, but you can change it to use the Atlas range.")]
    public ToggleNode UseAtlasRange { get; set; } = new ToggleNode(false);

    [Menu("Process Visited Map Nodes")]
    public ToggleNode ProcessVisitedNodes { get; set; } = new ToggleNode(true);

    [Menu("Process Unlocked Map Nodes")]
    public ToggleNode ProcessUnlockedNodes { get; set; } = new ToggleNode(true);

    [Menu("Process Locked Map Nodes")]
    public ToggleNode ProcessLockedNodes { get; set; } = new ToggleNode(true);

    [Menu("Process Hidden Map Nodes")]
    public ToggleNode ProcessHiddenNodes { get; set; } = new ToggleNode(true);

    [Menu("Draw Connections for Visited Map Nodes")]
    public ToggleNode DrawVisitedNodeConnections { get; set; } = new ToggleNode(true);

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

}
[Submenu(CollapsedByDefault = true)]
public class HotkeySettings
{
    [Menu("Map Cache Refresh Hotkey", "Default: ]")]
    public HotkeyNode RefreshMapCacheHotkey { get; set; } = new HotkeyNode(Keys.Home);

    [Menu("Add Waypoint Hotkey", "Default: ,")]
    public HotkeyNode AddWaypointHotkey { get; set; } = new HotkeyNode(Keys.Insert);

    [Menu("Remove Waypoint Hotkey", "Default: .")]
    public HotkeyNode DeleteWaypointHotkey { get; set; } = new HotkeyNode(Keys.Delete);

    [Menu("Waypoint Panel Hotkey", "Default: /")]
    public HotkeyNode ToggleWaypointPanelHotkey { get; set; } = new HotkeyNode(Keys.End);

    [Menu("Show Towers in Range Hotkey", "Default: '")]
    public HotkeyNode ShowTowerRangeHotkey { get; set; } = new HotkeyNode(Keys.PageUp);

    [Menu("Toggle Processing Visited Nodes", "Default: '")]
    public HotkeyNode ToggleVisitedNodesHotkey { get; set; } = new HotkeyNode(Keys.F13);

    [Menu("Toggle Processing Unlocked Nodes", "Default: '")]
    public HotkeyNode ToggleUnlockedNodesHotkey { get; set; } = new HotkeyNode(Keys.F13);

    [Menu("Toggle Processing Locked Nodes", "Default: '")]
    public HotkeyNode ToggleLockedNodesHotkey { get; set; } = new HotkeyNode(Keys.F13);
    
    [Menu("Toggle Processing Hidden Nodes", "Default: '")]
    public HotkeyNode ToggleHiddenNodesHotkey { get; set; } = new HotkeyNode(Keys.F13);

    [Menu("Print Node Debug Data")]
    public HotkeyNode DebugKey { get; set; } = new HotkeyNode(Keys.F13);

    [Menu("Update Map Type Data")]
    public HotkeyNode UpdateMapsKey { get; set; } = new HotkeyNode(Keys.F13);
}

[Submenu(CollapsedByDefault = true)]
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
    public RangeNode<float> LabelInterpolationScale { get; set; } = new RangeNode<float>(0.5f, 0, 1);

    [Menu("Line Color", "Color of the map connection lines and waypoint lines when no map specific color is set")]
    public ColorNode LineColor { get; set; } = new ColorNode(Color.FromArgb(200, 255, 222, 222));

    [Menu("Line Width", "Width of the map connection lines and waypoint lines")]
    public RangeNode<float> MapLineWidth { get; set; } = new RangeNode<float>(3.0f, 0, 10);

    [Menu("Visited Line Color", "Color of the map connection lines when an both nodes are visited.")]
    public ColorNode VisitedLineColor { get; set; } = new ColorNode(Color.FromArgb(80, 255, 255, 255));

    [Menu("Unlocked Line Color", "Color of the map connection lines when an adjacent node is unlocked.")]
    public ColorNode UnlockedLineColor { get; set; } = new ColorNode(Color.FromArgb(170, 90, 255, 90));

    [Menu("Locked Line Color", "Color of the map connection lines when no adjacent nodes are unlocked.")]
    public ColorNode LockedLineColor { get; set; } = new ColorNode(Color.FromArgb(170, 255, 90, 90));

    [Menu("Draw Lines as Gradients", "Draws lines as a gradient between the two colors. Performance intensive.")]
    public ToggleNode DrawGradientLines { get; set; } = new ToggleNode(true);

    [Menu("Content Ring Width", "Width of the rings used to indicate map content")]
    public RangeNode<float> RingWidth { get; set; } = new RangeNode<float>(5.0f, 0, 10);

    [Menu("Content Radius", "Radius of the rings used to indicate map content")]
    public RangeNode<float> RingRadius { get; set; } = new RangeNode<float>(1f, 0, 10);
    
    [Menu("Node Radius", "Radius of the circles used to highlight map nodes")]
    public RangeNode<float> NodeRadius { get; set; } = new RangeNode<float>(1.5f, 0, 10);
}

[Submenu(CollapsedByDefault = true)]
/// <summary>
/// Settings for Map types
/// </summary>
/// MARK: MapSettings
public class MapSettings
{

    public MapSettings() {    
        
        CustomMapSettings = new CustomNode
        {
            
            DrawDelegate = () =>
            {
                
                if (ImGui.BeginTable("map_options_table", 2, ImGuiTableFlags.NoBordersInBody|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 40);                                                               
                    ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthStretch, 300);                     
                
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool showMapNames = ShowMapNames;
                    if(ImGui.Checkbox($"##showmapnames", ref showMapNames))                        
                        ShowMapNames = showMapNames;

                    ImGui.TableNextColumn();
                    ImGui.Text("Show Map Names");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool showUnlocked = ShowMapNamesOnUnlockedNodes;
                    if(ImGui.Checkbox($"##showunlocked", ref showUnlocked))                        
                        ShowMapNamesOnUnlockedNodes = showUnlocked;

                    ImGui.TableNextColumn();
                    ImGui.Text("Show Map Names on Unlocked Nodes");                           
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool showLocked = ShowMapNamesOnLockedNodes;
                    if(ImGui.Checkbox($"##showlocked", ref showLocked))                        
                        ShowMapNamesOnLockedNodes = showLocked;

                    ImGui.TableNextColumn();
                    ImGui.Text("Show Map Names on Locked Nodes");                                
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool showHidden = ShowMapNamesOnHiddenNodes;
                    if(ImGui.Checkbox($"##showhidden", ref showHidden))                        
                        ShowMapNamesOnHiddenNodes = showHidden;

                    ImGui.TableNextColumn();
                    ImGui.Text("Show Map Names on Hidden Nodes");    
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
                    ImGui.Text("Color Map Nodes by Weight");                          
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool useNameColors = UseColorsForMapNames;
                    if(ImGui.Checkbox($"##usenamecolors", ref useNameColors))                        
                        UseColorsForMapNames = useNameColors;

                    ImGui.TableNextColumn();
                    ImGui.Text("Use Map Colors for Map Names");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool useWeightColors = UseWeightColorsForMapNames;
                    if(ImGui.Checkbox($"##useweightcolors", ref useWeightColors))                        
                        UseWeightColorsForMapNames = useWeightColors;

                    ImGui.TableNextColumn();
                    ImGui.Text("Use Weight Colors for Map Names");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool drawWeight = DrawWeightOnMap;
                    if(ImGui.Checkbox($"##draw_weight", ref drawWeight))                        
                        DrawWeightOnMap = drawWeight;

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("Draw Weight % on Map");  

                    ImGui.TableNextColumn();
                    Color goodColor = GoodNodeColor;
                    Vector4 colorVector = new(goodColor.R / 255.0f, goodColor.G / 255.0f, goodColor.B / 255.0f, goodColor.A / 255.0f);
                    if(ImGui.ColorEdit4($"##goodgoodcolor", ref colorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        GoodNodeColor = Color.FromArgb((int)(colorVector.W * 255), (int)(colorVector.X * 255), (int)(colorVector.Y * 255), (int)(colorVector.Z * 255));

                    ImGui.TableNextColumn();
                    ImGui.Text("Good Node Color");    
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    Vector4 badColor = new(BadNodeColor.R / 255.0f, BadNodeColor.G / 255.0f, BadNodeColor.B / 255.0f, BadNodeColor.A / 255.0f);
                    if(ImGui.ColorEdit4($"##goodbadcolor", ref badColor, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        BadNodeColor = Color.FromArgb((int)(badColor.W * 255), (int)(badColor.X * 255), (int)(badColor.Y * 255), (int)(badColor.Z * 255));

                    ImGui.TableNextColumn();
                    ImGui.Text("Bad Node Color"); 
                }

                ImGui.EndTable();       

   

                
      

            }
        };

        MapTable = new CustomNode
        {
            
            DrawDelegate = () =>
            {

            ImGui.Separator();
            ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
            
            if (ImGui.BeginTable("maps_table", 7, ImGuiTableFlags.SizingFixedFit|ImGuiTableFlags.Borders|ImGuiTableFlags.PadOuterX))
            {

                ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed, 250);                                                              
                ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100); 
                ImGui.TableSetupColumn("Node", ImGuiTableColumnFlags.WidthFixed, 30);     
                ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthFixed, 30);               
                ImGui.TableSetupColumn("Text BG", ImGuiTableColumnFlags.WidthFixed, 30);
                ImGui.TableSetupColumn("Line", ImGuiTableColumnFlags.WidthFixed, 30);                              
                ImGui.TableSetupColumn("Biomes", ImGuiTableColumnFlags.WidthStretch, 200);   
                ImGui.TableHeadersRow();
                
                if (Maps.Count == 0)   
                    Main.LoadDefaultMaps();
                
                foreach (var (key,map) in Maps.OrderBy(x => x.Value.Name))
                {
                    ImGui.PushID($"Map_{key}");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    bool isMapHighlighted = map.Highlight;

                    // Highlight
                    if(ImGui.Checkbox($"##{key}_highlight", ref isMapHighlighted))
                        map.Highlight = isMapHighlighted;

                    // Name
                    ImGui.SameLine();
                    ImGui.Text(map.Name);

                    // Weight
                    ImGui.TableNextColumn();
                    float weight = map.Weight;
                    ImGui.SetNextItemWidth(100);
                    if(ImGui.SliderFloat($"##{key}_weight", ref weight, -25.0f, 50.0f, "%.1f"))                        
                        map.Weight = weight;

                    ImGui.TableNextColumn();

                    // Node Color
                    SettingsHelpers.CenterControl(30f);
                    Vector4 colorVector = new(map.NodeColor.R / 255.0f, map.NodeColor.G / 255.0f, map.NodeColor.B / 255.0f, map.NodeColor.A / 255.0f);
                    if(ImGui.ColorEdit4($"##{key}_nodecolor", ref colorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        map.NodeColor = Color.FromArgb((int)(colorVector.W * 255), (int)(colorVector.X * 255), (int)(colorVector.Y * 255), (int)(colorVector.Z * 255));
                    
                    // Text Color
                    ImGui.TableNextColumn();
                    SettingsHelpers.CenterControl(30f);
                    Vector4 nameColorVector = new(map.NameColor.R / 255.0f, map.NameColor.G / 255.0f, map.NameColor.B / 255.0f, map.NameColor.A / 255.0f);
                    if(ImGui.ColorEdit4($"##{key}_namecolor", ref nameColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        map.NameColor = Color.FromArgb((int)(nameColorVector.W * 255), (int)(nameColorVector.X * 255), (int)(nameColorVector.Y * 255), (int)(nameColorVector.Z * 255));
                    
                    // Text BG Color
                    ImGui.TableNextColumn();
                    SettingsHelpers.CenterControl(30f);
                    Vector4 bgColorVector = new(map.BackgroundColor.R / 255.0f, map.BackgroundColor.G / 255.0f, map.BackgroundColor.B / 255.0f, map.BackgroundColor.A / 255.0f);
                    if(ImGui.ColorEdit4($"##{key}_bgcolor", ref bgColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        map.BackgroundColor = Color.FromArgb((int)(bgColorVector.W * 255), (int)(bgColorVector.X * 255), (int)(bgColorVector.Y * 255), (int)(bgColorVector.Z * 255));
                    
                    // Line Color
                    ImGui.TableNextColumn();
                    SettingsHelpers.CenterControl(30f);
                    bool drawLine = map.DrawLine;
                    if(ImGui.Checkbox($"##{key}_line", ref drawLine))
                        map.DrawLine = drawLine;    
                    
                    // Biomes
                    ImGui.TableNextColumn();
                    ImGui.Text(map.BiomesToString() ?? "None");

                    ImGui.PopID();
                }                
            }
            ImGui.EndTable();
            }
        };

    }

    [JsonIgnore]
    public CustomNode CustomMapSettings { get; set; }

    [Menu("Map Types", 1032, CollapsedByDefault = false)]
    [JsonIgnore]
    public EmptyNode MapTypesHeader { get; set; }
    [JsonIgnore]    
    [Menu(null, parentIndex = 1032)]
    public CustomNode MapTable { get; set; }

    public bool HighlightMapNodes { get; set; } = true;
    public bool ColorNodesByWeight { get; set; } = true;
    public bool DrawWeightOnMap { get; set; } = false;
    public bool ShowMapNames { get; set; } = true;
    public bool UseColorsForMapNames { get; set; } = true;
    public bool UseWeightColorsForMapNames { get; set; } = true;
    public bool ShowMapNamesOnUnlockedNodes { get; set; } = true;
    public bool ShowMapNamesOnLockedNodes { get; set; } = true;
    public bool ShowMapNamesOnHiddenNodes { get; set; } = true;
    public Color GoodNodeColor { get; set; } = Color.FromArgb(200, 50, 255, 50);
    public Color BadNodeColor { get; set; } = Color.FromArgb(200, 255, 50, 50);
    public ObservableDictionary<string, Map> Maps { get; set; } = [];
}

/// <summary>
/// Settings for Biomes
/// </summary>
/// MARK: BiomeSettings
[Submenu(CollapsedByDefault = true)]
public class BiomeSettings
{
    [JsonIgnore]
    public CustomNode CustomBiomeSettings { get; set; }
    public bool ShowBiomeIcons { get; set; }
    public ObservableDictionary<string, Biome> Biomes { get; set; } = [];
    public BiomeSettings() {    

        CustomBiomeSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (ImGui.BeginTable("biome_options_table", 2, ImGuiTableFlags.NoBordersInBody|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 40);                                                               
                    ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthStretch, 300);                     
                
                    ImGui.TableNextRow();

                    // ImGui.TableNextColumn();
                    // bool showBiomeIcons = ShowBiomeIcons;
                    // if(ImGui.Checkbox($"##showbiomeicons", ref showBiomeIcons))                        
                    //     ShowBiomeIcons = showBiomeIcons;

                    // ImGui.TableNextColumn();
                    // ImGui.Text("Show Biome Icons");    
                }

                ImGui.EndTable();

                ImGui.Spacing();
                ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
                ImGui.Spacing();

                if (ImGui.BeginTable("biomes_table", 3, ImGuiTableFlags.Borders|ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("Biome", ImGuiTableColumnFlags.WidthFixed, 250);                                                               
                    ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);     
                    // ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 50);
                    // ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 50); 
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 50);
                    ImGui.TableHeadersRow();

                    foreach (var (key,biome) in Biomes.OrderBy(x => x.Value.Name))
                    {
                        ImGui.PushID($"Biome_{key}");
                        ImGui.TableNextRow();

                        // Name
                        ImGui.TableNextColumn();
                        ImGui.Text(key);

                        // Weight
                        ImGui.TableNextColumn();
                        float weight = biome.Weight;                        
                        ImGui.SetNextItemWidth(100);
                        if(ImGui.SliderFloat($"##{biome}_weight", ref weight, -5.0f, 5.0f, "%.2f"))                        
                            biome.Weight = weight;
                        
                        // // Color
                        // ImGui.TableNextColumn();
                        // SettingsHelpers.CenterControl(30f);
                        // Vector4 biomeColorVector = new(biome.Color.R / 255.0f, biome.Color.G / 255.0f, biome.Color.B / 255.0f, biome.Color.A / 255.0f);
                        // if(ImGui.ColorEdit4($"##{biome}_color", ref biomeColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        //     biome.Color = Color.FromArgb((int)(biomeColorVector.W * 255), (int)(biomeColorVector.X * 255), (int)(biomeColorVector.Y * 255), (int)(biomeColorVector.Z * 255));
                        
                        // // Icon
                        // ImGui.TableNextColumn();
                        // SettingsHelpers.CenterControl(30f);

                        
                        ImGui.PopID();
                    }
                }
                ImGui.EndTable();
            }
        };
    }
}

/// <summary>
/// Settings for Map Content
/// </summary>
/// MARK: ContentSettings
[Submenu(CollapsedByDefault = true)]
public class ContentSettings
{
    [JsonIgnore]
    public CustomNode CustomContentSettings { get; set; }
    public ObservableDictionary<string, Content> ContentTypes { get; set; } = [];

    public bool ShowRingsOnUnlockedNodes { get; set; } = true;
    public bool ShowRingsOnLockedNodes { get; set; } = true;
    public bool ShowRingsOnHiddenNodes { get; set; } = true;
    public bool DrawContentIconsOnHiddenNodes { get; set; } = false;


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
                    bool highlightUnlocked = ShowRingsOnUnlockedNodes;
                    if(ImGui.Checkbox($"##unlocked_nodes_highlight", ref highlightUnlocked))                        
                        ShowRingsOnUnlockedNodes = highlightUnlocked;

                    ImGui.TableNextColumn();
                    ImGui.Text("Highlight Content in Unlocked Map Nodes");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool highlightLocked = ShowRingsOnLockedNodes;
                    if(ImGui.Checkbox($"##locked_nodes_highlight", ref highlightLocked))                        
                        ShowRingsOnLockedNodes = highlightLocked;

                    ImGui.TableNextColumn();
                    ImGui.Text("Highlight Content in Locked Map Nodes");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    bool highlightHidden = ShowRingsOnHiddenNodes;
                    if(ImGui.Checkbox($"##hidden_nodes_highlight", ref highlightHidden))                        
                        ShowRingsOnHiddenNodes = highlightHidden;

                    ImGui.TableNextColumn();
                    ImGui.Text("Highlight Content in Hidden Map Nodes");                    
                }

                ImGui.EndTable();

                ImGui.Spacing();
                ImGui.TextWrapped("CTRL+Click on a slider to manually enter a value.");
                ImGui.Spacing();

                if (ImGui.BeginTable("content_table", 5, ImGuiTableFlags.Borders))
                {
                    ImGui.TableSetupColumn("Content Type", ImGuiTableColumnFlags.WidthFixed, 250);                                                               
                    ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);     
                    ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("Ring", ImGuiTableColumnFlags.WidthFixed, 70); 
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 50);
                    ImGui.TableHeadersRow();

                    foreach (var (key,content) in ContentTypes.OrderBy(x => x.Value.Name))
                    {
                        ImGui.PushID($"Content_{key}");
                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.Text(key);

                        ImGui.TableNextColumn();
                        float weight = content.Weight;                        
                        ImGui.SetNextItemWidth(100);
                        if(ImGui.SliderFloat($"##{key}_weight", ref weight, -5.0f, 5.0f, "%.2f")) 
                            content.Weight = weight;
                        
                        ImGui.TableNextColumn();
                        SettingsHelpers.CenterControl(30f);
                        Vector4 contentColorVector = new Vector4(content.Color.R / 255.0f, content.Color.G / 255.0f, content.Color.B / 255.0f, content.Color.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{key}_color", ref contentColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                            content.Color = Color.FromArgb((int)(contentColorVector.W * 255), (int)(contentColorVector.X * 255), (int)(contentColorVector.Y * 255), (int)(contentColorVector.Z * 255));
                        
                        ImGui.TableNextColumn();
                        SettingsHelpers.CenterControl(30f);
                        bool highlight = content.Highlight;
                        if(ImGui.Checkbox($"##{key}_highlight", ref highlight))                        
                            content.Highlight = highlight;
                        
                        ImGui.TableNextColumn();

                        ImGui.PopID();
                    }
                }
                ImGui.EndTable();
            }
        };
    }
}

/// <summary>
/// Settings for Map Mods
/// </summary>
/// MARK: MapModSettings
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
                    ImGui.Text("Only Count Mods that Apply (e.g. no breach mods on non-breach maps)");

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
                    if (ImGui.BeginTable("mod_table", 6, ImGuiTableFlags.Borders))
                    {
                        ImGui.TableSetupColumn("Mod Type", ImGuiTableColumnFlags.WidthFixed, 250);                                                               
                        ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);     
                        ImGui.TableSetupColumn("Min Value", ImGuiTableColumnFlags.WidthFixed, 100);
                        ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 50);
                        ImGui.TableSetupColumn("Show", ImGuiTableColumnFlags.WidthFixed, 50);                        
                        ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch, 300);
                        ImGui.TableHeadersRow();
                        foreach (var (key,mod) in MapModTypes.OrderBy(x => x.Value.ModID))
                        {
                            ImGui.PushID($"Mod_{key}");
                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            // Add a space between each word in ModID: Example TowerDeliriumChance should become Tower Delirium Chance
                            string modID = mod.ModID.Replace("Tower","");
                            modID = string.Concat(modID.Select(x => Char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');                        
                            ImGui.TextUnformatted(modID);

                            ImGui.TableNextColumn();
                            float weight = mod.Weight;                        
                            ImGui.SetNextItemWidth(100);
                            if(ImGui.SliderFloat($"##{mod}_weight", ref weight, -1.00f, 5.00f, "%.3f")) 
                                mod.Weight = weight;

                            ImGui.TableNextColumn();
                            float minVal = mod.MinValueToShow;                        
                            ImGui.SetNextItemWidth(100);
                            if(ImGui.SliderFloat($"##{mod}_minweight", ref minVal, 0.00f, 100.00f, "%.1f")) 
                                mod.MinValueToShow = minVal;
                            else if (ImGui.IsItemHovered()) {
                                ImGui.BeginTooltip();
                                ImGui.Text("Minimum total value required for this mod to be displayed on the Atlas.");
                                ImGui.EndTooltip();
                            }

                            ImGui.TableNextColumn();
                            SettingsHelpers.CenterControl(30f);
                            Vector4 modColorVector = new(mod.Color.R / 255.0f, mod.Color.G / 255.0f, mod.Color.B / 255.0f, mod.Color.A / 255.0f);
                            if(ImGui.ColorEdit4($"##{mod}_color", ref modColorVector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                                mod.Color = Color.FromArgb((int)(modColorVector.W * 255), (int)(modColorVector.X * 255), (int)(modColorVector.Y * 255), (int)(modColorVector.Z * 255));


                            ImGui.TableNextColumn();
                            SettingsHelpers.CenterControl(30f);                        
                            bool showOnMaps = mod.ShowOnMap;
                            if(ImGui.Checkbox($"##show_on_maps", ref showOnMaps))                        
                            mod.ShowOnMap = showOnMaps;



                            ImGui.TableNextColumn(); 
                                    
                            ImGui.PushStyleColor(ImGuiCol.Text, modColorVector);                            
                            ImGui.TextUnformatted(mod.ToString().Replace("Inc.", "Increased").Replace("Dec.", "Decreased"));
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

/// <summary>
/// Settings for Waypoints
/// </summary>
/// MARK: WaypointSettings
[Submenu(CollapsedByDefault = true)]
public class WaypointSettings
{
    [JsonIgnore]
    public CustomNode CustomWaypointSettings { get; set; }
    public bool PanelIsOpen { get; set; } = false;
    public bool ShowWaypoints { get; set; } = true;
    public bool ShowWaypointArrows { get; set; } = true;

    public int WaypointPanelMaxItems { get; set; } = 30;
    public string WaypointPanelSortBy { get; set; } = "Weight";
    public bool WaypointsUseRegex { get; set; } = false;
    public bool ShowUnlockedOnly { get; set; } = false;

    public string WaypointPanelFilter { get; set; } = "";
    public ObservableDictionary<string, Waypoint> Waypoints { get; set; } = [];
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

public static class SettingsHelpers {
    public static void CenterControl(float width) {
        float availableWidth = ImGui.GetContentRegionAvail().X;
        float cursorPosX = ImGui.GetCursorPosX() + (availableWidth - width) / 2.0f;
        ImGui.SetCursorPosX(cursorPosX);
    }        
}
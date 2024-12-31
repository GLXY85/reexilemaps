using System;
using System.Drawing;
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
using static ExileMaps.ExileMapsCore;

namespace ExileMaps;

public class ExileMapsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Toggle Features")]
    public FeatureSettings Features { get; set; } = new FeatureSettings();

    [Menu("Map Node Labelling")]
    public LabelSettings Labels { get; set; } = new LabelSettings();

    [Menu("Map Content Highlighting")]
    public HighlightSettings Highlights { get; set; } = new HighlightSettings();

    [Menu("Graphics, Colors, and Performance Settings")]    
    public GraphicSettings Graphics { get; set; } = new GraphicSettings();
    
    [Menu("Map and Waypoint Settings")]
    public MapHighlightSettings MapHighlightSettings { get; set; } = new MapHighlightSettings();

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

}

[Submenu(CollapsedByDefault = false)]
public class MapHighlightSettings
{
    public Dictionary<string, Map> Maps { get; set; } = new Dictionary<string, Map>();

    [JsonIgnore]
    public CustomNode MapSettings { get; set; }

    public MapHighlightSettings() {    
        MapSettings = new CustomNode
        {
            DrawDelegate = () =>
            {
                if (Maps.Count == 0)   
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.2f, 0.2f, 1)); // Red text
                    ImGui.TextWrapped("No maps found. Please open your Atlas and click 'Update Maps' under the 'Map and Waypoint Settings' menu.");
                    ImGui.PopStyleColor();
                }
                if (ImGui.BeginTable("maps_table", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Enable", ImGuiTableColumnFlags.WidthFixed, 25f);
                    ImGui.TableSetupColumn("Map Name", ImGuiTableColumnFlags.WidthFixed|ImGuiTableColumnFlags.PreferSortAscending, 150f);                                                              
                    ImGui.TableSetupColumn("Node", ImGuiTableColumnFlags.WidthFixed, 45f);     
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 45f);               
                    ImGui.TableSetupColumn("Background", ImGuiTableColumnFlags.WidthFixed, 45f);
                    ImGui.TableSetupColumn("Line", ImGuiTableColumnFlags.WidthFixed, 35f);                              
                    ImGui.TableSetupColumn("Biomes", ImGuiTableColumnFlags.WidthStretch, 200f);      
                    ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthStretch, 70f);    
                    ImGui.TableHeadersRow();

                    // Sort Maps alphabetically by Name
                    Maps = Maps.OrderBy(x => x.Value.Name).ToDictionary(x => x.Key, x => x.Value);

                    foreach (var map in Maps)
                    {
                        ImGui.TableNextRow();

                        // Enable
                        ImGui.TableNextColumn();
                        bool isMapHighlighted = map.Value.Highlight;
                        if(ImGui.Checkbox($"##{map}_highlight", ref isMapHighlighted))
                        {
                            map.Value.Highlight = isMapHighlighted;
                        }

                        // Map Name
                        ImGui.TableNextColumn();
                        ImGui.Text(map.Value.Name);

                        // Map Node Colour
                        ImGui.TableNextColumn();
                        Color nodeColor = map.Value.NodeColor;
                        Vector4 colorVector = new Vector4(nodeColor.R / 255.0f, nodeColor.G / 255.0f, nodeColor.B / 255.0f, nodeColor.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{map}_nodecolor", ref colorVector, ImGuiColorEditFlags.AlphaBar))
                        {
                            map.Value.NodeColor = Color.FromArgb((int)(colorVector.W * 255), (int)(colorVector.X * 255), (int)(colorVector.Y * 255), (int)(colorVector.Z * 255));
                        }

                        // Map Name Text Colour
                        ImGui.TableNextColumn();
                        Color nameColor = map.Value.NameColor;
                        Vector4 nameColorVector = new Vector4(nameColor.R / 255.0f, nameColor.G / 255.0f, nameColor.B / 255.0f, nameColor.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{map}_namecolor", ref nameColorVector, ImGuiColorEditFlags.AlphaBar))
                        {
                            map.Value.NameColor = Color.FromArgb((int)(nameColorVector.W * 255), (int)(nameColorVector.X * 255), (int)(nameColorVector.Y * 255), (int)(nameColorVector.Z * 255));
                        }

                        // Map Name Background Colour
                        ImGui.TableNextColumn();
                        Color bgColor = map.Value.BackgroundColor;
                        Vector4 bgColorVector = new Vector4(bgColor.R / 255.0f, bgColor.G / 255.0f, bgColor.B / 255.0f, bgColor.A / 255.0f);
                        if(ImGui.ColorEdit4($"##{map}_bgcolor", ref bgColorVector, ImGuiColorEditFlags.AlphaBar))
                        {
                            map.Value.BackgroundColor = Color.FromArgb((int)(bgColorVector.W * 255), (int)(bgColorVector.X * 255), (int)(bgColorVector.Y * 255), (int)(bgColorVector.Z * 255));
                        }

                        // Waypoint lines
                        ImGui.TableNextColumn();
                        bool drawLine = map.Value.DrawLine;
                        if(ImGui.Checkbox($"##{map}_line", ref drawLine))
                        {
                            map.Value.DrawLine = drawLine;
                        }


                        // Map Biomes
                        ImGui.TableNextColumn();
                        ImGui.Text("");

                                                // Map Counter
                        ImGui.TableNextColumn();
                        ImGui.Text(map.Value.Count.ToString());

                    }                
                }
                ImGui.EndTable();
                ImGui.Spacing();

                var updatingMaps = false;

                    if (ImGui.Button("Update Maps") && !updatingMaps) {
                        var WorldMap = Main.Game.IngameState.IngameUi.WorldMap.AtlasPanel;
                        var screenCenter = Main.Game.Window.GetWindowRectangle().Center;
                        // if WorldMap isn't open, return
                        if (WorldMap == null) return;
                        
                        updatingMaps = true;
                        try
                        {
                            // Get unique map nodes within range

                            // Get map nodes with unique Element.Area.Name

                            var mapNodes = WorldMap.Descriptions                                
                                .GroupBy(x => x.Element.Area.Name)
                                .Select(g => g.First())
                                .ToList();

                            // Add the maps to the Maps dictionary if they don't already exist
                            foreach (var mapNode in mapNodes)
                            {

                                // check if the mapnode name exists in the maps dictionary



                                var mapName = mapNode.Element.Area.Name;
                                // We use this "Fake" ID because there are multiple maps with the same name but different IDs
                                // e.g. a map with a boss and without may have a different ID and layout
                                var mapId = mapNode.Element.Area.Name.ToString().Replace(" ", "");
                                
                                if (Maps.ContainsKey(mapId)) {
                                    // Update the map properties                                    
                                    Maps[mapId].RealID = mapNode.Element.Area.Id;                                    
                                    Maps[mapId].Count = WorldMap.Descriptions.Count(x => x.Element.Area.Name == mapName);
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
                        ImGui.SetTooltip("Add any/all new maps to the map list. Atlas map must be open.");
                    }
                
                ImGui.SameLine();
                ImGui.Spacing();
                ImGui.SameLine();
                if (ImGui.Button("Clear Maps") && !updatingMaps)
                {
                    ImGui.OpenPopup("Confirm Clear Maps");
                } else if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Clears the map list. This will erase all map specific settings and you will need to open your Atlas and update the maps again.");
                }
                bool open = true;
                if (ImGui.BeginPopupModal("Confirm Clear Maps", ref open, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("Are you sure you want to clear all maps? This will erase all map specific settings.");
                    ImGui.Separator();

                    if (ImGui.Button("Yes", new Vector2(120, 0)))
                    {
                        updatingMaps = true;
                        Maps.Clear();
                        Main.LogMessage("Cleared Maps");
                        updatingMaps = false;
                        ImGui.CloseCurrentPopup();
                        
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("No", new Vector2(120, 0)))
                        ImGui.CloseCurrentPopup();

                    ImGui.EndPopup();
                    open = false;
                }
            }
        };
    }
}

        
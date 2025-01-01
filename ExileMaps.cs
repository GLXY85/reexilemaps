using ExileCore2;
using ExileCore2.Shared.Nodes;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using GameOffsets2.Native;

using ImGuiNET;

using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Numerics;
using System.Text.Json;
using System.Collections.Generic;

using ExileMaps.Classes;

namespace ExileMaps;

public class ExileMapsCore : BaseSettingsPlugin<ExileMapsSettings>
{
    private int tickCount { get; set; }
    public static ExileMapsCore Main;

    private const string defaultMapsPath = "json\\maps.json";
    private const string defaultModsPath = "json\\mods.json";
    private const string defaultBiomesPath = "json\\biomes.json";
    private const string defaultContentPath = "json\\content.json";

    public GameController Game => GameController;
    public IngameState State => Game.IngameState;
    private Vector2 screenCenter;
    private bool refreshCache = false;


    public override bool Initialise()
    {
        Main = this;        

        // RegisterHotkey(Settings.Pathfinding.RefreshNodeCacheHotkey);
        // RegisterHotkey(Settings.Pathfinding.SetCurrentLocationHotkey);
        // RegisterHotkey(Settings.Pathfinding.AddWaypointHotkey);

        LoadDefaultBiomes();
        LoadDefaultContentTypes();
        LoadDefaultMaps();
  
        return true;
    }

    private void LoadDefaultBiomes() {
        try {
        if (Settings.Biomes.Biomes == null)
            Settings.Biomes.Biomes = new Dictionary<string, Biome>();

        var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultBiomesPath));
        var biomes = JsonSerializer.Deserialize<Dictionary<string, Biome>>(jsonFile);

        foreach (var biome in biomes)
            if (!Settings.Biomes.Biomes.ContainsKey(biome.Key)) 
                Settings.Biomes.Biomes.Add(biome.Key, biome.Value);  
        } catch (Exception e) {
            LogError("Error loading default biomes: " + e.Message);
        }
            
    }

    private void LoadDefaultContentTypes() {
        try {
            if (Settings.MapContent.ContentTypes == null)
                Settings.MapContent.ContentTypes = new Dictionary<string, Content>();

            var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultContentPath));
            var contentTypes = JsonSerializer.Deserialize<Dictionary<string, Content>>(jsonFile);

            foreach (var content in contentTypes)
                if (!Settings.MapContent.ContentTypes.ContainsKey(content.Key)) 
                    Settings.MapContent.ContentTypes.Add(content.Key, content.Value);   
        } catch (Exception e) {
            LogError("Error loading default content types: " + e.Message);
        }

    }
    
    public void LoadDefaultMaps()
    {
        try {
            if (Settings.Maps.Maps == null)
                Settings.Maps.Maps = new Dictionary<string, Map>();

            var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultMapsPath));
            var maps = JsonSerializer.Deserialize<Dictionary<string, Map>>(jsonFile);

            foreach (var map in maps) {
                if (!Settings.Maps.Maps.ContainsKey(map.Key)) 
                    Settings.Maps.Maps.Add(map.Key, map.Value);                       
                else if (Settings.Maps.Maps[map.Key].Biomes == null) 
                    Settings.Maps.Maps[map.Key].Biomes = map.Value.Biomes;
            }
        } catch (Exception e) {
            LogError("Error loading default maps: " + e.Message);
        }
    }
    private static void RegisterHotkey(HotkeyNode hotkey)
    {
        Input.RegisterKey(hotkey);
        hotkey.OnValueChanged += () => { Input.RegisterKey(hotkey); };
    }

    public override void AreaChange(AreaInstance area)
    {
        refreshCache = true;        
    }

    public override void Tick()
    {
        screenCenter = Game.Window.GetWindowRectangle().Center - Game.Window.GetWindowRectangle().Location;
        return;
    }

    public override void Render()
    {


        // if (Settings.Pathfinding.RefreshNodeCacheHotkey.PressedOnce())
        // {
        //     LogMessage("Node Cache Refreshed: ");
        // }
        // if (Settings.Pathfinding.SetCurrentLocationHotkey.PressedOnce())
        // {
        //     LogMessage("Current Location: ");
        // }
        // if (Settings.Pathfinding.AddWaypointHotkey.PressedOnce())
        // {
        //     LogMessage("Waypoint Added: ");
        // }
        // Only render every n ticks
        tickCount++;
        if (Settings.Graphics.RenderNTicks.Value % tickCount != 0) 
            return;  

        tickCount = 0;

        if (State.IngameUi.WorldMap.AtlasPanel == null || !State.IngameUi.WorldMap.AtlasPanel.IsVisible)
            return;

        var WorldMap = State.IngameUi.WorldMap.AtlasPanel;

        List<AtlasNodeDescription> mapNodes;

        // Get all map nodes within the specified range.
        try {
            mapNodes = WorldMap.Descriptions.FindAll(x => Vector2.Distance(screenCenter, x.Element.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000));//
        } catch (Exception e) {
            LogError("Error getting map nodes: " + e.Message);
            return;
        }

        // Filter out nodes based on settings.
        var selectedNodes = mapNodes
            .Where(x => ((Settings.Features.ProcessUnlockedNodes && x.Element.IsUnlocked && !x.Element.IsVisited) ||
                        (Settings.Features.ProcessLockedNodes && !x.Element.IsUnlocked) ||
                        (Settings.Features.ProcessHiddenNodes && !x.Element.IsVisible)) &&
                        !(!x.Element.IsUnlocked && x.Element.IsVisited));
        
        foreach (var mapNode in selectedNodes)
        {
            var ringCount = 0;           

            try {
                ringCount += HighlightMapNode(mapNode, ringCount, "Breach", Settings.Highlights.HighlightBreaches, Settings.Highlights.breachColor);
                ringCount += HighlightMapNode(mapNode, ringCount, "Delirium", Settings.Highlights.HighlightDelirium, Settings.Highlights.deliriumColor);
                ringCount += HighlightMapNode(mapNode, ringCount, "Expedition", Settings.Highlights.HighlightExpedition, Settings.Highlights.expeditionColor);
                ringCount += HighlightMapNode(mapNode, ringCount, "Ritual", Settings.Highlights.HighlightRitual, Settings.Highlights.ritualColor);
                ringCount += HighlightMapNode(mapNode, ringCount, "Boss", Settings.Highlights.HighlightBosses, Settings.Highlights.bossColor);
                DrawMapNode(mapNode);
                DrawMapName(mapNode);
            } catch (Exception e) {
                // doin a crime
            }
            

            
        }
        
        try {
            if (Settings.Features.DrawVisibleNodeConnections || Settings.Features.DrawHiddenNodeConnections) {
                
                var connectionNodes = mapNodes
                .Where(x => (Settings.Features.DrawVisibleNodeConnections && x.Element.IsVisible) || (Settings.Features.DrawHiddenNodeConnections && !x.Element.IsVisible))
                .Where(x => Vector2.Distance(screenCenter, x.Element.GetClientRect().Center) <= (Settings.Features.UseAtlasRange ? Settings.Features.AtlasRange : 1000));

                foreach (var mapNode in connectionNodes)
                {
                    DrawConnections(WorldMap, mapNode);
                }

            }
        } catch (Exception e) {
            // oops i did it again
        }

        string[] waypointNames = Settings.Maps.Maps.Where(x => x.Value.DrawLine).Select(x => x.Value.Name.Trim()).ToArray();

        if (waypointNames.Length > 0) {
            var waypointNodes = WorldMap.Descriptions
                    .Where(x => waypointNames.Contains(x.Element.Area.Name.Trim()))
                    .Where(x => !x.Element.IsVisited && !(!x.Element.IsUnlocked && x.Element.IsVisited))
                    .Where(x => Vector2.Distance(screenCenter, x.Element.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000) || !Settings.Features.WaypointsUseAtlasRange);

            try {
                foreach (var mapNode in waypointNodes)
                {
                    try {
                        DrawWaypointLine(mapNode);
                    } catch (Exception e) {
                        LogError($"Error drawing waypoint line to map node: {mapNode.Element.Area.Name.Trim()}: {e.Message}");
                    }
                }
            }   catch (Exception e) {
                LogError("Error drawing waypoint lines: " + e.Message);
            }
        }


        if (Settings.Features.DebugMode)
        {
            foreach (var mapNode in mapNodes)
            {
                var text = mapNode.Address.ToString("X");
                var position = mapNode.Element.GetClientRect().Center + new Vector2(0, 35);
                DrawCenteredTextWithBackground(mapNode.Address.ToString("X"), position, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
                position += new Vector2(0, 27);
                DrawCenteredTextWithBackground(mapNode.Coordinate.ToString(), position, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
            }

        }
    }

    

    /// <summary>
    /// Draws lines between a map node and its connected nodes on the atlas.
    /// </summary>
    /// <param name="WorldMap">The atlas panel containing the map nodes and their connections.</param>
    /// <param name="mapNode">The map node for which connections are to be drawn.</param>
    /// 
    private void DrawConnections(AtlasPanel WorldMap, AtlasNodeDescription mapNode)
    {
        var mapConnections = WorldMap.Points.FirstOrDefault(x => x.Item1 == mapNode.Coordinate);

        if (mapConnections.Equals(default((Vector2i, Vector2i, Vector2i, Vector2i, Vector2i))))
            return;

        var connectionArray = new[] { mapConnections.Item2, mapConnections.Item3, mapConnections.Item4, mapConnections.Item5 };

        foreach (var coordinates in connectionArray)
        {
            if (coordinates == default)
                continue;

            var destinationNode = WorldMap.Descriptions.FirstOrDefault(x => x.Coordinate == coordinates);
            if (destinationNode != null)
            {
                var color = (destinationNode.Element.IsUnlocked || mapNode.Element.IsUnlocked) ? Settings.Graphics.UnlockedLineColor : Settings.Graphics.LockedLineColor;
                Graphics.DrawLine(mapNode.Element.GetClientRect().Center, destinationNode.Element.GetClientRect().Center, Settings.Graphics.MapLineWidth, color);
            }
        }
    }
    /// <summary>
    /// Highlights a map node by drawing a circle around it if certain conditions are met.
    /// </summary>
    /// <param name="mapNode">The map node to be highlighted.</param>
    /// <param name="Count">The count used to calculate the radius of the circle.</param>
    /// <param name="Content">The content string to check within the map node's elements.</param>
    /// <param name="Draw">A boolean indicating whether to draw the circle or not.</param>
    /// <param name="color">The color of the circle to be drawn.</param>
    /// <returns>Returns 1 if the circle is drawn, otherwise returns 0.</returns>
    private int HighlightMapNode(AtlasNodeDescription mapNode, int Count, string Content, bool Draw, Color color)
    {
        if (!Settings.Features.DrawContentRings || !Draw || !mapNode.Element.Content.Any(x => x.Name.Contains(Content)))
            return 0;

        var radius = (Count * 5) + (mapNode.Element.GetClientRect().Right - mapNode.Element.GetClientRect().Left) / 2;
        Graphics.DrawCircle(mapNode.Element.GetClientRect().Center, radius, color, 4, 16);

        return 1;
    }
    
    /// Draws a line from the center of the screen to the specified map node on the atlas.
    /// </summary>
    /// <param name="mapNode">The atlas node to which the line will be drawn.</param>
    /// <remarks>
    /// This method checks if the feature to draw lines is enabled in the settings. If enabled, it finds the corresponding map settings
    /// for the given map node. If the map settings are found and the line drawing is enabled for that map, it proceeds to draw the line.
    /// Additionally, if the feature to draw line labels is enabled, it draws the node name and the distance to the node.
    /// </remarks>
    private void DrawWaypointLine(AtlasNodeDescription mapNode)
    {
        if (!Settings.Features.DrawLines)
            return;

        var map = Settings.Maps.Maps.FirstOrDefault(x => x.Value.Name.Trim() == mapNode.Element.Area.Name.Trim() && x.Value.DrawLine == true).Value;
        
        if (map == null)
            return;

        var color = map.NodeColor;
        var distance = Vector2.Distance(screenCenter, mapNode.Element.GetClientRect().Center);
        // Position for label and start of line.
        Vector2 position = Vector2.Lerp(screenCenter, mapNode.Element.GetClientRect().Center, Settings.Graphics.LabelInterpolationScale);
        // Draw the line from the center(ish) of the screen to the center of the map node.

        Graphics.DrawLine(position, mapNode.Element.GetClientRect().Center, Settings.Graphics.MapLineWidth, color);

        // If labels are enabled, draw the node name and the distance to the node.
        if (Settings.Features.DrawLineLabels) {
            string text = mapNode.Element.Area.Name.Trim();
            text += $" ({Vector2.Distance(screenCenter, mapNode.Element.GetClientRect().Center).ToString("0")})";
            
            DrawCenteredTextWithBackground(text, position, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        }
        
    }
    
    /// Draws a highlighted circle around a map node on the atlas if the node is configured to be highlighted.
    /// </summary>
    /// <param name="mapNode">The atlas node description containing information about the map node to be drawn.</param>    private void DrawMapNode(AtlasNodeDescription mapNode)
    private void DrawMapNode(AtlasNodeDescription mapNode)
    {
        if (!Settings.Features.DrawNodeHighlights)
            return;

        var map = Settings.Maps.Maps.FirstOrDefault(x => x.Value.Name.Trim() == mapNode.Element.Area.Name.Trim() && x.Value.Highlight == true).Value;

        if (map == null)
            return;

        var radius = 5 - (mapNode.Element.GetClientRect().Right - mapNode.Element.GetClientRect().Left) / 2;
        Graphics.DrawCircleFilled(mapNode.Element.GetClientRect().Center, radius, map.NodeColor, 16);
    }

    /// <summary>
    /// Draws the name of the map on the atlas.
    /// </summary>
    /// <param name="mapNode">The atlas node description containing information about the map.</param>
    private void DrawMapName(AtlasNodeDescription mapNode)
    {
        if (!Settings.Features.DrawNodeLabels ||
            (!mapNode.Element.IsVisible && !Settings.Labels.LabelHiddenNodes) ||
            (mapNode.Element.IsUnlocked && !Settings.Labels.LabelUnlockedNodes) ||
            (!mapNode.Element.IsUnlocked && !Settings.Labels.LabelLockedNodes))
            return;

        var fontColor = Settings.Graphics.FontColor;
        var backgroundColor = Settings.Graphics.BackgroundColor;

        if (Settings.Features.NameHighlighting) {            
            var map = Settings.Maps.Maps.FirstOrDefault(x => x.Value.Name.Trim() == mapNode.Element.Area.Name.Trim() && x.Value.Highlight == true).Value;

            if (map != null) {
                fontColor = map.NameColor;
                backgroundColor = map.BackgroundColor;
            }
        }

        DrawCenteredTextWithBackground(mapNode.Element.Area.Name.Trim().ToUpper(), mapNode.Element.GetClientRect().Center, fontColor, backgroundColor, true, 10, 3);
    }
    
    /// <summary>
    /// Draws text with a background color at the specified position.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="position">The position to draw the text at.</param>
    /// <param name="textColor">The color of the text.</param>
    /// <param name="backgroundColor">The color of the background.</param>
    private void DrawCenteredTextWithBackground(string text, Vector2 position, Color color, Color backgroundColor, bool center = false, int xPadding = 0, int yPadding = 0)
    {
        var boxSize = Graphics.MeasureText(text);

        boxSize += new Vector2(xPadding, yPadding);    

        if (center)
            position = position - new Vector2(boxSize.X / 2, boxSize.Y / 2);

        Graphics.DrawBox(position, boxSize + position, backgroundColor, 5.0f);       

        position += new Vector2(xPadding / 2, yPadding / 2);

        Graphics.DrawText(text, position, color);
    }

}

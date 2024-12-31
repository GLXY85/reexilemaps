using ExileCore2;
using ExileCore2.Shared.Nodes;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using GameOffsets2.Native;

using ImGuiNET;

using System;
using System.Linq;
using System.Drawing;
using System.Numerics;
using System.Text.Json;
using System.Collections.Generic;

namespace ExileMaps;

public class ExileMapsCore : BaseSettingsPlugin<ExileMapsSettings>
{
    private int tickCount { get; set; }
    public static ExileMapsCore Main;
    
    public GameController Game => GameController;
    public IngameState State => Game.IngameState;

    public override bool Initialise()
    {
        Main = this;        
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {

    }

    public override void Tick()
    {
        return;
    }

    public override void Render()
    {
        // Only render every n ticks
        tickCount++;
        if (Settings.Graphics.RenderNTicks.Value % tickCount != 0) 
            return;  

        tickCount = 0;

        var WorldMap = State.IngameUi.WorldMap.AtlasPanel;

        // If the world map is not visible, return.
        if (!WorldMap.IsVisible)
            return;

        // Get all map nodes within the specified range.
        var mapNodes = WorldMap.Descriptions.FindAll(x => Vector2.Distance(Game.Window.GetWindowRectangle().Center, x.Element.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000));//

        // Filter out nodes based on settings.
        var selectedNodes = mapNodes
            .Where(x => ((Settings.Features.ProcessUnlockedNodes && x.Element.IsUnlocked && !x.Element.IsVisited) ||
                        (Settings.Features.ProcessLockedNodes && !x.Element.IsUnlocked) ||
                        (Settings.Features.ProcessHiddenNodes && !x.Element.IsVisible)) &&
                        !(!x.Element.IsUnlocked && x.Element.IsVisited));
        
        foreach (var mapNode in selectedNodes)
        {
            var ringCount = 0;           

            // Draw content rings
            ringCount += HighlightMapNode(mapNode, ringCount, "Breach", Settings.Highlights.HighlightBreaches, Settings.Highlights.breachColor);
            ringCount += HighlightMapNode(mapNode, ringCount, "Delirium", Settings.Highlights.HighlightDelirium, Settings.Highlights.deliriumColor);
            ringCount += HighlightMapNode(mapNode, ringCount, "Expedition", Settings.Highlights.HighlightExpedition, Settings.Highlights.expeditionColor);
            ringCount += HighlightMapNode(mapNode, ringCount, "Ritual", Settings.Highlights.HighlightRitual, Settings.Highlights.ritualColor);
            ringCount += HighlightMapNode(mapNode, ringCount, "Boss", Settings.Highlights.HighlightBosses, Settings.Highlights.bossColor);
                
            DrawWaypointLine(mapNode); // Draw waypoint lines
            DrawMapNode(mapNode); // Draw node highlights
            DrawMapName(mapNode); // Draw node names
             // Draw hidden node connections
            
        }
        
        if (Settings.Features.DrawVisibleNodeConnections || Settings.Features.DrawHiddenNodeConnections) {
            
            var connectionNodes = mapNodes
            .Where(x => (Settings.Features.DrawVisibleNodeConnections && x.Element.IsVisible) || (Settings.Features.DrawHiddenNodeConnections && !x.Element.IsVisible))
            .Where(x => Vector2.Distance(Game.Window.GetWindowRectangle().Center, x.Element.GetClientRect().Center) <= (Settings.Features.UseAtlasRange ? Settings.Features.AtlasRange : 1000));

            foreach (var mapNode in connectionNodes)
            {
                DrawConnections(WorldMap, mapNode);
            }

        }

        // if (Settings.Features.DrawTowerRange) {
        //     var towerNodes = WorldMap.Descriptions            
        //     .FindAll(x => Vector2.Distance(Game.Window.GetWindowRectangle().Center, x.Element.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000))
        //     .Where(x => x.Element.Area.Name.Contains("Lost Tower"));

        //     foreach (var mapNode in towerNodes) {
        //         DrawTowerRange(mapNode);
        //     }
        // }


        if (Settings.Features.DebugMode)
        {
            foreach (var mapNode in mapNodes)
            {
                var text = mapNode.Address.ToString("X");
                Graphics.DrawText(text, mapNode.Element.GetClientRect().TopLeft, Color.Red);
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
                Graphics.DrawLine(mapNode.Element.GetClientRect().Center, destinationNode.Element.GetClientRect().Center, Settings.Graphics.MapLineWidth, Settings.Graphics.LineColor);
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

        var map = Settings.MapHighlightSettings.Maps.FirstOrDefault(x => x.Value.Name == mapNode.Element.Area.Name && x.Value.DrawLine == true).Value;
        
        if (map == null)
            return;

        var color = map.NodeColor;

        // Position for label and start of line.
        Vector2 position = Vector2.Lerp(Game.Window.GetWindowRectangle().Center, mapNode.Element.GetClientRect().Center, Settings.Graphics.LabelInterpolationScale);
        
        // If labels are enabled, draw the node name and the distance to the node.
        if (Settings.Features.DrawLineLabels) {
            string text = mapNode.Element.Area.Name;
            text += $" ({Vector2.Distance(Game.Window.GetWindowRectangle().Center, mapNode.Element.GetClientRect().Center).ToString("0")})";
            
            DrawTextWithBackground(text, position, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        }

        // Draw the line from the center(ish) of the screen to the center of the map node.
        Graphics.DrawLine(position, mapNode.Element.GetClientRect().Center, Settings.Graphics.MapLineWidth, color);
    }
    
    /// Draws a highlighted circle around a map node on the atlas if the node is configured to be highlighted.
    /// </summary>
    /// <param name="mapNode">The atlas node description containing information about the map node to be drawn.</param>    private void DrawMapNode(AtlasNodeDescription mapNode)
    private void DrawMapNode(AtlasNodeDescription mapNode)
    {
        if (!Settings.Features.DrawNodeHighlights)
            return;

        var map = Settings.MapHighlightSettings.Maps.FirstOrDefault(x => x.Value.Name == mapNode.Element.Area.Name && x.Value.Highlight == true).Value;

        if (map == null)
            return;

        var radius = 5 - (mapNode.Element.GetClientRect().Right - mapNode.Element.GetClientRect().Left) / 2;
        Graphics.DrawCircleFilled(mapNode.Element.GetClientRect().Center, radius, map.NodeColor, 8);
    }

    /// <summary>
    /// Draws the name of the map on the atlas.
    /// </summary>
    /// <param name="mapNode">The atlas node description containing information about the map.</param>
    private void DrawMapName(AtlasNodeDescription mapNode)
    {
        // If names are disabled, return.
        if (!Settings.Features.DrawNodeLabels)
            return;

        // If element is invisible and unrevealed names are disabled, return.
        if (!mapNode.Element.IsVisible && !Settings.Labels.LabelHiddenNodes)
            return; 

        // If element is locked and locked names are disabled, return.
        if (mapNode.Element.IsUnlocked && !Settings.Labels.LabelUnlockedNodes)
            return; 

        // If element is unlocked and unlocked names are disabled,
        if (!mapNode.Element.IsUnlocked && !Settings.Labels.LabelLockedNodes)
            return; 

        var fontColor = Settings.Graphics.FontColor;
        var backgroundColor = Settings.Graphics.BackgroundColor;

        if (Settings.Features.NameHighlighting) {            
            var map = Settings.MapHighlightSettings.Maps.FirstOrDefault(x => x.Value.Name == mapNode.Element.Area.Name && x.Value.Highlight == true).Value;

            if (map != null) {
                fontColor = map.NameColor;
                backgroundColor = map.BackgroundColor;
            }
        }

        DrawTextWithBackground(mapNode.Element.Area.Name.ToUpper(), mapNode.Element.GetClientRect().Center, fontColor, backgroundColor, true, 10, 3);
    }
    
    // private void DrawTowerRange(AtlasNodeDescription towerNode)
    // {
    //     if (!Settings.Features.DrawTowerRange || (!towerNode.Element.IsVisited && !Settings.Features.DrawInactiveTowers))
    //         return;

    //     var towerPosition = towerNode.Element.GetClientRect().Center;
    //     var towerRadius = towerNode.Element.Scale * Settings.Features.TowerEffectRange;

    //     _atlasGraphics.DrawEllipse(towerPosition, towerRadius, Settings.Features.TowerColor, 3.0f, Settings.Features.TowerRingWidth, 64);
    // }
    /// <summary>
    /// Draws text with a background color at the specified position.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="position">The position to draw the text at.</param>
    /// <param name="textColor">The color of the text.</param>
    /// <param name="backgroundColor">The color of the background.</param>
    private void DrawTextWithBackground(string text, Vector2 position, Color color, Color backgroundColor, bool center = false, int xPadding = 0, int yPadding = 0)
    {
        var boxSize = Graphics.MeasureText(text);

        boxSize += new Vector2(xPadding, yPadding);    

        if (center)
            position = position - new Vector2(boxSize.X / 2, boxSize.Y / 2);

        Graphics.DrawBox(position, boxSize + position, backgroundColor, 5.0f);        

        // Pad text position
        position += new Vector2(xPadding / 2, yPadding / 2);

        Graphics.DrawText(text, position, color);
    }
}

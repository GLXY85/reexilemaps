using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using System.Linq;
using ImGuiNET;
using System.Numerics;
using System.Drawing;
using ExileCore2.PoEMemory;
using ExileCore2.Shared.Nodes;

namespace ExileMaps;

public class ExileMaps : BaseSettingsPlugin<ExileMapsSettings>
{
    private int tickCount { get; set; }

    private IngameState State => GameController.IngameState;

    public override bool Initialise()
    {
        //Perform one-time initialization here

        //Maybe load you custom config (only do so if builtin settings are inadequate for the job)
        //var configPath = Path.Join(ConfigDirectory, "custom_config.txt");
        //if (File.Exists(configPath))
        //{
        //    var data = File.ReadAllText(configPath);
        //}
        // if Maps is empty, populate with default
        if (Settings.MapHighlightSettings.Maps.Count == 0) {
            var defaultMaps = "Abyss,Augury,Backwash,Bloodwood,Blooming Field,Burial Bog,Canal Hideout,Cenotes,Creek,Crimson Shores,Crypt,Decay,Deserted,Felled Hideout,Forge,Fortress,Gothic City,Hidden Grotto,Hive,Limestone Hideout,Lofty Summit,Lost Towers,Mire,Moment of Zen,Necropolis,Oasis,Penitentiary,Ravine,Riverside,Rustbowl,Savannah,Sandspit,Seepage,Slick,Spider Woods,Steaming Springs,Sulphuric Caverns,Steppe,Sump,The Copper Citadel ,The Iron Citadel,The Stone Citadel,Untainted Paradise,Vaal Foundry ,Vaal Factory ,Willow,Woodland"
                .Split(',');

            foreach (var map in defaultMaps)
            {
                Settings.MapHighlightSettings.Maps.Add(map.Replace(" ",""), 
                new Map { 
                    Name = map, 
                    ID = map.Replace(" ",""),
                    Highlight = false,
                    NodeColor = Color.FromArgb(180, 25, 200, 25),
                    NameColor = Color.FromArgb(255, 255, 255, 255),
                    BackgroundColor = Color.FromArgb(100, 0, 0, 0)
                });
                // Log
                LogMessage($"Added {map} to MapHighlightSettings");
            }

            

        }
                
        
        
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        //Perform once-per-zone processing here
        //For example, Radar builds the zone map texture here
    }

    public override void Tick()
    {
        //Perform non-render-related work here, e.g. position calculation.
        //This method is still called on every frame, so to really gain
        //an advantage over just throwing everything in the Render method
        //you have to return a custom job, but this is a bit of an advanced technique
        //here's how, just in case:
        //return new Job($"{nameof(ExileMaps)}MainJob", () =>
        //{
        //    var a = Math.Sqrt(7);
        //});

        //otherwise, just run your code here
        //var a = Math.Sqrt(7);
        return;
    }

    public override void Render()
    {
        var WorldMap = State.IngameUi.WorldMap.AtlasPanel;


        if (!WorldMap.IsVisible)
            return;

        
        tickCount++;
        if (Settings.Graphics.RenderNTicks.Value % tickCount != 0) 
            return;  

        var screenCenter = State.IngameUi.GetClientRect().Center;

        tickCount = 0;

        var mapNodes = WorldMap.Descriptions.FindAll(x => Vector2.Distance(screenCenter, x.Element.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000));//

        var zigguratNode = mapNodes.Find(x => x.Element.Area.Name.Contains("Ziggurat"));
        // Visible Nodes
        var visibleNodes = mapNodes.FindAll(x => x.Element.IsVisible);

        // Visited Nodes - not used yet
        // var visitedNodes = visibleNodes.FindAll(x => x.Element.IsVisited);

        // Locked Nodes
        var lockedNodes = visibleNodes.FindAll(x => !x.Element.IsUnlocked);

        // Unlocked Nodes
        var unlockedNodes = visibleNodes.FindAll(x => x.Element.IsUnlocked && !x.Element.IsVisited);

        // Invisible Nodes
        var invisibleNodes = mapNodes.FindAll(x => !x.Element.IsVisible);

        // Draw Unlocked nodes
        if (Settings.Features.UnlockedNodes) {
            foreach (var mapNode in unlockedNodes)
            {
                var nodeContent = mapNode.Element.Content;
                bool hasBreach =  Settings.Highlights.HighlightBreaches && nodeContent.Any(x => x.Name.Contains("Breach"));
                bool hasDelirium = Settings.Highlights.HighlightDelirium && nodeContent.Any(x => x.Name.Contains("Delirium"));
                bool hasExpedition = Settings.Highlights.HighlightExpedition && nodeContent.Any(x => x.Name.Contains("Expedition"));
                bool hasRitual = Settings.Highlights.HighlightRitual && nodeContent.Any(x => x.Name.Contains("Ritual"));
                bool hasBoss = Settings.Highlights.HighlightBosses && nodeContent.Any(x => x.Name.Contains("Boss"));
                bool isUntaintedParadise = Settings.Highlights.HighlightUntaintedParadise && mapNode.Element.Area.Id.Contains("UntaintedParadise");
                bool isTrader = Settings.Highlights.HighlightTrader && mapNode.Element.Area.Id.Contains("Merchant");
                bool isCitadel = Settings.Highlights.HighlightTrader && mapNode.Element.Area.Name.Contains("Citadel");

                var ringCount = 0;
                
                if (hasBreach)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.breachColor);
                    ringCount++;
                }
                if (hasDelirium)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.deliriumColor);
                    ringCount++;
                }
                if (hasExpedition)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.expeditionColor);
                    ringCount++;
                }
                if (hasRitual)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.ritualColor);
                    ringCount++;
                }
                if (hasBoss)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.bossColor);
                    ringCount++;
                }
                if (isUntaintedParadise)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.untaintedParadiseColor);

                    if (Settings.Highlights.LineToParadise)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.untaintedParadiseColor);
                    
                    ringCount++;
                }
                if (isTrader)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.traderColor);

                    if (Settings.Highlights.LineToTrader)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.traderColor);

                    ringCount++;
                }
                if (isCitadel)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.citadelColor);

                    if (Settings.Highlights.LineToCitadel)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.citadelColor);

                    ringCount++;
                }

                
                if (Settings.Features.NodeHighlighting)
                    DrawMapNode(mapNode);

                if (Settings.Features.UnlockedNames)                
                    DrawMapName(mapNode);
                

            }
        }
        if (Settings.Features.LockedNodes) {
            foreach (var mapNode in lockedNodes)
            {
                var nodeContent = mapNode.Element.Content;
                bool hasBreach =  Settings.Highlights.HighlightBreaches && nodeContent.Any(x => x.Name.Contains("Breach"));
                bool hasDelirium = Settings.Highlights.HighlightDelirium && nodeContent.Any(x => x.Name.Contains("Delirium"));
                bool hasExpedition = Settings.Highlights.HighlightExpedition && nodeContent.Any(x => x.Name.Contains("Expedition"));
                bool hasRitual = Settings.Highlights.HighlightRitual && nodeContent.Any(x => x.Name.Contains("Ritual"));
                bool hasBoss = Settings.Highlights.HighlightBosses && nodeContent.Any(x => x.Name.Contains("Boss"));
                bool isUntaintedParadise = Settings.Highlights.HighlightUntaintedParadise && mapNode.Element.Area.Id.Contains("UntaintedParadise");
                bool isTrader = Settings.Highlights.HighlightTrader && mapNode.Element.Area.Id.Contains("Trader");
                bool isCitadel = Settings.Highlights.HighlightTrader && mapNode.Element.Area.Name.Contains("Citadel");

                var ringCount = 0;
                
                if (hasBreach)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.breachColor);
                    ringCount++;
                }
                if (hasDelirium)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.deliriumColor);
                    ringCount++;
                }
                if (hasExpedition)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.expeditionColor);
                    ringCount++;
                }
                if (hasRitual)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.ritualColor);
                    ringCount++;
                }
                if (hasBoss)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.bossColor);
                    ringCount++;
                }
                if (isUntaintedParadise)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.untaintedParadiseColor);

                    if (Settings.Highlights.LineToParadise)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.untaintedParadiseColor);
                    
                    ringCount++;
                }
                if (isTrader)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.traderColor);

                    if (Settings.Highlights.LineToTrader)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.traderColor);

                    ringCount++;
                }
                if (isCitadel)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.citadelColor);

                    if (Settings.Highlights.LineToCitadel)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.citadelColor);

                    ringCount++;
                }

                if (Settings.Features.NodeHighlighting)
                    DrawMapNode(mapNode);

                if (Settings.Features.LockedNames)                
                    DrawMapName(mapNode);
                


            }
        }
        
        if (Settings.Features.UnrevealedNodes) {
            foreach (var mapNode in invisibleNodes)
            {
                var nodeContent = mapNode.Element.Content;
                bool hasBreach =  Settings.Highlights.HighlightBreaches && nodeContent.Any(x => x.Name.Contains("Breach"));
                bool hasDelirium = Settings.Highlights.HighlightDelirium && nodeContent.Any(x => x.Name.Contains("Delirium"));
                bool hasExpedition = Settings.Highlights.HighlightExpedition && nodeContent.Any(x => x.Name.Contains("Expedition"));
                bool hasRitual = Settings.Highlights.HighlightRitual && nodeContent.Any(x => x.Name.Contains("Ritual"));
                bool hasBoss = Settings.Highlights.HighlightBosses && nodeContent.Any(x => x.Name.Contains("Boss"));
                bool isUntaintedParadise = Settings.Highlights.HighlightUntaintedParadise && mapNode.Element.Area.Id.Contains("UntaintedParadise");
                bool isTrader = Settings.Highlights.HighlightTrader && mapNode.Element.Area.Id.Contains("Merchant");
                bool isCitadel = Settings.Highlights.HighlightTrader && mapNode.Element.Area.Name.Contains("Citadel");

                var ringCount = 0;
                
                if (hasBreach)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.breachColor);
                    ringCount++;
                }
                if (hasDelirium)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.deliriumColor);
                    ringCount++;
                }
                if (hasExpedition)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.expeditionColor);
                    ringCount++;
                }
                if (hasRitual)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.ritualColor);
                    ringCount++;
                }
                if (hasBoss)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.bossColor);
                    ringCount++;
                }
                if (isUntaintedParadise)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.untaintedParadiseColor);

                    if (Settings.Highlights.LineToParadise)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.untaintedParadiseColor);
                    
                    ringCount++;
                }
                if (isTrader)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.traderColor);

                    if (Settings.Highlights.LineToTrader)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.traderColor);

                    ringCount++;
                }
                if (isCitadel)
                {
                    HighlightMapNode(mapNode, ringCount, Settings.Graphics.citadelColor);

                    if (Settings.Highlights.LineToCitadel)
                        DrawLineToMapNode(mapNode, zigguratNode, Settings.Graphics.citadelColor);

                    ringCount++;
                }

                if (Settings.Features.NodeHighlighting)
                    DrawMapNode(mapNode);

                if (Settings.Features.UnrevealedNames)                
                    DrawMapName(mapNode);

            }
        }

        if (Settings.Features.DebugMode)
        {
            foreach (var mapNode in mapNodes)
            {
                var text = mapNode.Address.ToString("X");
                Graphics.DrawText(text, mapNode.Element.GetClientRect().TopLeft, Color.Red);
            }
        }
    }

    

    private void HighlightMapNode(AtlasNodeDescription mapNode, int Count, Color color)
    {
        var radius = (Count * 5) + (mapNode.Element.GetClientRect().Right - mapNode.Element.GetClientRect().Left) / 2;
        Graphics.DrawCircle(mapNode.Element.GetClientRect().Center, radius, color, 4, 16);
    }

    private void DrawLineToMapNode(AtlasNodeDescription mapNode, AtlasNodeDescription fromNode, Color color)
    {
        Vector2 position = Vector2.Lerp(State.IngameUi.GetClientRect().Center, mapNode.Element.GetClientRect().Center, Settings.Highlights.DrawDistanceOnLineScale);
        if (Settings.Highlights.DrawDistanceOnLine) {
            string text = mapNode.Element.Area.Name;
            text += $" ({Vector2.Distance(State.IngameUi.GetClientRect().Center, mapNode.Element.GetClientRect().Center).ToString("0")})";
            
            DrawTextWithBackground(text, position, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        }

        Graphics.DrawLine(position, mapNode.Element.GetClientRect().Center, Settings.Highlights.MapLineWidth, color);
    }

    private void DrawMapNode(AtlasNodeDescription mapNode)
    {
        var map = Settings.MapHighlightSettings.Maps.FirstOrDefault(x => x.Value.Name == mapNode.Element.Area.Name && x.Value.Highlight == true).Value;

        if (map == null)
            return;

        var radius = 5 - (mapNode.Element.GetClientRect().Right - mapNode.Element.GetClientRect().Left) / 2;
        Graphics.DrawCircleFilled(mapNode.Element.GetClientRect().Center, radius, map.NodeColor, 32);
    }

    private void DrawMapName(AtlasNodeDescription mapNode)
    {
        if (Settings.Features.NameHighlighting) {            
            var map = Settings.MapHighlightSettings.Maps.FirstOrDefault(x => x.Value.Name == mapNode.Element.Area.Name && x.Value.Highlight == true).Value;

            if (map != null) {
                DrawTextWithBackground(map.Name.ToUpper(), mapNode.Element.GetClientRect().Center, map.NameColor, map.BackgroundColor, true, 10, 3);
                return;
            }
        }

        DrawTextWithBackground(mapNode.Element.Area.Name.ToUpper(), mapNode.Element.GetClientRect().Center, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 3);
        
    }
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

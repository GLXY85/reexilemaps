using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;

using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Helpers;
using ExileCore2.Shared.Nodes;
using ExileCore2.Shared.Enums;

using GameOffsets2.Native;

using ImGuiNET;

using RectangleF = ExileCore2.Shared.RectangleF;
using ExileMaps.Classes;

namespace ExileMaps;

public class ExileMapsCore : BaseSettingsPlugin<ExileMapsSettings>
{
    #region Declarations
    private int tickCount { get; set; }
    public static ExileMapsCore Main;

    private const string defaultMapsPath = "json\\maps.json";
    private const string defaultModsPath = "json\\mods.json";
    private const string defaultBiomesPath = "json\\biomes.json";
    private const string defaultContentPath = "json\\content.json";
    private const string ArrowPath = "textures\\arrow.png";
    private const string IconsFile = "Icons.png";
    
    public GameController Game => GameController;
    public IngameState State => Game.IngameState;
    public AtlasPanel AtlasPanel => State.IngameUi.WorldMap.AtlasPanel;
    private Vector2 screenCenter;
    public Dictionary<long, Node> mapCache = new Dictionary<long, Node>();
    public bool refreshCache = false;
    private bool refreshingCache = false;
    private float maxMapWeight = 20.0f;
    private float minMapWeight = -20.0f;
    private readonly object mapCacheLock = new object();
    private DateTime lastRefresh = DateTime.Now;
    private bool mapClosed = true;
    internal IntPtr iconsId;
    internal IntPtr arrowId;
    public bool WaypointPanelIsOpen = false;


    #endregion

    #region ExileCore Methods
    public override bool Initialise()
    {
        Main = this;        
        RegisterHotkey(Settings.Keybinds.RefreshMapCacheHotkey);
        RegisterHotkey(Settings.Features.DebugKey);
        RegisterHotkey(Settings.Keybinds.ToggleWaypointPanelHotkey);
        RegisterHotkey(Settings.Keybinds.AddWaypointHotkey);
        RegisterHotkey(Settings.Keybinds.DeleteWaypointHotkey);

        // RegisterHotkey(Settings.Pathfinding.SetCurrentLocationHotkey);
        // RegisterHotkey(Settings.Pathfinding.AddWaypointHotkey);

        LoadDefaultBiomes();
        LoadDefaultContentTypes();
        LoadDefaultMaps();
        LoadDefaultMods();

        Settings.Maps.Maps.CollectionChanged += (_, _) => { recalculateWeights(); };
        Settings.Maps.Maps.PropertyChanged += (_, _) => { recalculateWeights(); };
        Settings.Biomes.Biomes.PropertyChanged += (_, _) => { recalculateWeights(); };
        Settings.Biomes.Biomes.CollectionChanged += (_, _) => { recalculateWeights(); };
        Settings.MapContent.ContentTypes.CollectionChanged += (_, _) => { recalculateWeights(); };
        Settings.MapContent.ContentTypes.PropertyChanged += (_, _) => { recalculateWeights(); };
        Settings.MapMods.MapModTypes.CollectionChanged += (_, _) => { recalculateWeights(); };
        Settings.MapMods.MapModTypes.PropertyChanged += (_, _) => { recalculateWeights(); };

        
        Graphics.InitImage(IconsFile);
        iconsId = Graphics.GetTextureId(IconsFile);
        Graphics.InitImage("arrow.png", Path.Combine(DirectoryFullName, ArrowPath));
        arrowId = Graphics.GetTextureId("arrow.png");

        CanUseMultiThreading = true;

        return true;
    }
    public override void AreaChange(AreaInstance area)
    {
        refreshCache = true;
    }

    public override void Tick()
    {
        if (!AtlasPanel.IsVisible) {
            mapClosed = true;;
            return;
        }

        if (mapClosed)
            RefreshMapCache(true);

        mapClosed = false;
        
        screenCenter = Game.Window.GetWindowRectangle().Center - Game.Window.GetWindowRectangle().Location;
        
        if (AtlasPanel.IsVisible && AtlasPanel.Descriptions.Any(x => !mapCache.ContainsKey(x.Element.Address)))
            refreshCache = true;

        if (refreshCache && !refreshingCache && (DateTime.Now.Subtract(lastRefresh).TotalSeconds > Settings.Graphics.MapCacheRefreshRate || mapCache.Count == 0))
        {
            var job = new Job($"{nameof(ExileMaps)}RefreshCache", () =>
            {
                RefreshMapCache();
                refreshCache = false;
            });
            job.Start();
            
        }
        
        return;
    }

    public override void Render()
    {

        if (AtlasPanel.IsVisible && AtlasPanel.Descriptions.Any(x => !mapCache.ContainsKey(x.Element.Address)))
            refreshCache = true;

        CheckKeybinds();

        if (WaypointPanelIsOpen)
            DrawWaypointPanel();

        tickCount++;
        if (Settings.Graphics.RenderNTicks.Value % tickCount != 0) 
            return;  

        tickCount = 0;

        if (!AtlasPanel.IsVisible)
            return;

        if (mapCache.Count == 0) {
            refreshCache = true;
            return;
        }

        var mapNodes = AtlasPanel.Descriptions.Select(x => x.Element).ToList();

        // Get all map nodes within the specified range.
        try {
            mapNodes = mapNodes.Where(x => Vector2.Distance(screenCenter, x.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000)).ToList();//
        } catch (Exception e) {
            LogError("Error getting map nodes: " + e.Message);
            return;
        }

        // Filter out nodes based on settings.
        var selectedNodes = mapNodes
            .Where(x => ((Settings.Features.ProcessUnlockedNodes && x.IsUnlocked) ||
                        (Settings.Features.ProcessLockedNodes && !x.IsUnlocked) ||
                        (Settings.Features.ProcessHiddenNodes && !x.IsVisible)))
            .Where(x => IsOnScreen(x.GetClientRect().Center));
        
        foreach (var mapNode in selectedNodes) {
            try {
                if (!Settings.Features.DebugMode)
                    RenderNode(mapNode);
            } catch (Exception e) {
                LogError("Error rendering map node: " + e.Message + " - " + e.StackTrace);
            }
        }   

        try {
            string[] waypointNames = Settings.Maps.Maps.Where(x => x.Value.DrawLine).Select(x => x.Value.Name.Trim()).ToArray();
            if (waypointNames.Length > 0 && Settings.Features.DrawLines) {
                List<AtlasPanelNode> waypointNodes = AtlasPanel.Descriptions.Select(x => x.Element)
                        .Where(x => waypointNames.Contains(x.Area.Name.Trim()))
                        .Where(x => !x.IsVisited && !(!x.IsUnlocked && x.IsVisited))
                        .Where(x => Vector2.Distance(screenCenter, x.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000) || !Settings.Features.WaypointsUseAtlasRange).ToList();
                
                foreach(var waypointNode in waypointNodes) {
                    try {
                        DrawWaypointLine(waypointNode);
                    } catch (Exception e) {
                        LogError($"Error drawing waypoint line for map {waypointNode.Area.Name.Trim()}: " + e.Message + "\n" + e.StackTrace);
                    }
                }
            }
        } catch (Exception e) {
            LogError("Error drawing waypoint lines: " + e.Message + "\n" + e.StackTrace);
        }

        try {
            foreach (var (coordinates,waypoint) in Settings.Waypoints.Waypoints) {
                try {
                    DrawWaypoint(waypoint);
                    DrawWaypointArrow(waypoint);
                } catch (Exception e) {
                    LogError("Error drawing waypoint: " + e.Message + "\n" + e.StackTrace);
                }
            }
        }

        catch (Exception e) {
            LogError("Error drawing waypoints: " + e.Message + "\n" + e.StackTrace);
        }
        if (Settings.Features.DebugMode) {
            foreach (var mapNode in AtlasPanel.Descriptions) {
                try {
                    DrawDebugging(mapNode.Element);
                } catch (Exception e) {
                    LogError("Error drawing debug info: " + e.Message + "\n" + e.StackTrace);
                }
            }
        }
    }
    #endregion

    #region Keybinds
    private void CheckKeybinds() {
        if (!AtlasPanel.IsVisible)
            return;

        if (Settings.Keybinds.RefreshMapCacheHotkey.PressedOnce()) {        
            LogMessage("Refreshing Map Cache");
            var timer = new Stopwatch();
            timer.Start();
            RefreshMapCache();
            timer.Stop();
            LogMessage($"Map cache refreshed in {timer.ElapsedMilliseconds}ms");
        }

        if (Settings.Features.DebugKey.PressedOnce())        
            DoDebugging();

        if (Settings.Keybinds.ToggleWaypointPanelHotkey.PressedOnce()) {  
            WaypointPanelIsOpen = !WaypointPanelIsOpen;
        }

        if (Settings.Keybinds.AddWaypointHotkey.PressedOnce())        
            AddWaypoint(GetClosestNodeToCursor());

        if (Settings.Keybinds.DeleteWaypointHotkey.PressedOnce())        
            RemoveWaypoint(GetClosestNodeToCursor());


    }
    #endregion

    #region Load Defaults
    private void LoadDefaultMods() {
        try {
            if (Settings.MapMods.MapModTypes == null)
                Settings.MapMods.MapModTypes = new ObservableDictionary<string, Mod>();

            var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultModsPath));
            var mods = JsonSerializer.Deserialize<Dictionary<string, Mod>>(jsonFile);

            foreach (var mod in mods)
                if (!Settings.MapMods.MapModTypes.ContainsKey(mod.Key))
                    Settings.MapMods.MapModTypes.Add(mod.Key, mod.Value);

            LogMessage("Loaded Mods");
        } catch (Exception e) {
            LogError("Error loading default mod: " + e.Message);
        }
    }

    private void LoadDefaultBiomes() {
        try {
        if (Settings.Biomes.Biomes == null)
            Settings.Biomes.Biomes = new ObservableDictionary<string, Biome>();

        var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultBiomesPath));
        var biomes = JsonSerializer.Deserialize<Dictionary<string, Biome>>(jsonFile);

        foreach (var biome in biomes)
            if (!Settings.Biomes.Biomes.ContainsKey(biome.Key)) 
                Settings.Biomes.Biomes.Add(biome.Key, biome.Value);  

        LogMessage("Loaded Biomes");
        } catch (Exception e) {
            LogError("Error loading default biomes: " + e.Message);
        }
            
    }

    private void LoadDefaultContentTypes() {
        try {
            if (Settings.MapContent.ContentTypes == null)
                Settings.MapContent.ContentTypes = new ObservableDictionary<string, Content>();

            var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultContentPath));
            var contentTypes = JsonSerializer.Deserialize<Dictionary<string, Content>>(jsonFile);

            foreach (var content in contentTypes)
                if (!Settings.MapContent.ContentTypes.ContainsKey(content.Key)) 
                    Settings.MapContent.ContentTypes.Add(content.Key, content.Value);   

            LogMessage("Loaded Content Types");
        } catch (Exception e) {
            LogError("Error loading default content types: " + e.Message);
        }

    }
    
    public void LoadDefaultMaps()
    {
        try {
            if (Settings.Maps.Maps == null)
                Settings.Maps.Maps = new ObservableDictionary<string, Map>();

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

    #endregion
    
    private static void RegisterHotkey(HotkeyNode hotkey)
    {
        Input.RegisterKey(hotkey);
        hotkey.OnValueChanged += () => { Input.RegisterKey(hotkey); };
    }

    
    #region Map Processing
    
    private void RenderNode(AtlasPanelNode mapNode)
    {
        var ringCount = 0;           

        try {
            ringCount += HighlightMapContent(mapNode, ringCount, "Breach");
            ringCount += HighlightMapContent(mapNode, ringCount, "Delirium");
            ringCount += HighlightMapContent(mapNode, ringCount, "Expedition");
            ringCount += HighlightMapContent(mapNode, ringCount, "Ritual");
            ringCount += HighlightMapContent(mapNode, ringCount, "Boss");
            ringCount += HighlightMapContent(mapNode, ringCount, "Corruption");
            ringCount += HighlightMapContent(mapNode, ringCount, "Irradiated");
            DrawConnections(mapNode);
            DrawMapNode(mapNode);            
            DrawTowerMods(mapNode);
            DrawMapName(mapNode);
            DrawWeight(mapNode);
            
            

        } catch (Exception e) {
            LogError("Error drawing map node: " + e.Message + " - " + e.StackTrace);
            return;
        }
    }
    #endregion

    #region Debugging
    private void DoDebugging() {
        // get node closest to cursor
        var cursorElement = State.UIHoverElement;
        var closestNode = AtlasPanel.Descriptions.OrderBy(x => Vector2.Distance(cursorElement.GetClientRect().Center, x.Element.GetClientRect().Center)).FirstOrDefault();
        if (!mapCache.ContainsKey(closestNode.Element.Address))
            return;
        Node cachedNode = mapCache[closestNode.Element.Address];
        LogMessage(cachedNode.ToString());

    }

    private void DrawDebugging(AtlasPanelNode mapNode) {
        if (!mapCache.ContainsKey(mapNode.Address))            
            return;

        Node cachedNode = mapCache[mapNode.Address];
        var position = mapNode.GetClientRect().Center + new Vector2(0, 35);
        DrawCenteredTextWithBackground(cachedNode.ParentAddress.ToString("X"), position, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        position += new Vector2(0, 27);
        DrawCenteredTextWithBackground(cachedNode.Coordinate.ToString(), position, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        position += new Vector2(0, 27);
        string debugText = "";
                                    
        debugText = $"N: {cachedNode.Neighbors.Count}";                    
        debugText += $", W: {cachedNode.Weight.ToString("0.000")}";

        // get distinct effect sources
        var towers = cachedNode.Effects.SelectMany(x => x.Value.Sources).Distinct().Count();
        if (towers > 0) {
            debugText += $", T: {towers}";
            var effects = cachedNode.Effects.Distinct().Count();
            if (effects > 0)
                debugText += $", E: {effects}";
        }

        DrawCenteredTextWithBackground(debugText, position, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
    
    }

    #endregion

    private AtlasNodeDescription GetClosestNodeToCursor() {
        return AtlasPanel.Descriptions.OrderBy(x => Vector2.Distance(State.UIHoverElement.GetClientRect().Center, x.Element.GetClientRect().Center)).FirstOrDefault();
    }

    #region Map Cache
    public void RefreshMapCache(bool clearCache = false)
    {
        refreshingCache = true;

        lock (mapCacheLock)
        {
            mapCache.Clear();
        }
        

        List<AtlasNodeDescription> atlasNodes = AtlasPanel.Descriptions.ToList();
    
        foreach (var node in atlasNodes) {
            if (!mapCache.ContainsKey(node.Element.Address))
                CacheNewMapNode(node);
            else
                RefreshCachedMapNode(node);
        }

        recalculateWeights();

        refreshingCache = false;
        refreshCache = false;
        lastRefresh = DateTime.Now;
    }
    
    private void recalculateWeights() {

        maxMapWeight = mapCache.Values.Where(x => !x.IsVisited).Select(x => x.Weight).OrderByDescending(x => x).Skip(15).FirstOrDefault();
        minMapWeight = mapCache.Values.Where(x => !x.IsVisited).Select(x => x.Weight).OrderBy(x => x).Skip(5).FirstOrDefault();
     
    }

    private void CacheNewMapNode(AtlasNodeDescription node)
    {
        Node newNode = new Node();

        newNode.IsUnlocked = node.Element.IsUnlocked;
        newNode.IsVisible = node.Element.IsVisible;
        newNode.IsVisited = node.Element.IsVisited || (!node.Element.IsUnlocked && node.Element.IsVisited);
        newNode.IsHighlighted = node.Element.isHighlighted;
        newNode.IsActive = node.Element.IsActive;
        newNode.ParentAddress = node.Address;
        newNode.Address = node.Element.Address;
        newNode.Coordinate = node.Coordinate;
        newNode.Name = node.Element.Area.Name;
        newNode.Id = node.Element.Area.Id;

        // Get connections from this node to other nodes
        var connectionPoints = AtlasPanel.Points.FirstOrDefault(x => x.Item1 == newNode.Coordinate);
        newNode.Point = (connectionPoints.Item1, connectionPoints.Item2, connectionPoints.Item3, connectionPoints.Item4, connectionPoints.Item5);

        var connectionArray = new[] { connectionPoints.Item2, connectionPoints.Item3, connectionPoints.Item4, connectionPoints.Item5 };
        foreach (var vector in connectionArray) {
            if (vector == default)
                continue;

            if (!newNode.Neighbors.ContainsKey(vector))
                newNode.Neighbors.Add(vector, null);
        
        // Get connections from other nodes to this node
        var neighborConnections = AtlasPanel.Points.Where(x => x.Item2 == newNode.Coordinate || x.Item3 == newNode.Coordinate || x.Item4 == newNode.Coordinate || x.Item5 == newNode.Coordinate);
        foreach (var point in neighborConnections)
            if (!newNode.Neighbors.ContainsKey(point.Item1))
                newNode.Neighbors.Add(point.Item1, null);
        }
        
        // We only care about weights if the node is not visited
        
        // Get base weight for the map 
        float weight = 10.0f;

        try {
            weight += !newNode.IsVisited ? Settings.Maps.Maps.FirstOrDefault(x => x.Value.Name.Trim() == node.Element.Area.Name.Trim()).Value.Weight : 0;
        } catch (Exception e) {
            LogError($"Error getting weight for map type {node.Element.Area.Name.Trim()}: " + e.Message);
        }

        // Set Content
        // TODO: Get corrupted status from children[0].element.children.texturename
        if (!newNode.IsVisited) {
            // Check if the map has content
            try {
                if (node.Element.Content != null) {
                    var mapContent = node.Element.Content.ToList();
                    var iconList = node.Element.GetChildAtIndex(0);                    
                    bool hasCorruption = iconList.Children.Any(x => x.TextureName.Contains("Corrupt"));
                    
                    if (hasCorruption) {
                        newNode.MapContent.Add(Settings.MapContent.ContentTypes["Corruption"]);
                        weight += !newNode.IsVisited ? Settings.MapContent.ContentTypes["Corruption"].Weight : 0;
                    }

                    foreach(var content in mapContent) {        
                        if (content == null )
                            continue;
                        
                        newNode.MapContent.Add(Settings.MapContent.ContentTypes[content.Name]);

                        weight += !newNode.IsVisited ? Settings.MapContent.ContentTypes[content.Name].Weight : 0;
                    }
                }
            } catch (Exception e) {
                LogError($"Error getting Content for map type {node.Address.ToString("X")}: " + e.Message);
            }
            

            // Set Biomes
            try {
                var biomes = Settings.Maps.Maps.FirstOrDefault(m => m.Value.Name.Trim() == node.Element.Area.Name.Trim() && m.Value.Biomes != null).Value.Biomes.Where(b => b != "").ToList();               
                foreach (var biome in biomes.Where(x => !Settings.Biomes.Biomes.ContainsKey(x))) {                    
                    newNode.Biomes.Add(Settings.Biomes.Biomes[biome]);

                    weight += !newNode.IsVisited ? Settings.Biomes.Biomes[biome].Weight : 0;
                }
            }   catch (Exception e) {
                LogError($"Error getting Biomes for map type {node.Element.Area.Name.Trim()}: " + e.Message);
            }
        }
    
        try {
            // Get Tower Effects within Range
            var nearbyEffects = AtlasPanel.EffectSources.Where(x => Vector2.Distance(x.Coordinate, node.Coordinate) <= 11);

            foreach(var source in nearbyEffects) {
                foreach(var effect in source.Effects.Where(x => Settings.MapMods.MapModTypes.ContainsKey(x.ModId.ToString()) && x.Value != 0)) {
                    var effectKey = effect.ModId.ToString();
                    var requiredContent = Settings.MapMods.MapModTypes[effectKey].RequiredContent;
                    
                    if (!newNode.Effects.ContainsKey(effectKey)) {
                        Effect newEffect = new Effect() {
                            Name = Settings.MapMods.MapModTypes[effectKey].Name,
                            Description = Settings.MapMods.MapModTypes[effectKey].Description,
                            Value1 = effect.Value,
                            ID = effect.ModId,
                            ShowOnMap = (Settings.MapMods.MapModTypes[effectKey].ShowOnMap && 
                                        !(Settings.MapMods.OnlyDrawApplicableMods && 
                                        !String.IsNullOrEmpty(requiredContent) && 
                                        (newNode.MapContent == null || !newNode.MapContent.Any(x => x.Name.Contains(requiredContent))))),
                            Sources = new List<Vector2i> { source.Coordinate }
                        };
                        
                        newNode.Effects.Add(effectKey, newEffect);
                    } else {
                        if (newNode.Name != "Lost Towers" || !newNode.IsVisited)
                            newNode.Effects[effectKey].Value1 += effect.Value;

                        newNode.Effects[effectKey].Sources.Add(source.Coordinate);
                    }                                       

                    newNode.Effects[effectKey].Weight = Settings.MapMods.MapModTypes[effectKey].Weight * newNode.Effects[effect.ModId.ToString()].Value1;

                    if (!String.IsNullOrEmpty(requiredContent) && (newNode.MapContent == null || !newNode.MapContent.Any(x => x.Name.Contains(requiredContent))))
                        continue;

                    weight += !newNode.IsVisited ? newNode.Effects[effectKey].Weight : 0;
                }
            }
        } catch (Exception e) {
            LogError($"Error getting Tower Effects for map {newNode.Coordinate}: " + e.Message);
        }

        newNode.Weight = !newNode.IsVisited ? weight : 500;;  


        lock (mapCacheLock)
        {
            if (!mapCache.ContainsKey(node.Element.Address))
                mapCache.Add(node.Element.Address, newNode);
        }
    }

    private void RefreshCachedMapNode(AtlasNodeDescription node)
    {
        if (!mapCache.ContainsKey(node.Element.Address))
            return;

        Node cachedNode = mapCache[node.Element.Address];

        cachedNode.IsUnlocked = node.Element.IsUnlocked;
        cachedNode.IsVisible = node.Element.IsVisible;
        cachedNode.IsVisited = node.Element.IsVisited || (!node.Element.IsUnlocked && node.Element.IsVisited);
        cachedNode.IsHighlighted = node.Element.isHighlighted;
        cachedNode.IsActive = node.Element.IsActive;
        cachedNode.ParentAddress = node.Address;

        // Get base weight for the map 
        float weight = 0.0f;

        try {
            weight -= !cachedNode.IsVisited ? Settings.Maps.Maps.FirstOrDefault(x => x.Value.Name.Trim() == node.Element.Area.Name.Trim()).Value.Weight : 0;
            
        } catch (Exception e) {
            LogError($"Error getting weight for map type {node.Element.Area.Name.Trim()}: " + e.Message);
        }
        
        if (!cachedNode.IsVisited) {
            // Set Content
            // TODO: Get corrupted status from children[0].element.children.texturename
            var mapContent = node.Element.Content.Select(x => x.Name).ToList();
            foreach(var content in mapContent.Where(x => !Settings.MapContent.ContentTypes.ContainsKey(x))) {           
                cachedNode.MapContent.Add(Settings.MapContent.ContentTypes[content]);

                weight += !cachedNode.IsVisited ? Settings.MapContent.ContentTypes[content].Weight : 0;
            }

            // Set Biomes
            try {
                var biomes = Settings.Maps.Maps.FirstOrDefault(m => m.Value.Name.Trim() == node.Element.Area.Name.Trim() && m.Value.Biomes != null).Value.Biomes.Where(b => b != "").ToList();               
                foreach (var biome in biomes)                  
                    weight += !cachedNode.IsVisited ? Settings.Biomes.Biomes[biome].Weight : 0;
                
            }   catch (Exception e) {
                LogError($"Error getting Biomes for map type {node.Element.Area.Name.Trim()}: " + e.Message);
            }
        }

        try {
            var nearbyEffects = AtlasPanel.EffectSources.Where(x => Vector2.Distance(x.Coordinate, node.Coordinate) <= 11);
            foreach(var source in nearbyEffects) {
                foreach(var effect in source.Effects.Where(x => Settings.MapMods.MapModTypes.ContainsKey(x.ModId.ToString()) && x.Value != 0)) {
                    var effectKey = effect.ModId.ToString();
                    if (!cachedNode.Effects.ContainsKey(effectKey)) {
                        Effect newEffect = new Effect() {
                            Name = Settings.MapMods.MapModTypes[effectKey].Name,
                            Description = Settings.MapMods.MapModTypes[effectKey].Description,
                            Value1 = effect.Value,
                            ID = effect.ModId,
                            ShowOnMap = Settings.MapMods.MapModTypes[effectKey].ShowOnMap,
                            Sources = new List<Vector2i> { source.Coordinate }
                        };
                        
                        cachedNode.Effects.Add(effectKey, newEffect);
                    } else { 
                        if (cachedNode.Name != "Lost Towers" || !cachedNode.IsVisited)
                            cachedNode.Effects[effectKey].Value1 += effect.Value;
                        cachedNode.Effects[effectKey].Sources.Add(source.Coordinate);
                    }                                       

                    cachedNode.Effects[effectKey].Weight = Settings.MapMods.MapModTypes[effectKey].Weight * cachedNode.Effects[effect.ModId.ToString()].Value1;
                    weight += !cachedNode.IsVisited ? cachedNode.Effects[effectKey].Weight : 0;
                }
            }
        } catch (Exception e) {
            LogError($"Error getting Tower Effects for map {cachedNode.Coordinate}: " + e.Message);
        }

        cachedNode.Weight = !cachedNode.IsVisited ? weight : 500;
    } 
    #endregion

    #region Map Drawing
    /// <summary>
    /// Draws lines between a map node and its connected nodes on the atlas.
    /// </summary>
    /// <param name="WorldMap">The atlas panel containing the map nodes and their connections.</param>
    /// <param name="mapNode">The map node for which connections are to be drawn.</param>
    /// 
    private void DrawConnections(AtlasPanelNode mapNode)
    {
        if (!mapCache.ContainsKey(mapNode.Address) ||
        (!mapNode.IsVisible && !Settings.Features.DrawHiddenNodeConnections))
            return;

        var mapConnections = mapCache[mapNode.Address].Point;

        if (mapConnections.Equals(default))
            return;

        var connectionArray = new[] { mapConnections.Item2, mapConnections.Item3, mapConnections.Item4, mapConnections.Item5 };

        foreach (Vector2i coordinates in connectionArray)
        {
            if (coordinates == default)
                continue;

            AtlasNodeDescription destinationNode = AtlasPanel.Descriptions.FirstOrDefault(x => x.Coordinate == coordinates);
            if (destinationNode != null)
            {
                if (!Settings.Features.DrawVisitedNodeConnections && (destinationNode.Element.IsVisited || mapNode.IsVisited))
                    continue;
                
                var destinationPos = destinationNode.Element.GetClientRect();

                var sourcePos = mapNode.GetClientRect();
                if (!IsOnScreen(destinationPos.Center) || !IsOnScreen(sourcePos.Center))
                    continue;
                


                if (Settings.Graphics.DrawGradientLines) {
                    Color sourceColor = mapNode.IsVisited ? Settings.Graphics.VisitedLineColor : mapNode.IsUnlocked ? Settings.Graphics.UnlockedLineColor : Settings.Graphics.LockedLineColor;
                    Color destinationColor = destinationNode.Element.IsVisited ? Settings.Graphics.VisitedLineColor : destinationNode.Element.IsUnlocked ? Settings.Graphics.UnlockedLineColor : Settings.Graphics.LockedLineColor;
                    DrawGradientLine(sourcePos.Center, destinationPos.Center, sourceColor, destinationColor);
                } else {
                var color = Settings.Graphics.LockedLineColor;

                if (destinationNode.Element.IsUnlocked || mapNode.IsUnlocked)
                    color = Settings.Graphics.UnlockedLineColor;
                
                if (destinationNode.Element.IsVisited && mapNode.IsVisited)
                    color = Settings.Graphics.VisitedLineColor;
                    Graphics.DrawLine(sourcePos.Center, destinationPos.Center, Settings.Graphics.MapLineWidth, color);
                }
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
    private int HighlightMapContent(AtlasPanelNode mapNode, int Count, string Content)
    {
        var currentPosition = mapNode.GetClientRect();
 
        if (!IsOnScreen(currentPosition.Center) || !mapCache.ContainsKey(mapNode.Address))
            return 0;

        Node cachedNode = mapCache[mapNode.Address];
        
        if ((!Settings.MapContent.HighlightLockedNodes && !mapNode.IsUnlocked) || 
        (!Settings.MapContent.HighlightUnlockedNodes && mapNode.IsUnlocked) || 
        (!Settings.MapContent.HighlightHiddenNodes && !mapNode.IsVisible) ||         
        !cachedNode.MapContent.Any(x => x.Name.Contains(Content)) ||
        mapNode.IsVisited)
            return 0;

        var contentSettings = Settings.MapContent.ContentTypes.FirstOrDefault(x => x.Key == Content).Value;

        if (contentSettings == null || !contentSettings.Highlight)
            return 0;

        var radius = ((Count * Settings.Graphics.RingWidth) + 1) + (((currentPosition.Right - currentPosition.Left) / 2) * Settings.Graphics.RingRadius);
        Graphics.DrawCircle(currentPosition.Center, radius, contentSettings.Color, Settings.Graphics.RingWidth, 16);

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
    private void DrawWaypointLine(AtlasPanelNode mapNode)
    {
        var currentPosition = mapNode.GetClientRect();
        if (!Settings.Features.DrawLines || mapNode.IsVisited)
            return;

        if (State.IngameUi.OpenLeftPanel.IsVisible && currentPosition.Center.X < State.IngameUi.OpenLeftPanel.GetClientRect().Right ||
            State.IngameUi.OpenRightPanel.IsVisible && currentPosition.Center.X > State.IngameUi.OpenRightPanel.GetClientRect().Left)
            return;

        var map = Settings.Maps.Maps.FirstOrDefault(x => x.Value.Name.Trim() == mapNode.Area.Name.Trim() && x.Value.DrawLine == true).Value;
        
        if (map == null)
            return;

        var color = map.NodeColor;
        var distance = Vector2.Distance(screenCenter, currentPosition.Center);

        if (distance < 400)
            return;

        // Position for label and start of line.
        Vector2 position = Vector2.Lerp(screenCenter, currentPosition.Center, Settings.Graphics.LabelInterpolationScale);
        // Draw the line from the center(ish) of the screen to the center of the map node.

        Graphics.DrawLine(position, currentPosition.Center, Settings.Graphics.MapLineWidth, color);

        // If labels are enabled, draw the node name and the distance to the node.
        if (Settings.Features.DrawLineLabels) {
            string text = mapNode.Area.Name;
            text += $" ({Vector2.Distance(screenCenter, currentPosition.Center).ToString("0")})";
            
            DrawCenteredTextWithBackground(text, position, map.NameColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        }
        
    }
    
    /// Draws a highlighted circle around a map node on the atlas if the node is configured to be highlighted.
    /// </summary>
    /// <param name="mapNode">The atlas node description containing information about the map node to be drawn.</param>   
    private void DrawMapNode(AtlasPanelNode mapNode)
    {
        var currentPosition = mapNode.GetClientRect();
        if (!IsOnScreen(currentPosition.Center))
            return;

        if (!Settings.Maps.HighlightMapNodes || mapNode.IsVisited)
            return;

        var radius = ((currentPosition.Right - currentPosition.Left) / 4) * Settings.Graphics.NodeRadius;
        var color = Color.White;

        if (Settings.Maps.ColorNodesByWeight) {
            if (mapCache.ContainsKey(mapNode.Address)) {
                var weight = mapCache[mapNode.Address].Weight;
                // iterpolate the color based on the weight
                try {
                    color = ColorUtils.InterpolateColor(Settings.Maps.BadNodeColor,Settings.Maps.GoodNodeColor, (weight - minMapWeight) / (maxMapWeight - minMapWeight));
                } catch (Exception e) {
                    LogError($"Error interpolating color: {weight} - {minMapWeight} - {maxMapWeight}: {e.Message}");
                }
                
            }
        } else {
            var map = Settings.Maps.Maps.FirstOrDefault(x => x.Value.Name.Trim() == mapNode.Area.Name && x.Value.Highlight == true).Value;

            if (map == null) 
                return;
            
            color = map.NodeColor;
            
        }
        Graphics.DrawCircleFilled(currentPosition.Center, radius, color, 16);
    }
    private void DrawWeight(AtlasPanelNode mapNode)
    {
        var currentPosition = mapNode.GetClientRect();
        if (!IsOnScreen(currentPosition.Center))
            return;

        if (!Settings.Maps.DrawWeightOnMap ||
            (!mapNode.IsVisible && !Settings.Labels.LabelHiddenNodes) ||
            (mapNode.IsUnlocked && !Settings.Labels.LabelUnlockedNodes) ||
            (!mapNode.IsUnlocked && !Settings.Labels.LabelLockedNodes) ||
            mapNode.IsVisited)
            return;

        if (!mapCache.ContainsKey(mapNode.Address))
            return;

        Node cachedNode = mapCache[mapNode.Address];       

        Color backgroundColor = Settings.Graphics.BackgroundColor;
        float weight = (cachedNode.Weight - minMapWeight) / (maxMapWeight - minMapWeight);        
        Color color = ColorUtils.InterpolateColor(Settings.Maps.BadNodeColor,Settings.Maps.GoodNodeColor, weight);
         
        float offsetX = (Graphics.MeasureText(mapNode.Area.Name.ToUpper()).X / 2) + 30;
        Vector2 position = new Vector2(currentPosition.Center.X + offsetX, currentPosition.Center.Y);

        DrawCenteredTextWithBackground($"{(int)((weight*100))}%", position, color, backgroundColor, true, 10, 3);
    }
    /// <summary>
    /// Draws the name of the map on the atlas.
    /// </summary>
    /// <param name="mapNode">The atlas node description containing information about the map.</param>
    private void DrawMapName(AtlasPanelNode mapNode)
    {
        var currentPosition = mapNode.GetClientRect();
        if (!IsOnScreen(currentPosition.Center))
            return;

        if (!Settings.Labels.DrawNodeLabels ||
            (!mapNode.IsVisible && !Settings.Labels.LabelHiddenNodes) ||
            (mapNode.IsUnlocked && !Settings.Labels.LabelUnlockedNodes) ||
            (!mapNode.IsUnlocked && !Settings.Labels.LabelLockedNodes) ||
            mapNode.IsVisited)
            return;

        var fontColor = Settings.Graphics.FontColor;
        var backgroundColor = Settings.Graphics.BackgroundColor;

        if (Settings.Labels.NameHighlighting) {            
            var map = Settings.Maps.Maps.FirstOrDefault(x => x.Value.Name.Trim() == mapNode.Area.Name && x.Value.Highlight == true).Value;

            if (map != null) {
                fontColor = map.NameColor;
                backgroundColor = map.BackgroundColor;
            }
        }

        

        DrawCenteredTextWithBackground(mapNode.Area.Name.ToUpper(), currentPosition.Center, fontColor, backgroundColor, true, 10, 3);
    }

    private void DrawTowerMods(AtlasPanelNode mapNode)
    {
        var currentPosition = mapNode.GetClientRect();
        if (!IsOnScreen(currentPosition.Center) || !mapCache.ContainsKey(mapNode.Address))
            return;

        Node cachedNode = mapCache[mapNode.Address];
        string mapName = mapNode.Area.Name.Trim();
        if (cachedNode == null || (mapName == "Lost Towers" && !Settings.MapMods.ShowOnTowers) || (mapName != "Lost Towers" && !Settings.MapMods.ShowOnMaps))    
            return; 

        Dictionary<string, Color> mods = new Dictionary<string, Color>();

        var effects = new List<Effect>();
        if (mapName == "Lost Towers") {            
            if (Settings.MapMods.ShowOnTowers) {                
                effects = cachedNode.Effects.Where(x => x.Value.Sources.Contains(cachedNode.Coordinate)).Select(x => x.Value).ToList();

                if (effects.Count == 0 && mapNode.IsVisited)
                    DrawCenteredTextWithBackground("MISSING TABLET", currentPosition.Center + new Vector2(0, Settings.MapMods.MapModOffset), Color.Red, Settings.Graphics.BackgroundColor, true, 10, 4);
                }
        } else {
            if (Settings.MapMods.ShowOnMaps && !mapNode.IsVisited) {
                effects = cachedNode.Effects.Where(x => x.Value.ShowOnMap).Select(x => x.Value).ToList();
            }
        }

        if (effects.Count == 0)
            return;

        foreach (var effect in effects) {
            mods.Add(effect.ToString(), Settings.MapMods.MapModTypes[effect.ID.ToString()].Color);
        }

        DrawMapModText(mods, currentPosition.Center);
    }
    private void DrawMapModText(Dictionary<string, Color> mods, Vector2 position)
    {      
        using (Graphics.SetTextScale(Settings.MapMods.MapModScale)) {
            string fullText = string.Join("\n", mods.Select(x => $"{x.Key}"));
            var boxSize = Graphics.MeasureText(fullText) + new Vector2(10, 10);
            var lineHeight = Graphics.MeasureText("A").Y;
            position = position - new Vector2(boxSize.X / 2, boxSize.Y / 2);

            // offset the box below the node
            position += new Vector2(0, (boxSize.Y / 2) + Settings.MapMods.MapModOffset);
            
            if (!IsOnScreen(boxSize + position))
                return;

            Graphics.DrawBox(position, boxSize + position, Settings.Graphics.BackgroundColor, 5.0f);

            position += new Vector2(5, 5);

            foreach (var mod in mods)
            {
                Graphics.DrawText(mod.Key, position, mod.Value);
                position += new Vector2(0, lineHeight);
            }
        }
    }

    // private void DrawBiomes(AtlasPanelNode mapNode)
    // {
    //     var currentPosition = mapNode.GetClientRect();
    //     if (!IsOnScreen(currentPosition.Center) || !mapCache.ContainsKey(mapNode.Address))
    //         return;

    //     Node cachedNode = mapCache[mapNode.Address];
    //     string mapName = mapNode.Area.Name.Trim();
    //     if (cachedNode == null || !Settings.Biomes.ShowBiomes || cachedNode.Biomes.Count == 0)    
    //         return; 

    //     Dictionary<string, Color> biomes = new Dictionary<string, Color>();

    //     var biomeList = new List<Biome>();
    //     if (Settings.Biomes.ShowBiomes && !mapNode.IsVisited) {
    //         biomeList = cachedNode.Biomes;
    //     }

    //     foreach (var biome in biomeList) {
    //         biomes.Add(biome.ToString(), Settings.Biomes.Biomes[biome.ToString()].Color);
    //     }

    //     DrawBiomeText(biomes, currentPosition.Center);
    // }

    // private void DrawBiomeText(Dictionary<string, Color> biomes, Vector2 position)
    // {      
    //     using (Graphics.SetTextScale(Settings.Biomes.BiomeScale)) {
    //         string fullText = string.Join("\n", biomes.Select(x => $"{x.Key}"));
    //         var boxSize = Graphics.MeasureText(fullText) + new Vector2(10, 10);
    //         var lineHeight = Graphics.MeasureText("A").Y;
    //         position = position - new Vector2(boxSize.X / 2, boxSize.Y / 2);

    //         // offset the box below the node
    //         position += new Vector2(0, (boxSize.Y / 2) + Settings.Biomes.BiomeOffset);
            
    //         if (!IsOnScreen(boxSize + position))
    //             return;

    //         Graphics.DrawBox(position, boxSize + position, Settings.Graphics.BackgroundColor, 5.0f);

    //         position += new Vector2(5, 5);

    //         foreach (var biome in biomes)
    //         {
    //             Graphics.DrawText(biome.Key, position, biome.Value);
    //             position += new Vector2(0, lineHeight);
    //         }
    //     }
    // }
    #endregion

    #region Misc Drawing
    /// <summary>
    /// Draws text with a background color at the specified position.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="position">The position to draw the text at.</param>
    /// <param name="textColor">The color of the text.</param>
    /// <param name="backgroundColor">The color of the background.</param>
    /// Yes, I know exilecore has this built in, but I wanted padding and rounded corners.

    private void DrawCenteredTextWithBackground(string text, Vector2 position, Color color, Color backgroundColor, bool center = false, int xPadding = 0, int yPadding = 0)
    {
        if (!IsOnScreen(position))
            return;

        var boxSize = Graphics.MeasureText(text);

        boxSize += new Vector2(xPadding, yPadding);    

        if (center)
            position = position - new Vector2(boxSize.X / 2, boxSize.Y / 2);

        Graphics.DrawBox(position, boxSize + position, backgroundColor, 5.0f);       

        position += new Vector2(xPadding / 2, yPadding / 2);

        Graphics.DrawText(text, position, color);
    }

    private void DrawRotatedImage(IntPtr textureId, Vector2 position, Vector2 size, float angle, Color color)
    {
        Vector2 center = position + size / 2;

        float cosTheta = (float)Math.Cos(angle);
        float sinTheta = (float)Math.Sin(angle);

        Vector2 RotatePoint(Vector2 point)
        {
            Vector2 translatedPoint = point - center;
            Vector2 rotatedPoint = new Vector2(
                translatedPoint.X * cosTheta - translatedPoint.Y * sinTheta,
                translatedPoint.X * sinTheta + translatedPoint.Y * cosTheta
            );
            return rotatedPoint + center;
        }

        Vector2 topLeft = RotatePoint(position);
        Vector2 topRight = RotatePoint(position + new Vector2(size.X, 0));
        Vector2 bottomRight = RotatePoint(position + size);
        Vector2 bottomLeft = RotatePoint(position + new Vector2(0, size.Y));


        Graphics.DrawQuad(textureId, topLeft, topRight, bottomRight, bottomLeft, color);
        }
        private void DrawGradientLine(Vector2 start, Vector2 end, Color startColor, Color endColor)
    {
        int segments = 10; // Number of segments to create the gradient effect
        Vector2 direction = (end - start) / segments;

        for (int i = 0; i < segments; i++)
        {
            Vector2 segmentStart = start + direction * i;
            Vector2 segmentEnd = start + direction * (i + 1);

            float t = (float)i / segments;
            Color segmentColor = ColorUtils.InterpolateColor(startColor, endColor, t);

            Graphics.DrawLine(segmentStart, segmentEnd, Settings.Graphics.MapLineWidth, segmentColor);
        }
    }

    #endregion
    
    #region Helper Functions

    private bool IsOnScreen(Vector2 position)
    {
        var screen = new RectangleF
        {
            X = 0,
            Y = 0,
            Width = GameController.Window.GetWindowRectangleTimeCache.Size.X,
            Height = GameController.Window.GetWindowRectangleTimeCache.Size.Y
        };

        var left = screen.Left;
        var right = screen.Right;

        if (State.IngameUi.OpenRightPanel.IsVisible)
            right -= State.IngameUi.OpenRightPanel.GetClientRect().Width;

        if (State.IngameUi.OpenLeftPanel.IsVisible || WaypointPanelIsOpen)
            left += Math.Max(State.IngameUi.OpenLeftPanel.GetClientRect().Width, State.IngameUi.SettingsPanel.GetClientRect().Width);

        if (State.IngameUi.WorldMap.GetChildAtIndex(9).IsVisible) {
            
            RectangleF mapTooltip = State.IngameUi.WorldMap.GetChildAtIndex(9).GetClientRect();                
            mapTooltip.Inflate(mapTooltip.Width * 0.1f, mapTooltip.Height * 0.1f);

            if (position.X > mapTooltip.Left && position.X < mapTooltip.Right && position.Y > mapTooltip.Top && position.Y < mapTooltip.Bottom)
                return false;
        }
        
        return !(position.X < left || position.X > right || position.Y < screen.Top || position.Y > screen.Bottom);
    }

    public float GetDistanceToNode(AtlasNodeDescription node)
    {
        return Vector2.Distance(screenCenter, node.Element.GetClientRect().Center);
    }

    #endregion
    #region Waypoint Panel
    private void DrawWaypointPanel() {
        Vector2 panelSize = new Vector2(State.IngameUi.SettingsPanel.GetClientRect().Width, State.IngameUi.SettingsPanel.GetClientRect().Height);
        Vector2 panelPosition = State.IngameUi.SettingsPanel.GetClientRect().TopLeft;
        ImGui.SetNextWindowPos(panelPosition, ImGuiCond.Always);
        ImGui.SetNextWindowSize(panelSize, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.8f);

        ImGui.Begin("WaypointPanel", ref WaypointPanelIsOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove);

        // Settings table
        if (ImGui.BeginTable("waypoint_top_table", 2, ImGuiTableFlags.NoBordersInBody|ImGuiTableFlags.PadOuterX))
        {
            ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 60);                                                               
            ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthStretch, 300);                     

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            bool _show = Settings.Waypoints.ShowWaypoints;
            if(ImGui.Checkbox($"##show_waypoints", ref _show))                        
                Settings.Waypoints.ShowWaypoints = _show;

            ImGui.TableNextColumn();
            ImGui.Text("Show Waypoints on Atlas");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            bool _showArrows = Settings.Waypoints.ShowWaypointArrows;
            if(ImGui.Checkbox($"##show_arrows", ref _showArrows))                        
                Settings.Waypoints.ShowWaypointArrows = _showArrows;

            ImGui.TableNextColumn();
            ImGui.Text("Show Waypoint Arrows on Atlas");

            ImGui.TableNextRow();
        }
        ImGui.EndTable();

        ImGui.Spacing();

        // larger font size
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 10));
        ImGui.Text("Waypoints");
        ImGui.PopStyleVar();        
        ImGui.Separator();


        #region Waypoints Table
        // Collapse
        if (ImGui.CollapsingHeader("Waypoints", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerH;
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0, 0, 0, 0));
            if (ImGui.BeginTable("waypoint_list_table", 8, flags, new Vector2(-1, panelSize.Y/3)))
            {
                ImGui.TableSetupColumn("Enable", ImGuiTableColumnFlags.WidthFixed, 30);                                                               
                ImGui.TableSetupColumn("Waypoint Name", ImGuiTableColumnFlags.WidthFixed, 300);     
                ImGui.TableSetupColumn("X", ImGuiTableColumnFlags.WidthFixed, 40);                    
                ImGui.TableSetupColumn("Y", ImGuiTableColumnFlags.WidthFixed, 40);     
                ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 30);     
                ImGui.TableSetupColumn("Scale", ImGuiTableColumnFlags.WidthFixed, 100);     
                ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthFixed, 60); 
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 50);
                ImGui.TableHeadersRow();                    

                foreach (var waypoint in Settings.Waypoints.Waypoints.Values) {
                    string id = waypoint.Address.ToString();
                    ImGui.PushID(id);
                    
                    ImGui.TableNextRow();

                    // Enabled
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - 30.0f) / 2.0f);
                    bool _show = waypoint.Show;
                    if (ImGui.Checkbox($"##{id}_enabled", ref _show)) {
                        waypoint.Show = _show;
                    }

                    // Name
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(300);
                    string _name = waypoint.Name;                    
                    if (ImGui.InputText($"##{id}_name", ref _name, 32)) {
                        waypoint.Name = _name;
                    }
                    // Coordinates
                    ImGui.TableNextColumn();                    
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - 40.0f) / 2.0f);
                    ImGui.Text(waypoint.Coordinates.X.ToString());

                    ImGui.TableNextColumn();                    
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - 40.0f) / 2.0f);
                    ImGui.Text(waypoint.Coordinates.Y.ToString());


                    // Color
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - 30.0f) / 2.0f);
                    Color _color = waypoint.Color;
                    Vector4 _vector = new Vector4(_color.R / 255.0f, _color.G / 255.0f, _color.B / 255.0f, _color.A / 255.0f);
                    if(ImGui.ColorEdit4($"##{id}_nodecolor", ref _vector, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs))                        
                        waypoint.Color = Color.FromArgb((int)(_vector.W * 255), (int)(_vector.X * 255), (int)(_vector.Y * 255), (int)(_vector.Z * 255));
                    
                    // Scale
                    ImGui.TableNextColumn();
                    float _scale = waypoint.Scale;
                    ImGui.SetNextItemWidth(100);
                    if(ImGui.SliderFloat($"##{id}_weight", ref _scale, 0.1f, 2.0f, "%.2f"))                        
                        waypoint.Scale = _scale;


                    // Buttons
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - 50.0f) / 2.0f);
                    ImGui.SetNextItemWidth(60);
                    if (ImGui.Button("Delete")) {
                        RemoveWaypoint(waypoint);
                    }
                    ImGui.PopID();
                }
                ImGui.EndTable();
                ImGui.PopStyleColor();
            }
            #endregion
            
        }
       
        ImGui.Spacing();

        #region Atlas Table
        if (ImGui.CollapsingHeader("Atlas"))
        {

            ImGui.BeginGroup();
            // Sort by Combobox
            ImGui.SetNextItemWidth(200);
            string sortBy = "Weight";
            if (ImGui.BeginCombo("Sort By", sortBy))
            {
                if (ImGui.Selectable("Name", sortBy == "Name"))
                    sortBy = "Name";
                if (ImGui.Selectable("Weight", sortBy == "Weight"))
                    sortBy = "Weight";
                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();
            int maxItems = 100;
            ImGui.SetNextItemWidth(200);
            ImGui.InputInt("Max Items", ref maxItems);

            ImGui.EndGroup();

            var tempCache = mapCache.Where(x => !x.Value.IsVisited);    

            switch(sortBy) {
                case "Name":
                    tempCache = tempCache.OrderBy(x => x.Value.Name).ToDictionary(x => x.Key, x => x.Value);
                    break;
                case "Weight":
                    tempCache = tempCache.OrderByDescending(x => x.Value.Weight).ToDictionary(x => x.Key, x => x.Value);
                    break;
                default:
                    tempCache = tempCache.OrderByDescending(x => x.Value.Weight).ToDictionary(x => x.Key, x => x.Value);
                    break;
            }

            tempCache = tempCache.Take(maxItems).ToDictionary(x => x.Key, x => x.Value);

            var flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.NoSavedSettings;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2)); // Adjust the padding values as needed
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(2, 2)); // A
            if (ImGui.BeginTable("atlas_list_table", 8, flags, new Vector2(-1, panelSize.Y/3)))
            {                                                            
                ImGui.TableSetupColumn("Map Name", ImGuiTableColumnFlags.WidthFixed, 200);   
                ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthFixed, 60);     
                ImGui.TableSetupColumn("Modifiers", ImGuiTableColumnFlags.WidthFixed, 100); 
                ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Unlocked", ImGuiTableColumnFlags.WidthFixed, 28);
                ImGui.TableSetupColumn("Way", ImGuiTableColumnFlags.WidthFixed, 32);
                ImGui.TableHeadersRow();                    

                Vector4 _colorVector;
                Color _color;

                if (tempCache != null) {
                    foreach (var (key, node) in tempCache) {
                        string id = node.Address.ToString();
                        ImGui.PushID(id);                        
                        ImGui.TableNextRow();

                        // Name
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(node.Name);

                        ImGui.SetWindowFontScale(0.7f);            

                        // Content
                        ImGui.TableNextColumn();
                        foreach(var content in node.MapContent) {
                            _color = Settings.MapContent.ContentTypes[content.Name].Color;
                            _colorVector = new Vector4(_color.R / 255.0f, _color.G / 255.0f, _color.B / 255.0f, _color.A / 255.0f);
                            ImGui.PushStyleColor(ImGuiCol.Text, _colorVector);
                            ImGui.TextUnformatted(content.Name);
                            ImGui.PopStyleColor();
                        }
                        

                        // Modifiers
                        ImGui.TableNextColumn();
                        foreach(var effect in node.Effects) {       
                            _color = Settings.MapMods.MapModTypes[effect.Key].Color;
                            _colorVector = new Vector4(_color.R / 255.0f, _color.G / 255.0f, _color.B / 255.0f, _color.A / 255.0f);
                            ImGui.PushStyleColor(ImGuiCol.Text, _colorVector);
                            ImGui.TextUnformatted(effect.Value.ToString());
                            ImGui.PopStyleColor();
                        }
                        // reset font size
                        ImGui.SetWindowFontScale(1.0f);

                        // Weight
                        ImGui.TableNextColumn();
                        // set color
                        float weight = (node.Weight - minMapWeight) / (maxMapWeight - minMapWeight);        
                        _color = ColorUtils.InterpolateColor(Settings.Maps.BadNodeColor,Settings.Maps.GoodNodeColor, weight);
                        _colorVector = new Vector4(_color.R / 255.0f, _color.G / 255.0f, _color.B / 255.0f, _color.A / 255.0f);
                        ImGui.PushStyleColor(ImGuiCol.Text, _colorVector);
                        ImGui.TextUnformatted(node.Weight.ToString("0.0"));
                        ImGui.PopStyleColor();

                        // Unlocked
                        ImGui.TableNextColumn();
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - 30.0f) / 2.0f);
                        bool _unlocked = node.IsUnlocked;
                        ImGui.BeginDisabled();                        
                        ImGui.Checkbox($"##{id}_enabled", ref _unlocked);
                        ImGui.EndDisabled();
    //
                        // Buttons
                        ImGui.TableNextColumn();
                        RectangleF icon = SpriteHelper.GetUV(MapIconsIndex.Waypoint);
                        
                        if (!node.IsWaypoint()){
                            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.TableRowBg));
                            if (ImGui.ImageButton($"$${id}_wp", iconsId, new Vector2(32,32), icon.TopLeft, icon.BottomRight)) {
                                AddWaypoint(node.MapNode());
                            } else if (ImGui.IsItemHovered()) {
                                ImGui.SetTooltip("Add Waypoint");
                            }
                            ImGui.PopStyleColor();
                        }

                        ImGui.PopID();
                    }
                }
            }
            ImGui.EndTable();
            ImGui.PopStyleVar(2);
            #endregion
            
        }

        ImGui.End();
    }
    #endregion

    #region Waypoint Functions
    private void DrawWaypoint(Waypoint waypoint) {
        if (!Settings.Waypoints.ShowWaypoints || waypoint.MapNode() == null || !waypoint.Show || !IsOnScreen(waypoint.MapNode().Element.GetClientRect().Center))
            return;

        Vector2 waypointSize = new Vector2(48, 48);        
        waypointSize *= waypoint.Scale;

        Vector2 iconPosition = waypoint.MapNode().Element.GetClientRect().Center - new Vector2(0, (waypoint.MapNode().Element.GetClientRect().Height / 2));

        if (waypoint.MapNode().Element.GetChildAtIndex(0) != null)
            iconPosition -= new Vector2(0, waypoint.MapNode().Element.GetChildAtIndex(0).GetClientRect().Height);

        iconPosition -= new Vector2(0, 20);
        Vector2 waypointTextPosition = iconPosition - new Vector2(0, 10);
        
        DrawCenteredTextWithBackground(waypoint.Name, waypointTextPosition, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        
        iconPosition -= new Vector2(waypointSize.X / 2, 0);
        RectangleF iconSize = new RectangleF(iconPosition.X, iconPosition.Y, waypointSize.X, waypointSize.Y);
        Graphics.DrawImage(IconsFile, iconSize, SpriteHelper.GetUV(waypoint.Icon), waypoint.Color);


    }

    private void AddWaypoint(AtlasNodeDescription mapNode) {
        if (!mapCache.ContainsKey(mapNode.Element.Address)) {
            LogMessage("MapNode not found in cache, scroll your atlas to load it first.");
            return;
        }

        Node node = mapCache[mapNode.Element.Address];
        if (Settings.Waypoints.Waypoints.ContainsKey(node.Coordinate.ToString()))
            return;

        float weight = (node.Weight - minMapWeight) / (maxMapWeight - minMapWeight);
        Waypoint newWaypoint = node.ToWaypoint();
        newWaypoint.Icon = MapIconsIndex.LootFilterLargeWhiteUpsideDownHouse;
        newWaypoint.Color = ColorUtils.InterpolateColor(Settings.Maps.BadNodeColor, Settings.Maps.GoodNodeColor, weight);

        Settings.Waypoints.Waypoints.Add(node.Coordinate.ToString(), newWaypoint);
    }

    private void RemoveWaypoint(AtlasNodeDescription mapNode) {
        if (!Settings.Waypoints.Waypoints.ContainsKey(mapNode.Coordinate.ToString()))
            return;

        Settings.Waypoints.Waypoints.Remove(mapNode.Coordinate.ToString());
    }
    private void RemoveWaypoint(Waypoint waypoint) {

        Settings.Waypoints.Waypoints.Remove(waypoint.Coordinates.ToString());
    }

    private void DrawWaypointArrow(Waypoint waypoint) {
        if (!Settings.Waypoints.ShowWaypointArrows || waypoint.MapNode() == null)
            return;


        Vector2 waypointPosition = waypoint.MapNode().Element.GetClientRect().Center;

        float distance = Vector2.Distance(screenCenter, waypointPosition);

        if (distance < 400)
            return;

        Vector2 arrowSize = new(64, 64);
        Vector2 arrowPosition = waypointPosition;
        arrowPosition.X = Math.Clamp(arrowPosition.X, 0, GameController.Window.GetWindowRectangleTimeCache.Size.X);
        arrowPosition.Y = Math.Clamp(arrowPosition.Y, 0, GameController.Window.GetWindowRectangleTimeCache.Size.Y);
        arrowPosition = Vector2.Lerp(screenCenter, arrowPosition, 0.80f);
        arrowPosition -= new Vector2(arrowSize.X / 2, arrowSize.Y / 2);

        Vector2 direction = waypointPosition - screenCenter;
        float phi = (float)Math.Atan2(direction.Y, direction.X) + (float)(Math.PI / 2);

        Color color = Color.FromArgb(255, waypoint.Color);
        DrawRotatedImage(arrowId, arrowPosition, arrowSize, phi, color);

        Vector2 textPosition = arrowPosition + new Vector2(arrowSize.X / 2, arrowSize.Y / 2);
        textPosition = Vector2.Lerp(textPosition, screenCenter, 0.10f);
        DrawCenteredTextWithBackground($"{waypoint.Name} ({distance:0})", textPosition, color, Settings.Graphics.BackgroundColor, true, 10, 4);
    }

    #endregion

}

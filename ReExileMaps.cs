using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ExileCore2;
using ExileCore2.PoEMemory.Elements.AtlasElements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Helpers;
using ExileCore2.Shared.Nodes;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared; // Для RectangleF
using GameOffsets2.Native; // Для Vector2i

using ImGuiNET;

using RectangleF = ExileCore2.Shared.RectangleF;
using ReExileMaps.Classes;

namespace ReExileMaps;

// Интерфейс для компонента с позицией, если его нет в библиотеке
public interface IPositioned
{
    Vector2i? GridPos { get; }
}

// Helper classes
public class MapSearchItem
{
    public Node MapNode { get; set; }
    public float Distance { get; set; }
    public string Name => MapNode?.Name ?? "Unknown";
    
    public MapSearchItem(Node node, float distance)
    {
        MapNode = node;
        Distance = distance;
    }
}

#nullable enable
// Main plugin class
public class ReExileMapsCore : BaseSettingsPlugin<ReExileMapsSettings>
{
    #region Declarations
    public static ReExileMapsCore Main;

    // Константы для путей к файлам и ресурсам
    private const string defaultMapsPath = "json\\maps.json";
    private const string defaultModsPath = "json\\mods.json";
    private const string defaultBiomesPath = "json\\biomes.json";
    private const string defaultContentPath = "json\\content.json";
    private const string ArrowTexturePath = "textures\\arrow.png";
    private const string IconsFile = "Icons.png";
    
    public IngameUIElements UI;
    public AtlasPanel AtlasPanel;

    private Vector2 screenCenter;
    private Dictionary<Vector2i, Node> mapCache = [];
    public bool refreshCache = false;
    private int cacheTicks = 0;
    private bool refreshingCache = false;
    private float maxMapWeight = 20.0f;
    private float minMapWeight = -20.0f;
    private float averageMapWeight = 0.0f;
    private float stdDevMapWeight = 0.0f;
    private readonly object mapCacheLock = new();
    private DateTime lastRefresh = DateTime.Now;
    private int TickCount { get; set; }

    private Vector2 atlasOffset;
    private Vector2 atlasDelta;

    internal IntPtr iconsId;
    internal IntPtr arrowId;

    private bool AtlasHasBeenClosed = true;
    private bool WaypointPanelIsOpen = false;
    private bool ShowMinimap = false;

    private bool SearchPanelIsOpen = false;
    private Vector2 searchPanelPosition = Vector2.Zero;
    private List<Node> searchResults = [];
    private string previousSearchQuery = "";

    // Фильтрованный кеш для ускорения отображения
    private Dictionary<Vector2i, Node> filteredMapCache = new Dictionary<Vector2i, Node>();
    private DateTime lastSearchUpdate = DateTime.MinValue;
    private DateTime lastWaypointUpdate = DateTime.MinValue;
    private bool needSearchUpdate = true;
    private bool needWaypointUpdate = true;

    // Для расстояния по координатам
    private Dictionary<string, float> cachedDistances = new Dictionary<string, float>();
    private List<MapSearchItem> mapItems = new List<MapSearchItem>();
    private string referencePositionText = "from screen center";
    private Vector2 manualReferencePosition = Vector2.Zero;
    private bool useManualReferencePosition = false;
    
    // Для поиска
    private Dictionary<string, List<Node>> nodesByName = new Dictionary<string, List<Node>>();
    private List<MapSearchItem> currentItems = new List<MapSearchItem>();
    private string query = "";
    private List<Node> results = new List<Node>();
    private bool distanceHeaderClicked = false;

    #endregion

    #region ExileCore Methods
    public override bool Initialise()
    {
        Main = this;        
        RegisterHotkeys();
        SubscribeToEvents();

        LoadDefaultBiomes();
        LoadDefaultContentTypes();
        LoadDefaultMaps();
        LoadDefaultMods();
        
        Graphics.InitImage(IconsFile);
        iconsId = Graphics.GetTextureId(IconsFile);
        Graphics.InitImage("arrow.png", Path.Combine(DirectoryFullName, ArrowTexturePath));
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
        try {
            if (GameController?.Game?.IngameState?.IngameUi == null) return;
            
            UI = GameController.Game.IngameState.IngameUi;
            if (UI?.WorldMap?.AtlasPanel == null) return;
            
            AtlasPanel = UI.WorldMap.AtlasPanel;

            if (!AtlasPanel.IsVisible) {
                AtlasHasBeenClosed = true;
                WaypointPanelIsOpen = false;
                return;
            }

            if (AtlasHasBeenClosed) {
                RefreshMapCache(true);
            }

            AtlasHasBeenClosed = false;

            cacheTicks++;
            // ��������� ������������� ���������� ���� ������ 30 �����, � �� 100
            if (cacheTicks % 30 == 0) {
                if (AtlasPanel.Descriptions != null && AtlasPanel.Descriptions.Count > mapCache.Count)
                    refreshCache = true;
                cacheTicks = 0;
            }
            
            if (GameController?.Window?.GetWindowRectangle() == null) return;
            screenCenter = GameController.Window.GetWindowRectangle().Center - GameController.Window.GetWindowRectangle().Location;
            
            if (refreshCache && !refreshingCache && (DateTime.Now.Subtract(lastRefresh).TotalSeconds > Settings.Graphics.MapCacheRefreshRate || mapCache.Count == 0))
            {
                var job = new Job($"{nameof(ReExileMapsCore)}RefreshCache", () =>
                {
                    RefreshMapCache();
                    refreshCache = false;
                });
                job.Start();
            }
        }
        catch (Exception ex) {
            LogError($"Error in Tick: {ex.Message}\n{ex.StackTrace}");
        }
        
        return;
    }

    public override void Render()
    {
        CheckKeybinds();

        if (WaypointPanelIsOpen) DrawWaypointPanel();
        
        // ��������� ������ ��� ����������� ���������� ������
        if (Settings.Search.PanelIsOpen) {
            // ���� ���� ������������� �������� �����, � ������ ������� � ���������� �����
            if (needSearchUpdate && DateTime.Now.Subtract(lastSearchUpdate).TotalSeconds > 0.75) {
                UpdateSearchResults();
            }
            DrawSearchPanel();
        }

        TickCount++;
        if (Settings.Graphics.RenderNTicks.Value % TickCount != 0) return;  

        TickCount = 0;

        if (!AtlasPanel.IsVisible) return;
        
        // ������������� ��������� ���, ���� �� ����
        if (mapCache.Count == 0) {
            var job = new Job($"{nameof(ReExileMapsCore)}InitialRefreshCache", () => {
                RefreshMapCache();
                refreshCache = false;
            });
            job.Start();
            return; // ���������� ���� ����, �������� � ���������
        }
        
        // Filter out nodes based on settings.
        List<Node> selectedNodes;
        lock (mapCacheLock) {
            selectedNodes = mapCache.Values.Where(x => Settings.Features.ProcessVisitedNodes || !x.IsVisited || x.IsAttempted)
                .Where(x => (Settings.Features.ProcessHiddenNodes && !x.IsVisible) || x.IsVisible || x.IsTower)            
                .Where(x => (Settings.Features.ProcessLockedNodes && !x.IsUnlocked) || x.IsUnlocked)
                .Where(x => (Settings.Features.ProcessUnlockedNodes && x.IsUnlocked) || !x.IsUnlocked)
                .Where(x => IsOnScreen(x.MapNode.Element.GetClientRect().Center)).AsParallel().ToList();
        }
        
        selectedNodes.ForEach(RenderNode);

        try {
            List<string> waypointNames = Settings.MapTypes.Maps.Where(x => x.Value.DrawLine).Select(x => x.Value.Name).ToList();
            if (Settings.Features.DrawLines && waypointNames.Count > 0) {
                List<Node> waypointNodes;

                lock (mapCacheLock) {                        
                    waypointNodes = mapCache.Values.Where(x => !x.IsVisited || x.IsAttempted)
                    .Where(x => waypointNames.Contains(x.Name))
                    .Where(x => !Settings.Features.WaypointsUseAtlasRange || Vector2.Distance(screenCenter, x.MapNode.Element.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000)).AsParallel().ToList();
                }
                
                waypointNodes.ForEach(DrawWaypointLine);
            }
        } catch (Exception e) {
            LogError("Error drawing waypoint lines: " + e.Message + "\n" + e.StackTrace);
        }

        try {
            foreach (var (k,waypoint) in Settings.Waypoints.Waypoints) {
                DrawWaypoint(waypoint);
                DrawWaypointArrow(waypoint);
            }
        }
        catch (Exception e) {
            LogError("Error drawing waypoints: " + e.Message + "\n" + e.StackTrace);
        }

    }
    #endregion

    #region Keybinds & Events

    ///MARK: SubscribeToEvents
    /// <summary>
    /// Subscribes to events that trigger a refresh of the map cache.
    /// </summary>
    private void SubscribeToEvents() {
        try {
            Settings.MapTypes.Maps.CollectionChanged += (_, _) => { RecalculateWeights(); };
            Settings.MapTypes.Maps.PropertyChanged += (_, _) => { RecalculateWeights(); };
            Settings.Biomes.Biomes.PropertyChanged += (_, _) => { RecalculateWeights(); };
            Settings.Biomes.Biomes.CollectionChanged += (_, _) => { RecalculateWeights(); };
            Settings.MapContent.ContentTypes.CollectionChanged += (_, _) => { RecalculateWeights(); };
            Settings.MapContent.ContentTypes.PropertyChanged += (_, _) => { RecalculateWeights(); };
            Settings.MapMods.MapModTypes.CollectionChanged += (_, _) => { RecalculateWeights(); };
            Settings.MapMods.MapModTypes.PropertyChanged += (_, _) => { RecalculateWeights(); };
        } catch (Exception e) {
            LogError("Error subscribing to events: " + e.Message);
        }
    }

    ///MARK: RegisterHotkeys
    /// <summary>
    /// Registers the hotkeys defined in the settings.
    /// </summary>
    private void RegisterHotkeys() {
        RegisterHotkey(Settings.Keybinds.RefreshMapCacheHotkey);
        RegisterHotkey(Settings.Keybinds.DebugKey);
        RegisterHotkey(Settings.Keybinds.ToggleWaypointPanelHotkey);
        RegisterHotkey(Settings.Keybinds.AddWaypointHotkey);
        RegisterHotkey(Settings.Keybinds.DeleteWaypointHotkey);
        RegisterHotkey(Settings.Keybinds.ShowTowerRangeHotkey);
        RegisterHotkey(Settings.Keybinds.UpdateMapsKey);
        RegisterHotkey(Settings.Keybinds.ToggleLockedNodesHotkey);
        RegisterHotkey(Settings.Keybinds.ToggleUnlockedNodesHotkey);
        RegisterHotkey(Settings.Keybinds.ToggleVisitedNodesHotkey);
        RegisterHotkey(Settings.Keybinds.ToggleHiddenNodesHotkey);
        RegisterHotkey(Settings.Keybinds.SearchPanelHotkey);
    }
    
    private static void RegisterHotkey(HotkeyNode hotkey)
    {
        Input.RegisterKey(hotkey);
        hotkey.OnValueChanged += () => { Input.RegisterKey(hotkey); };
    }
    private void CheckKeybinds() {
        try {
            if (AtlasPanel == null || !AtlasPanel.IsVisible)
                return;

            if (Settings?.Keybinds?.RefreshMapCacheHotkey?.PressedOnce() == true) {  
                // ��������� ���������� ���� � ��������� ������, ����� �������� ���������
                var job = new Job($"{nameof(ReExileMapsCore)}RefreshCache", () =>
                {
                    RefreshMapCache();
                    refreshCache = false;
                });
                job.Start();
            }

            if (Settings?.Keybinds?.DebugKey?.PressedOnce() == true) {
                DoDebugging();
            }

            if (Settings?.Keybinds?.UpdateMapsKey?.PressedOnce() == true) {
                UpdateMapData();
            }

            if (Settings?.Keybinds?.ToggleWaypointPanelHotkey?.PressedOnce() == true) {  
                WaypointPanelIsOpen = !WaypointPanelIsOpen;
            }

            if (Settings?.Keybinds?.AddWaypointHotkey?.PressedOnce() == true) {
                var node = GetClosestNodeToCursor();
                if (node != null) AddWaypoint(node);
            }

            if (Settings?.Keybinds?.DeleteWaypointHotkey?.PressedOnce() == true) {
                var node = GetClosestNodeToCursor();
                if (node != null) RemoveWaypoint(node);
            }

            if (Settings?.Keybinds?.ToggleLockedNodesHotkey?.PressedOnce() == true && Settings?.Features?.ProcessLockedNodes != null) {
                Settings.Features.ProcessLockedNodes.Value = !Settings.Features.ProcessLockedNodes.Value;
            }
            
            if (Settings?.Keybinds?.ToggleUnlockedNodesHotkey?.PressedOnce() == true && Settings?.Features?.ProcessUnlockedNodes != null) {
                Settings.Features.ProcessUnlockedNodes.Value = !Settings.Features.ProcessUnlockedNodes.Value;
            }

            if (Settings?.Keybinds?.ToggleVisitedNodesHotkey?.PressedOnce() == true && Settings?.Features?.ProcessVisitedNodes != null) {
                Settings.Features.ProcessVisitedNodes.Value = !Settings.Features.ProcessVisitedNodes.Value;
            }

            if (Settings?.Keybinds?.ToggleHiddenNodesHotkey?.PressedOnce() == true && Settings?.Features?.ProcessHiddenNodes != null) {
                Settings.Features.ProcessHiddenNodes.Value = !Settings.Features.ProcessHiddenNodes.Value;
            }

            if (Settings?.Keybinds?.ShowTowerRangeHotkey?.PressedOnce() == true) {
                var cursor = GetClosestNodeToCursor();
                if (cursor != null && mapCache.TryGetValue(cursor.Coordinates, out Node node)) {
                    if (node != null) {
                        var nodesToUpdate = mapCache.Where(x => x.Value.DrawTowers && x.Value.Address != node.Address).AsParallel().ToList();
                        foreach (var n in nodesToUpdate) {
                            if (n.Value != null) n.Value.DrawTowers = false;
                        }
                        node.DrawTowers = !node.DrawTowers;
                    }
                }
            }

            if (Settings?.Keybinds?.SearchPanelHotkey?.PressedOnce() == true && Settings?.Search != null) {
                Settings.Search.PanelIsOpen = !Settings.Search.PanelIsOpen;
                if (Settings.Search.PanelIsOpen && GameController?.Window?.GetWindowRectangle() != null) {
                    // Set initial position for search panel
                    searchPanelPosition = new Vector2(GameController.Window.GetWindowRectangle().Width / 2 - 300, 100);
                    UpdateSearchResults();
                }
            }
        }
        catch (Exception ex) {
            LogError($"Error in CheckKeybinds: {ex.Message}\n{ex.StackTrace}");
        }
    }
    #endregion

    #region Load Defaults
    private void LoadDefaultMods() {
        try {
            if (Settings.MapMods.MapModTypes == null)
                Settings.MapMods.MapModTypes = new ObservableDictionary<string, Mod>();

            var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultModsPath));
            var mods = JsonConvert.DeserializeObject<Dictionary<string, Mod>>(jsonFile);

            foreach (var mod in mods.OrderBy(x => x.Value.Name))
                Settings.MapMods.MapModTypes.TryAdd(mod.Key, mod.Value);

            LogMessage("Loaded Mods");
        } catch (Exception e) {
            LogError("Error loading default mod: " + e.Message + "\n" + e.StackTrace);
        }
    }

    private void LoadDefaultBiomes() {
        try {
            var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultBiomesPath));
            var biomes = JsonConvert.DeserializeObject<Dictionary<string, Biome>>(jsonFile);

            foreach (var biome in biomes.Where(x => x.Value.Name != "").OrderBy(x => x.Value.Name))
                Settings.Biomes.Biomes.TryAdd(biome.Key, biome.Value);  

            LogMessage("Loaded Biomes");
        } catch (Exception e) {
            LogError("Error loading default biomes: " + e.Message + "\n" + e.StackTrace);
        }
            
    }

    private void LoadDefaultContentTypes() {
        try {
            var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultContentPath));
            var contentTypes = JsonConvert.DeserializeObject<Dictionary<string, Content>>(jsonFile);

            foreach (var content in contentTypes.OrderBy(x => x.Value.Name))
                Settings.MapContent.ContentTypes.TryAdd(content.Key, content.Value);   

            LogMessage("Loaded Content Types");
        } catch (Exception e) {
            LogError("Error loading default content types: " + e.Message + "\n" + e.StackTrace);
        }

    }
    
    public void LoadDefaultMaps()
    {
        try {
            var jsonFile = File.ReadAllText(Path.Combine(DirectoryFullName, defaultMapsPath));
            var maps = JsonConvert.DeserializeObject<Dictionary<string, Map>>(jsonFile);

            foreach (var (key,map) in maps.OrderBy(x => x.Value.Name)) {

                // Update legacy map settings
                if(Settings.MapTypes.Maps.TryGetValue(map.Name.Replace(" ", ""), out Map existingMap) && existingMap.IDs.Length == 0) {
                    Settings.MapTypes.Maps.Remove(existingMap.Name.Replace(" ",""));
                    existingMap.ID = key;
                    existingMap.IDs = map.IDs;
                    existingMap.ShortestId = map.ShortestId;
                    Settings.MapTypes.Maps.TryAdd(key, existingMap);                
                } else {
                    // add new map
                    Settings.MapTypes.Maps.TryAdd(key, map);
                }
            }
        } catch (Exception e) {
            LogError("Error loading default maps: " + e.Message + "\n" + e.StackTrace);
        }
    }

    #endregion
    

    
    #region Map Processing
    ///MARK: RenderNode
    /// <summary>
    /// Renders a map node on the atlas panel.
    /// </summary>
    /// <param name="cachedNode"></param>
    private void RenderNode(Node cachedNode)
    {
        if (ShowMinimap) 
            return;

        if (Settings.Features.DebugMode) {
            DrawDebugging(cachedNode);
            return;
        }

        var ringCount = 0;           
        RectangleF nodeCurrentPosition = cachedNode.MapNode.Element.GetClientRect();

        try {
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Breach");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Delirium");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Expedition");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Ritual");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Boss");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Corruption");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Irradiated");
            DrawConnections(cachedNode, nodeCurrentPosition);
            DrawMapNode(cachedNode, nodeCurrentPosition);            
            DrawTowerMods(cachedNode, nodeCurrentPosition);
            DrawMapName(cachedNode, nodeCurrentPosition);
            DrawWeight(cachedNode, nodeCurrentPosition);
            DrawTowerRange(cachedNode);

        } catch (Exception e) {
            LogError("Error drawing map node: " + e.Message + " - " + e.StackTrace);
            return;
        }
    }
    #endregion

    #region Debugging
    private void DoDebugging() {
        mapCache.TryGetValue(GetClosestNodeToCursor().Coordinates, out Node cachedNode);
        LogMessage(cachedNode.ToString());

    }

    private void DrawDebugging(Node cachedNode) {
        using (Graphics.SetTextScale(Settings.MapMods.MapModScale))
            DrawCenteredTextWithBackground(cachedNode.DebugText(), cachedNode.MapNode.Element.GetClientRect().Center, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);

    }

    private void UpdateMapData() {
        var uniqueMapNames = AtlasPanel.Descriptions.Select(x => x.Element.Area.Name).Distinct().ToList();

        // iterate through each name and find the ID for all mapes iwth that name
        foreach (var name in uniqueMapNames) {
            var maps = AtlasPanel.Descriptions.Where(x => x.Element.Area.Name == name).ToList();
            var mapIds = maps.Select(x => x.Element.Area.Id).Distinct().ToList();
            // get shortest item from list
            var shortID = mapIds.OrderBy(x => x.Length).FirstOrDefault();
            if (Settings.MapTypes.Maps.TryGetValue(name.Replace(" ", ""), out Map mapType) || Settings.MapTypes.Maps.TryGetValue(shortID, out mapType)) {        
                Settings.MapTypes.Maps.Remove(name.Replace(" ", ""));                
                Settings.MapTypes.Maps.TryAdd(shortID, mapType);
                mapType.IDs = [.. mapIds];
                mapType.ShortestId = shortID;
                LogMessage($"Updated Map Data for {shortID}");
            } else {
                var newMap = new Map { 
                    Name = name, 
                    IDs = [.. mapIds],
                    ShortestId = shortID};
        
                Settings.MapTypes.Maps.TryAdd(shortID, newMap);        
                LogMessage($"Added Map Data for {shortID}");    
            }
        }

        var json = JsonConvert.SerializeObject(Settings.MapTypes.Maps, Formatting.Indented);
        File.WriteAllText(Path.Combine(DirectoryFullName, defaultMapsPath), json);

        LogMessage("Updated Map Data");
    }

    #endregion

    private Node GetClosestNodeToCursor() {
        try {
            if (AtlasPanel?.Descriptions == null || AtlasPanel.Descriptions.Count == 0)
                return null;
                
            // �������� ������� ��������� ������� ������������ �������� ����
            Vector2 cursorPos = Vector2.Zero;
            try {
                // ���������� ��������� ������� ���� ����� ������� ����������
                if (GameController?.Game?.IngameState?.UIHoverElement != null)
                    cursorPos = GameController.Game.IngameState.UIHoverElement.Tooltip.GetClientRect().Center;
                else
                    cursorPos = screenCenter; // �������� ������� - ����� ������
                
                // ����������� ��� �������
                LogMessage($"������ ��������� � �������: X={cursorPos.X}, Y={cursorPos.Y}");
            }
            catch (Exception ex) {
                LogError($"������ ��� ��������� ������� �������: {ex.Message}");
                cursorPos = screenCenter; // �������� ������� - ����� ������
            }
            
            // ���� ��������� ���� � ������� �������
            var closestNode = AtlasPanel.Descriptions
                .Where(x => x != null && x.Element != null)
                .OrderBy(x => {
                    try {
                        return Vector2.Distance(cursorPos, x.Element.GetClientRect().Center);
                    }
                    catch {
                        return float.MaxValue;
                    }
                })
                .AsParallel()
                .FirstOrDefault();
            
            if (closestNode == null)
                return null;
                
            if (mapCache.TryGetValue(closestNode.Coordinate, out Node cachedNode))
                return cachedNode;
            else
                return null;
        }
        catch (Exception ex) {
            try {
                LogError($"Error in GetClosestNodeToCursor: {ex.Message}");
            }
            catch {
                // ���������� ������ � ����� �����������
            }
            return null;
        }
    }

    private Node GetClosestNodeToCenterScreen() {
        if (AtlasPanel?.Descriptions == null || AtlasPanel.Descriptions.Count == 0)
            return null;
            
        var closestNode = AtlasPanel.Descriptions.OrderBy(x => Vector2.Distance(screenCenter, x.Element.GetClientRect().Center)).AsParallel().FirstOrDefault();
        
        if (closestNode == null)
            return null;
            
        if (mapCache.TryGetValue(closestNode.Coordinate, out Node cachedNode))
            return cachedNode;
        else 
            return null;
    }

    #region Map Cache
    public void RefreshMapCache(bool clearCache = false)
    {
        refreshingCache = true;

        if (clearCache)        
            lock (mapCacheLock)            
                mapCache.Clear();

        if (AtlasPanel?.Descriptions == null || AtlasPanel.Descriptions.Count == 0) {
            refreshingCache = false;
            refreshCache = false;
            lastRefresh = DateTime.Now;
            return;
        }

        List<AtlasNodeDescription> atlasNodes = [.. AtlasPanel.Descriptions];

        // Start timer
        var timer = new Stopwatch();
        timer.Start();
        long count = 0;
        foreach (var node in atlasNodes) {
            if (mapCache.TryGetValue(node.Coordinate, out Node cachedNode))
                count += RefreshCachedMapNode(node, cachedNode);
            else
                count += CacheNewMapNode(node);

            CacheMapConnections(mapCache[node.Coordinate]);

        }
        // stop timer
        timer.Stop();
        long time = timer.ElapsedMilliseconds;
        float average = (float)time / count;
        //LogMessage($"Map cache refreshed in {time}ms, {count} nodes processed, average time per node: {average:0.00}ms");

        RecalculateWeights();
        //LogMessage($"Max Map Weight: {maxMapWeight}, Min Map Weight: {minMapWeight}");

        refreshingCache = false;
        refreshCache = false;
        lastRefresh = DateTime.Now;
    }
    
    private void RecalculateWeights() {

        if (mapCache.Count == 0)
            return;

        var mapNodes = mapCache.Values.Where(x => !x.IsVisited).Select(x => x.Weight).Distinct().ToList();
        // Get the weighted average value of the map nodes
        averageMapWeight = mapNodes.Count > 0 ? mapNodes.Average() : 0;
        // Get the standard deviation of the map nodes
        // Get the max and min map weights
        maxMapWeight = mapNodes.Count > 0 ? mapNodes.OrderByDescending(x => x).Skip(10).Max() : 0;
        minMapWeight = mapNodes.Count > 0 ? mapNodes.OrderBy(x => x).Skip(5).Min() : 0;
    }

    private int CacheNewMapNode(AtlasNodeDescription node)
    {
        string mapId = node.Element.Area.Id.Trim();
        string shortID = mapId.Replace("_NoBoss", "");
        Node newNode = new()
        {
            IsUnlocked = node.Element.IsUnlocked,
            IsVisible = node.Element.IsVisible,
            IsVisited = node.Element.IsVisited,
            IsActive = node.Element.IsActive,
            ParentAddress = node.Address,
            Address = node.Element.Address,
            Coordinates = node.Coordinate,
            Name = node.Element.Area.Name,
            Id = mapId,
            MapNode = node,
            MapType = Settings.MapTypes.Maps.TryGetValue(shortID, out Map mapType) ? mapType : Settings.MapTypes.Maps.Where(x => x.Value.MatchID(mapId)).Select(x => x.Value).FirstOrDefault() ?? new Map()
        };

        // Set Content
        if (!newNode.IsVisited) {
            // Check if the map has content
            try {
                if (node.Element.GetChildAtIndex(0).Children.Any(x => x.TextureName.Contains("Corrupt")))
                    if (Settings.MapContent.ContentTypes.TryGetValue("Corruption", out Content corruption))                        
                        newNode.Content.TryAdd(corruption.Name, corruption);

                if (node.Element.Content != null)  
                    foreach(var content in node.Element.Content.Where(x => x.Name != "").AsParallel().ToList())        
                        if (Settings.MapContent.ContentTypes.TryGetValue(content.Name, out Content newContent))
                            newNode.Content.TryAdd(newContent.Name, newContent);

            } catch (Exception e) {
                LogError($"Error getting Content for map type {node.Address.ToString("X")}: " + e.Message);
            }
            
            // Set Biomes
            try {
                var biomes = Settings.MapTypes.Maps.TryGetValue(mapId, out Map map) ? map.Biomes.Where(x => x != "").AsParallel().ToList() : [];

                foreach (var biome in biomes)                     
                    if (Settings.Biomes.Biomes.TryGetValue(biome, out Biome newBiome)) 
                        newNode.Biomes.TryAdd(newBiome.Name, newBiome);

            }   catch (Exception e) {
                LogError($"Error getting Biomes for map type {mapId}: " + e.Message);
            }
        }
    
        if (!newNode.IsVisited || newNode.IsTower) {
            try {
                if (AtlasPanel?.EffectSources != null) {
                    foreach(var source in AtlasPanel.EffectSources.Where(x => Vector2.Distance(x.Coordinate, node.Coordinate) <= 11).AsParallel().ToList()) {
                        if (source?.Effects != null) {
                            foreach(var effect in source.Effects.Where(x => Settings.MapMods.MapModTypes.ContainsKey(x.ModId.ToString()) && x.Value != 0).AsParallel().ToList()) {
                                var effectKey = effect.ModId.ToString();
                                var requiredContent = Settings.MapMods.MapModTypes.TryGetValue(effectKey, out var modType) ? modType.RequiredContent : string.Empty;
                                
                                if (newNode.Effects.TryGetValue(effectKey, out Effect existingEffect)) {
                                    if (!newNode.IsTower || !newNode.IsVisited)
                                        newNode.Effects[effectKey].Value1 += effect.Value;

                                    newNode.Effects[effectKey].Sources.Add(source.Coordinate);
                                } else {
                                    if (Settings.MapMods.MapModTypes.TryGetValue(effectKey, out var mod)) {
                                        Effect newEffect = new() {
                                            Name = mod.Name,
                                            Description = mod.Description,
                                            Value1 = effect.Value,
                                            ID = effect.ModId,
                                            Enabled = mod.ShowOnMap && 
                                                    !(Settings.MapMods.OnlyDrawApplicableMods && 
                                                    !string.IsNullOrEmpty(requiredContent) && 
                                                    (newNode.Content == null || !newNode.Content.Any(x => x.Value.Name.Contains(requiredContent)))),
                                            Sources = [source.Coordinate]
                                        };
                                        
                                        newNode.Effects.TryAdd(effectKey, newEffect);
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                LogError($"Error getting Tower Effects for map {newNode.Coordinates}: " + e.Message);
            }
        }
        newNode.RecalculateWeight();

        lock (mapCacheLock)        
            return mapCache.TryAdd(node.Coordinate, newNode) ? 1 : 0;

    }

    private int RefreshCachedMapNode(AtlasNodeDescription node, Node cachedNode)
    {
        string shortID = node.Element.Area.Id.Trim().Replace("_NoBoss", "");
        cachedNode.IsUnlocked = node.Element.IsCompleted;
        cachedNode.IsVisible = node.Element.IsVisible;
        cachedNode.IsVisited = node.Element.IsVisited;
        cachedNode.IsActive = node.Element.IsActive;
        cachedNode.Address = node.Address;
        cachedNode.ParentAddress = node.Address;     
        cachedNode.MapNode = node;
        cachedNode.MapType = Settings.MapTypes.Maps.TryGetValue(shortID, out Map mapType) ? mapType : Settings.MapTypes.Maps.Where(x => x.Value.MatchID(node.Element.Area.Id)).Select(x => x.Value).FirstOrDefault() ?? new Map();

        if (cachedNode.IsVisited)
            return 1;

        cachedNode.Content.Clear();

        if (node.Element.GetChildAtIndex(0).Children.Any(x => x.TextureName.Contains("Corrupt")))
            if (Settings.MapContent.ContentTypes.TryGetValue("Corruption", out Content corruption))                        
                cachedNode.Content.TryAdd(corruption.Name, corruption);

        if (node.Element.Content != null)
            foreach(var content in node.Element.Content.Where(x => x.Name != ""))           
                cachedNode.Content.TryAdd(content.Name, Settings.MapContent.ContentTypes[content.Name]);

        try {
            cachedNode.Effects.Clear();
            if (AtlasPanel?.EffectSources != null) {
                foreach(var source in AtlasPanel.EffectSources.Where(x => Vector2.Distance(x.Coordinate, node.Coordinate) <= 11).ToList()) {
                    if (source?.Effects != null) {
                        foreach(var effect in source.Effects.Where(x => Settings.MapMods.MapModTypes.ContainsKey(x.ModId.ToString()) && x.Value != 0).ToList()) {
                            var effectKey = effect.ModId.ToString();
                            var requiredContent = Settings.MapMods.MapModTypes.TryGetValue(effectKey, out var modType) ? modType.RequiredContent : string.Empty;
                            
                            if (cachedNode.Effects.TryGetValue(effectKey, out Effect existingEffect)) {
                                if (cachedNode.IsTower || !cachedNode.IsVisited)
                                    cachedNode.Effects[effectKey].Value1 += effect.Value;

                                cachedNode.Effects[effectKey].Sources.Add(source.Coordinate);
                            } else {
                                if (Settings.MapMods.MapModTypes.TryGetValue(effectKey, out var mod)) {
                                    Effect newEffect = new() {
                                        Name = mod.Name,
                                        Description = mod.Description,
                                        Value1 = effect.Value,
                                        ID = effect.ModId,
                                        Enabled = mod.ShowOnMap && 
                                                    !(Settings.MapMods.OnlyDrawApplicableMods && 
                                                    !string.IsNullOrEmpty(requiredContent) && 
                                                    (cachedNode.Content == null || !cachedNode.Content.Any(x => x.Value.Name.Contains(requiredContent)))),
                                        Sources = [source.Coordinate]
                                    };
                                    
                                    cachedNode.Effects.TryAdd(effectKey, newEffect);
                                }
                            }                                       
                        }
                    }
                }
            }
        } catch (Exception e) {
            LogError($"Error getting Tower Effects for map {cachedNode.Coordinates}: " + e.Message);
        }

        cachedNode.RecalculateWeight();

        bool wasUnlocked = cachedNode.IsUnlocked;
        bool wasVisited = cachedNode.IsVisited;
        
        cachedNode.IsUnlocked = node.Element.IsCompleted;
        cachedNode.IsVisible = node.Element.IsVisible;
        cachedNode.IsVisited = node.Element.IsVisited;
        
        // If node was visited or unlocked and it wasn't before, and auto-remove is enabled
        if (Settings.Search.AutoRemoveWaypointAfterVisit && 
            ((!wasVisited && cachedNode.IsVisited) || (!wasUnlocked && cachedNode.IsUnlocked))) {
            // Remove waypoint if it exists
            string coordKey = cachedNode.Coordinates.ToString();
            if (Settings.Waypoints.Waypoints.ContainsKey(coordKey)) {
                Settings.Waypoints.Waypoints.Remove(coordKey);
            }
        }

        return 1;
    } 
    
    private void CacheMapConnections(Node cachedNode) {
        
        if (cachedNode.Neighbors.Where(x => x.Value.Coordinates != default).Count() == 4)
            return;
            
        var connectionPoints = AtlasPanel.Points.FirstOrDefault(x => x.Item1 == cachedNode.Coordinates);
        cachedNode.NeighborCoordinates = (connectionPoints.Item2, connectionPoints.Item3, connectionPoints.Item4, connectionPoints.Item5);
        var neighborCoordinates = new[] { connectionPoints.Item2, connectionPoints.Item3, connectionPoints.Item4, connectionPoints.Item5 };

        foreach (Vector2i vector in neighborCoordinates)
            if (mapCache.TryGetValue(vector, out Node neighborNode))
                cachedNode.Neighbors.TryAdd(vector, neighborNode);
        
        // Get connections from other nodes to this node
        var neighborConnections = AtlasPanel.Points.Where(x => x.Item2 == cachedNode.Coordinates || x.Item3 == cachedNode.Coordinates || x.Item4 == cachedNode.Coordinates || x.Item5 == cachedNode.Coordinates).AsParallel().ToList();
        foreach (var point in neighborConnections)
            if (mapCache.TryGetValue(point.Item1, out Node neighborNode))
                cachedNode.Neighbors.TryAdd(point.Item1, neighborNode);
        
    }
    
    #endregion

    #region Drawing Functions
    //MARK: DrawConnections
    /// <summary>
    /// Draws lines between a map node and its connected nodes on the atlas.
    /// </summary>
    /// <param name="WorldMap">The atlas panel containing the map nodes and their connections.</param>
    /// <param name="cachedNode">The map node for which connections are to be drawn.</param>
    /// 
    private void DrawConnections(Node cachedNode, RectangleF nodeCurrentPosition)
    {       
         foreach (Vector2i coordinates in cachedNode.GetNeighborCoordinates())
        {
            if (coordinates == default)
                continue;
            
            if (!mapCache.TryGetValue(coordinates, out Node destinationNode))
                continue;
                
            if (!Settings.Features.DrawVisitedNodeConnections && (destinationNode.IsVisited || cachedNode.IsVisited))
                continue;

            if ((!Settings.Features.DrawHiddenNodeConnections || !Settings.Features.ProcessHiddenNodes) && (!destinationNode.IsVisible || !cachedNode.IsVisible))
                continue;
            
            var destinationPos = destinationNode.MapNode.Element.GetClientRect();

            if (!IsOnScreen(destinationPos.Center) || !IsOnScreen(nodeCurrentPosition.Center))
                continue;

            if (Settings.Graphics.DrawGradientLines) {
                Color sourceColor = cachedNode.IsVisited ? Settings.Graphics.VisitedLineColor : cachedNode.IsUnlocked ? Settings.Graphics.UnlockedLineColor : Settings.Graphics.LockedLineColor;
                Color destinationColor = destinationNode.IsVisited ? Settings.Graphics.VisitedLineColor : destinationNode.IsUnlocked ? Settings.Graphics.UnlockedLineColor : Settings.Graphics.LockedLineColor;
                
                if (sourceColor == destinationColor)
                    Graphics.DrawLine(nodeCurrentPosition.Center, destinationPos.Center, Settings.Graphics.MapLineWidth, sourceColor);
                else
                    Graphics.DrawLine(nodeCurrentPosition.Center, destinationPos.Center, Settings.Graphics.MapLineWidth, sourceColor, destinationColor);

            } else {
                var color = Settings.Graphics.LockedLineColor;

                if (destinationNode.IsUnlocked || cachedNode.IsUnlocked)
                    color = Settings.Graphics.UnlockedLineColor;
                
                if (destinationNode.IsVisited && cachedNode.IsVisited)
                    color = Settings.Graphics.VisitedLineColor;

                Graphics.DrawLine(nodeCurrentPosition.Center, destinationPos.Center, Settings.Graphics.MapLineWidth, color);
            }
            
        }
    }

    /// MARK: HighlightMapContent
    /// <summary>
    /// Highlights a map node by drawing a circle around it if certain conditions are met.
    /// </summary>
    /// <param name="cachedNode">The map node to be highlighted.</param>
    /// <param name="Count">The count used to calculate the radius of the circle.</param>
    /// <param name="Content">The content string to check within the map node's elements.</param>
    /// <param name="Draw">A boolean indicating whether to draw the circle or not.</param>
    /// <param name="color">The color of the circle to be drawn.</param>
    /// <returns>Returns 1 if the circle is drawn, otherwise returns 0.</returns>
    private int DrawContentRings(Node cachedNode, RectangleF nodeCurrentPosition, int Count, string Content)
    {
        if ((cachedNode.IsVisited && !cachedNode.IsAttempted) || 
            (!Settings.MapContent.ShowRingsOnLockedNodes && !cachedNode.IsUnlocked) || 
            (!Settings.MapContent.ShowRingsOnUnlockedNodes && cachedNode.IsUnlocked) || 
            (!Settings.MapContent.ShowRingsOnHiddenNodes && !cachedNode.IsVisible) ||         
            !cachedNode.Content.TryGetValue(Content, out Content cachedContent) || 
            !cachedNode.MapType.Highlight || !cachedContent.Highlight)            
            return 0;

        float radius = (Count * Settings.Graphics.RingWidth) + 1 + ((nodeCurrentPosition.Right - nodeCurrentPosition.Left) / 2 * Settings.Graphics.RingRadius);
        Graphics.DrawCircle(nodeCurrentPosition.Center, radius, cachedContent.Color, Settings.Graphics.RingWidth, 32);

        return 1;
    }
    
    /// MARK: DrawWaypointLine
    /// Draws a line from the center of the screen to the specified map node on the atlas.
    /// </summary>
    /// <param name="mapNode">The atlas node to which the line will be drawn.</param>
    /// <remarks>
    /// This method checks if the feature to draw lines is enabled in the settings. If enabled, it finds the corresponding map settings
    /// for the given map node. If the map settings are found and the line drawing is enabled for that map, it proceeds to draw the line.
    /// Additionally, if the feature to draw line labels is enabled, it draws the node name and the distance to the node.
    /// </remarks>
    private void DrawWaypointLine(Node cachedNode)
    {
        
        if (cachedNode.IsVisited || cachedNode.IsAttempted || !cachedNode.MapType.DrawLine || !Settings.Features.DrawLines)
            return;

        RectangleF nodeCurrentPosition = cachedNode.MapNode.Element.GetClientRect();
        var distance = Vector2.Distance(screenCenter, nodeCurrentPosition.Center);

        if (distance < 400)
            return;

        Vector2 position = Vector2.Lerp(screenCenter, nodeCurrentPosition.Center, Settings.Graphics.LabelInterpolationScale);
        // Clamp position to screen
        var minX = Graphics.MeasureText($"{cachedNode.Name} ({distance:0})").X;
        var minY = Graphics.MeasureText($"{cachedNode.Name} ({distance:0})").Y;
        var maxX = GameController.Window.GetWindowRectangle().Width - Graphics.MeasureText($"{cachedNode.Name} ({distance:0})").X;
        var maxY = GameController.Window.GetWindowRectangle().Height - Graphics.MeasureText($"{cachedNode.Name} ({distance:0})").Y;
        // Clamp
        position.X = Math.Clamp(position.X, minX, maxX);
        position.Y = Math.Clamp(position.Y, minY, maxY);

        Graphics.DrawLine(position, nodeCurrentPosition.Center, Settings.Graphics.MapLineWidth, cachedNode.MapType.NodeColor);

        if (Settings.Features.DrawLineLabels) {
            DrawCenteredTextWithBackground( $"{cachedNode.Name} ({Vector2.Distance(screenCenter, nodeCurrentPosition.Center):0})", position, cachedNode.MapType.NameColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        }  
            
        
    }
    
    /// MARK: DrawMapNode
    /// Draws a highlighted circle around a map node on the atlas if the node is configured to be highlighted.
    /// </summary>
    /// <param name="mapNode">The atlas node description containing information about the map node to be drawn.</param>   
    private void DrawMapNode(Node cachedNode, RectangleF nodeCurrentPosition)
    {
        if (!Settings.MapTypes.HighlightMapNodes || cachedNode.IsVisited || !cachedNode.MapType.Highlight)
            return;
            
        var radius = (nodeCurrentPosition.Right - nodeCurrentPosition.Left) / 4 * Settings.Graphics.NodeRadius;
        var weight = cachedNode.Weight;
        Color color = Settings.MapTypes.ColorNodesByWeight ? ColorUtils.InterpolateColor(Settings.MapTypes.BadNodeColor, Settings.MapTypes.GoodNodeColor, (weight - minMapWeight) / (maxMapWeight - minMapWeight)) : cachedNode.MapType.NodeColor;
        Graphics.DrawCircleFilled(nodeCurrentPosition.Center, radius, color, 16);
    }

    //DrawMapNode
    private void DrawWeight(Node cachedNode, RectangleF nodeCurrentPosition)
    {
        if (!Settings.MapTypes.DrawWeightOnMap ||
            (!cachedNode.IsVisible && !Settings.MapTypes.ShowMapNamesOnHiddenNodes) ||
            (cachedNode.IsUnlocked && !Settings.MapTypes.ShowMapNamesOnUnlockedNodes) ||
            (!cachedNode.IsUnlocked && !Settings.MapTypes.ShowMapNamesOnLockedNodes) ||
            cachedNode.IsVisited || !cachedNode.MapType.Highlight)
            return;  

        // get the map weight % relative to the average map weight
        float weight = (cachedNode.Weight - minMapWeight) / (maxMapWeight - minMapWeight);  
         
        float offsetX = Settings.MapTypes.ShowMapNames ? (Graphics.MeasureText(cachedNode.Name.ToUpper()).X / 2) + 30 : 50;
        Vector2 position = new(nodeCurrentPosition.Center.X + offsetX, nodeCurrentPosition.Center.Y);

        DrawCenteredTextWithBackground($"{(int)(weight*100)}%", position, ColorUtils.InterpolateColor(Settings.MapTypes.BadNodeColor, Settings.MapTypes.GoodNodeColor, weight), Settings.Graphics.BackgroundColor, true, 10, 3);
    }
    /// <summary>
    /// Draws the name of the map on the atlas.
    /// </summary>
    /// <param name="cachedNode">The atlas node description containing information about the map.</param>
    private void DrawMapName(Node cachedNode, RectangleF nodeCurrentPosition)
    {
        if (!Settings.MapTypes.ShowMapNames ||
            (!cachedNode.IsVisible && !Settings.MapTypes.ShowMapNamesOnHiddenNodes) ||
            (cachedNode.IsUnlocked && !Settings.MapTypes.ShowMapNamesOnUnlockedNodes) ||
            (!cachedNode.IsUnlocked && !Settings.MapTypes.ShowMapNamesOnLockedNodes) ||
            cachedNode.IsVisited || !cachedNode.MapType.Highlight)
            return;

        Color fontColor = Settings.MapTypes.UseColorsForMapNames ? cachedNode.MapType.NameColor : Settings.Graphics.FontColor;
        Color backgroundColor = Settings.MapTypes.UseColorsForMapNames ? cachedNode.MapType.BackgroundColor : Settings.Graphics.BackgroundColor;
        
        if (Settings.MapTypes.UseWeightColorsForMapNames) {
            float weight = (cachedNode.Weight - minMapWeight) / (maxMapWeight - minMapWeight);
            fontColor = ColorUtils.InterpolateColor(Settings.MapTypes.BadNodeColor, Settings.MapTypes.GoodNodeColor, weight);
        }

        DrawCenteredTextWithBackground(cachedNode.Name.ToUpper(), nodeCurrentPosition.Center, fontColor, backgroundColor, true, 10, 3);
    }

    private void DrawTowerMods(Node cachedNode, RectangleF nodeCurrentPosition)
    {
        if ((cachedNode.IsTower && !Settings.MapMods.ShowOnTowers) || (!cachedNode.IsTower && !Settings.MapMods.ShowOnMaps) || !cachedNode.MapType.Highlight)    
            return; 

        Dictionary<string, Color> mods = [];

        var effects = new List<Effect>();
        if (cachedNode.IsTower) {            
            if (Settings.MapMods.ShowOnTowers) {                
                effects = cachedNode.Effects.Where(x => x.Value.Sources.Contains(cachedNode.Coordinates)).Select(x => x.Value).ToList();

                if (effects.Count == 0 && cachedNode.IsVisited)
                    DrawCenteredTextWithBackground("MISSING TABLET", nodeCurrentPosition.Center + new Vector2(0, Settings.MapMods.MapModOffset), Color.Red, Settings.Graphics.BackgroundColor, true, 10, 4);
                }
        } else {
            if (Settings.MapMods.ShowOnMaps && !cachedNode.IsVisited) {
                effects = cachedNode.Effects.Where(x => x.Value.Enabled).Select(x => x.Value).ToList();
            }
        }

        if (effects.Count == 0)
            return;
        
        foreach (var effect in effects) {
            if (Settings.MapMods.MapModTypes.TryGetValue(effect.ID.ToString(), out Mod mod)) {
                if (effect.Value1 >= mod.MinValueToShow) {
                    mods.TryAdd(effect.ToString(), mod.Color);
                }
            }
            
        }
        mods = mods.OrderBy(x => x.Value.ToString()).ToDictionary(x => x.Key, x => x.Value);
        DrawMapModText(mods, cachedNode.MapNode.Element.GetClientRect().Center);
    }
    private void DrawMapModText(Dictionary<string, Color> mods, Vector2 position)
    {      
        using (Graphics.SetTextScale(Settings.MapMods.MapModScale)) {
            string fullText = string.Join("\n", mods.Select(x => $"{x.Key}"));
            var boxSize = Graphics.MeasureText(fullText) + new Vector2(10, 10);
            var lineHeight = Graphics.MeasureText("A").Y;
            position -= new Vector2(boxSize.X / 2, boxSize.Y / 2);

            // offset the box below the node
            position += new Vector2(0, (boxSize.Y / 2) + Settings.MapMods.MapModOffset);
            
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

    /// MARK: DrawTowersWithinRange
    /// <summary>
    /// Draws lines between towers and maps within range of eachother.
    /// </summary>
    /// <param name="mapNode"></param>
    private void DrawTowerRange(Node cachedNode) {
        if (!cachedNode.DrawTowers || (cachedNode.IsVisited && !cachedNode.IsTower))
            return;

        if (cachedNode.IsTower) {
            DrawNodesWithinRange(cachedNode);
        } else {
            DrawTowersWithinRange(cachedNode);
        }
    }
    /// MARK: DrawTowersWithinRange
    /// <summary>
    ///  Draws lines between the current map node and any Lost Towers within range.
    /// </summary>
    /// <param name="cachedNode"></param>
    private void DrawTowersWithinRange(Node cachedNode) {
        if (!cachedNode.DrawTowers || cachedNode.IsVisited)
            return;

        var nearbyTowers = mapCache.Where(x => x.Value.IsTower && Vector2.Distance(x.Value.Coordinates, cachedNode.Coordinates) <= 11).Select(x => x.Value).AsParallel().ToList();
        if (nearbyTowers.Count == 0)
            return;

        Vector2 nodePos = cachedNode.MapNode.Element.GetClientRect().Center;
        Graphics.DrawCircle(nodePos, 50, Settings.Graphics.LineColor, 5, 16);

        foreach (var tower in nearbyTowers) {
            if (!mapCache.TryGetValue(tower.Coordinates, out Node towerNode))
                continue;

            var towerPosition = towerNode.MapNode.Element.GetClientRect();                        
            var endPos = towerPosition.Center;
            var distance = Vector2.Distance(nodePos, endPos);
            var direction = (endPos - nodePos) / distance;
            var offset = direction * 50;

            Graphics.DrawCircle(towerPosition.Center, 50, Settings.Graphics.LineColor, 5, 16);      
            Graphics.DrawLine(nodePos + offset, endPos - offset, Settings.Graphics.MapLineWidth, Settings.Graphics.LineColor);     
            DrawCenteredTextWithBackground($"{nearbyTowers.Count:0} towers in range", nodePos + new Vector2(0, -50), Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        }
    }

    /// MARK: DrawNodesWithinRange
    /// <summary>
    /// Draws lines between maps and tower within range of eachother.
    /// </summary>
    /// <param name="cachedNode"></param>
    private void DrawNodesWithinRange(Node cachedNode) {
        if (!cachedNode.DrawTowers)
            return;

        var nearbyMaps = mapCache.Where(x => x.Value.Name != "Lost Towers" && !x.Value.IsVisited && Vector2.Distance(x.Value.Coordinates, cachedNode.Coordinates) <= 11).Select(x => x.Value).AsParallel().ToList();
        if (nearbyMaps.Count == 0)
            return;
        Vector2 nodePos = cachedNode.MapNode.Element.GetClientRect().Center;
        Graphics.DrawCircle(nodePos, 50, Settings.Graphics.LineColor, 5, 16);

        foreach (var map in nearbyMaps) {
            if(!mapCache.TryGetValue(map.Coordinates, out Node nearbyMap))
                continue;

            var mapPosition = nearbyMap.MapNode.Element.GetClientRect();            
            var endPos = mapPosition.Center;
            var distance = Vector2.Distance(nodePos, endPos);
            var direction = (endPos - nodePos) / distance;
            var offset = direction * 50;

            Graphics.DrawCircle(mapPosition.Center, 50, Settings.Graphics.LineColor, 5, 16);  
            Graphics.DrawLine(nodePos + offset, endPos - offset, Settings.Graphics.MapLineWidth, Settings.Graphics.LineColor);     
            DrawCenteredTextWithBackground($"{nearbyMaps.Count:0} maps in range", nodePos + new Vector2(0, -50), Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        }
    }

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
        private void DrawGradientLine(Vector2 start, Vector2 end, Color startColor, Color endColor, float lineWidth)
    {
        // No need to draw a gradient if the colors are the same
        if (startColor == endColor)
        {
            Graphics.DrawLine(start, end, Settings.Graphics.MapLineWidth, startColor);
            return;
        }

        int segments = 10; // Number of segments to create the gradient effect
        Vector2 direction = (end - start) / segments;

        for (int i = 0; i < segments; i++)
        {
            Vector2 segmentStart = start + direction * i;
            Vector2 segmentEnd = start + direction * (i + 1);

            float t = (float)i / segments;
            Color segmentColor = ColorUtils.InterpolateColor(startColor, endColor, t);

            Graphics.DrawLine(segmentStart, segmentEnd, lineWidth, segmentColor);

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

        if (UI.OpenRightPanel.IsVisible)
            right -= UI.OpenRightPanel.GetClientRect().Width;

        if (UI.OpenLeftPanel.IsVisible || WaypointPanelIsOpen)
            left += Math.Max(UI.OpenLeftPanel.GetClientRect().Width, UI.SettingsPanel.GetClientRect().Width);

        RectangleF screenRect = new RectangleF(left, screen.Top, right - left, screen.Height);
        if (UI.WorldMap.GetChildAtIndex(9).IsVisible) {
            RectangleF mapTooltip = UI.WorldMap.GetChildAtIndex(9).GetClientRect();                
            mapTooltip.Inflate(mapTooltip.Width * 0.1f, mapTooltip.Height * 0.1f);

            if (mapTooltip.Contains(position))
                return false;
        }
        
        return screenRect.Contains(position);
    }

    public float GetDistanceToNode(Node cachedNode)
    {
        return Vector2.Distance(screenCenter, cachedNode.MapNode.Element.GetClientRect().Center);
    }
    
    #endregion
    #region Waypoint Panel
    private void DrawWaypointPanel() {
        ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2, ImGuiCond.FirstUseEver, new Vector2(0.5f, 0.5f));
        
        bool window_open = WaypointPanelIsOpen;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(15, 15));
        
        if (!ImGui.Begin("���������� �������� ����������##waypoint_panel", ref window_open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.End();
            ImGui.PopStyleVar(3);
            WaypointPanelIsOpen = window_open;
            return;
        }
        
        WaypointPanelIsOpen = window_open;

        // ���������
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.25f, 0.25f, 0.25f, 1.0f));
        
        if (ImGui.CollapsingHeader("��������� �����������", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // ��������� ���������� ��������� � ������� ��� ����� ����������� �����������
            if (ImGui.BeginTable("waypoint_settings_table", 2, ImGuiTableFlags.None, new Vector2(0, 0)))
            {
                // ��������� ���������� �� ����������
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                bool _showWaypoints = Settings.Waypoints.ShowWaypoints;
                if(ImGui.Checkbox($"##show_waypoints", ref _showWaypoints))                        
                    Settings.Waypoints.ShowWaypoints = _showWaypoints;

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
        }
        
        ImGui.PopStyleColor(3);
        ImGui.Spacing();

        // Tab menu ������ ������
        if (ImGui.BeginTabBar("WaypointsTabBar", ImGuiTabBarFlags.None))
        {
            // ������� ���������� �����������
            if (ImGui.BeginTabItem("���������� �����������"))
            {
                DrawWaypointManagementTab();
                ImGui.EndTabItem();
            }
            
            // ������� ������ �� ����� ������
            if (ImGui.BeginTabItem("����� ����"))
            {
                // ��������� ������������� ���������� ������ (�� ���� ��� ��� � 0.5 �������)
                if (needWaypointUpdate || DateTime.Now.Subtract(lastWaypointUpdate).TotalSeconds > 0.5)
                {
                    DrawAtlasSearchTab();
                    lastWaypointUpdate = DateTime.Now;
                    needWaypointUpdate = false;
                }
                else
                {
                    // ���������� ������������ ������ ��� ���������� ��� ���������� ������� ��������
                    DrawAtlasSearchTabCached();
                }
                ImGui.EndTabItem();
            }
            else
            {
                // ���� ������� �� �������, �������� ��� ��� ��������� �������� ����� �������� ������
                needWaypointUpdate = true;
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.End();
        ImGui.PopStyleVar(3);
    }
    
    // ���������� ������ DrawAtlasSearchTab, ������������ ������������ ������
    private void DrawAtlasSearchTabCached()
    {
        var panelSize = ImGui.GetContentRegionAvail();
        string regex = Settings.Waypoints.WaypointPanelFilter;

        // ����� ���������� - �������� UI �������� ��� ���������� ������� ��������
        ImGui.AlignTextToFramePadding();
        ImGui.Text("����������: ");
        ImGui.SameLine();
        
        var sortBy = Settings.Waypoints.WaypointPanelSortBy;
        
        if (ImGui.BeginCombo("##sort_by", sortBy))
        {
            bool changed = false;
            
            if (ImGui.Selectable("Weight", sortBy == "Weight")) {
                Settings.Waypoints.WaypointPanelSortBy = "Weight";
                changed = true;
            }
                
            if (ImGui.Selectable("Name", sortBy == "Name")) {
                Settings.Waypoints.WaypointPanelSortBy = "Name";
                changed = true;
            }
                
            if (ImGui.Selectable("Distance", sortBy == "Distance")) {
                Settings.Waypoints.WaypointPanelSortBy = "Distance";
                changed = true;
            }
            
            if (changed) {
                needWaypointUpdate = true;
            }
                
            ImGui.EndCombo();
        }
        
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        
        ImGui.Text("����. ���������: ");
        ImGui.SameLine();
        int maxItems = Settings.Waypoints.WaypointPanelMaxItems;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("##maxItems", ref maxItems)) {
            Settings.Waypoints.WaypointPanelMaxItems = maxItems;
            needWaypointUpdate = true;
        }

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        bool unlockedOnly = Settings.Waypoints.ShowUnlockedOnly;
        if (ImGui.Checkbox("������ ����������������", ref unlockedOnly)) {
            Settings.Waypoints.ShowUnlockedOnly = unlockedOnly;
            needWaypointUpdate = true;
        }

        ImGui.Spacing();
        
        ImGui.AlignTextToFramePadding();
        ImGui.Text("�����: ");
        ImGui.SameLine();
        
        ImGui.SetNextItemWidth(panelSize.X - 120);
        bool searchChanged = false;
        if (ImGui.InputText("##search", ref regex, 32, ImGuiInputTextFlags.EnterReturnsTrue)) {
            Settings.Waypoints.WaypointPanelFilter = regex;
            searchChanged = true;
        } else if (ImGui.IsItemDeactivatedAfterEdit()) {
            Settings.Waypoints.WaypointPanelFilter = regex;
            searchChanged = true;
        } else if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("����� �� �������� ����� ��� ������������. ������� Enter ��� ������.");
        }

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        bool useRegex = Settings.Waypoints.WaypointsUseRegex;
        if (ImGui.Checkbox("���������� ���������", ref useRegex)) {
            Settings.Waypoints.WaypointsUseRegex = useRegex;
            searchChanged = true;
        }
        
        if (searchChanged) {
            needWaypointUpdate = true;
        }
        
        ImGui.Separator();
    
        // ���������� ������������ ������� ��� ���������� ������� �������� ���������� � ����������
        var flags = ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.NoSavedSettings;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(2, 2));
        if (ImGui.BeginTable("atlas_list_table", 8, flags))
        {                                                            
            ImGui.TableSetupColumn("Map Name", ImGuiTableColumnFlags.WidthFixed, 200);   
            ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthFixed, 60);     
            ImGui.TableSetupColumn("Modifiers", ImGuiTableColumnFlags.WidthFixed, 100); 
            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Unlocked", ImGuiTableColumnFlags.WidthFixed, 28);
            ImGui.TableSetupColumn("Way", ImGuiTableColumnFlags.WidthFixed, 32);
            ImGui.TableHeadersRow();                    

            // ���������� ������������ ������
            RenderFilteredMapTable(filteredMapCache);
            
            ImGui.PopStyleVar(2);
            ImGui.EndTable();
        }
    }
    
    // ����������� ������� ���� ��� ��������� ���������� � ����������
    private void RenderFilteredMapTable(Dictionary<Vector2i, Node> tempCache)
    {
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
                foreach (var (_, content) in node.Content) {
                    _color = content.Color;
                    _colorVector = new Vector4(_color.R / 255.0f, _color.G / 255.0f, _color.B / 255.0f, _color.A / 255.0f);
                    ImGui.TextColored(_colorVector, content.Name);
                }
                
                ImGui.TableNextColumn();
                foreach (var (_, effect) in node.Effects) {
                    if (!effect.Enabled) continue;
                    
                    _color = Color.FromArgb(
                        255,
                        (byte)Math.Min(255, 255 * (effect.Value1 > 0 ? 1.0f : 0.2f)),
                        (byte)Math.Min(255, 255 * (Math.Abs(effect.Value1) < 0.01 ? 1.0f : 0.2f)),
                        (byte)Math.Min(255, 255 * (effect.Value1 < 0 ? 1.0f : 0.2f))
                    );
                    
                    _colorVector = new Vector4(_color.R / 255.0f, _color.G / 255.0f, _color.B / 255.0f, _color.A / 255.0f);
                    ImGui.TextColored(_colorVector, $"{effect.Name} {effect.Value1:+#;-#;0}");
                }

                // Weight
                ImGui.TableNextColumn();
                _color = ColorUtils.InterpolateColor(Settings.MapTypes.BadNodeColor, Settings.MapTypes.GoodNodeColor, (node.Weight - minMapWeight) / (maxMapWeight - minMapWeight));
                _colorVector = new Vector4(_color.R / 255.0f, _color.G / 255.0f, _color.B / 255.0f, _color.A / 255.0f);
                ImGui.TextColored(_colorVector, $"{node.Weight:0.#}");   

                // Unlocked
                ImGui.TableNextColumn();
                if (ImGui.Checkbox("", ref node.IsUnlocked))
                    needWaypointUpdate = true;
                     
                // Waypoint
                ImGui.TableNextColumn();
                bool hasWaypoint = Settings.Waypoints.Waypoints.ContainsKey(node.Coordinates.ToString());
                if (ImGui.Checkbox("##", ref hasWaypoint))
                {
                    if (hasWaypoint)                             
                        AddWaypoint(node);
                    else 
                        RemoveWaypoint(node);
                }
                
                ImGui.SetWindowFontScale(1.0f);
                ImGui.PopID();
            }
        }
    }
    
    private void DrawAtlasSearchTab()
    {
        var panelSize = ImGui.GetContentRegionAvail();
        string regex = Settings.Waypoints.WaypointPanelFilter;

        // ����� ����������
        ImGui.AlignTextToFramePadding();
        ImGui.Text("����������: ");
        ImGui.SameLine();
        
        var sortBy = Settings.Waypoints.WaypointPanelSortBy;
        
        if (ImGui.BeginCombo("##sort_by", sortBy))
        {
            if (ImGui.Selectable("Weight", sortBy == "Weight"))
                Settings.Waypoints.WaypointPanelSortBy = "Weight";
                
            if (ImGui.Selectable("Name", sortBy == "Name"))
                Settings.Waypoints.WaypointPanelSortBy = "Name";
                
            if (ImGui.Selectable("Distance", sortBy == "Distance"))
                Settings.Waypoints.WaypointPanelSortBy = "Distance";
                
            ImGui.EndCombo();
        }
        
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        
        ImGui.Text("����. ���������: ");
        ImGui.SameLine();
        int maxItems = Settings.Waypoints.WaypointPanelMaxItems;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("##maxItems", ref maxItems))
            Settings.Waypoints.WaypointPanelMaxItems = maxItems; 

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        bool unlockedOnly = Settings.Waypoints.ShowUnlockedOnly;
        if (ImGui.Checkbox("������ ����������������", ref unlockedOnly))
            Settings.Waypoints.ShowUnlockedOnly = unlockedOnly;

        ImGui.Spacing();
        
        ImGui.AlignTextToFramePadding();
        ImGui.Text("�����: ");
        ImGui.SameLine();
        
        ImGui.SetNextItemWidth(panelSize.X - 120);
        if (ImGui.InputText("##search", ref regex, 32, ImGuiInputTextFlags.EnterReturnsTrue)) {
            Settings.Waypoints.WaypointPanelFilter = regex;
        } else if (ImGui.IsItemDeactivatedAfterEdit()) {
            Settings.Waypoints.WaypointPanelFilter = regex;
        } else if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("����� �� �������� ����� ��� ������������. ������� Enter ��� ������.");
        }

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        bool useRegex = Settings.Waypoints.WaypointsUseRegex;
        if (ImGui.Checkbox("���������� ���������", ref useRegex))
            Settings.Waypoints.WaypointsUseRegex = useRegex;
        
        ImGui.Separator();
    
        // �������� AsParallel().ToDictionary �� ������� ��� ��������
        var tempCache = mapCache.Where(x => !x.Value.IsVisited && (!Settings.Waypoints.ShowUnlockedOnly || x.Value.IsUnlocked)).ToDictionary(x => x.Key, x => x.Value);    
        // if search isnt blank
        if (!string.IsNullOrEmpty(Settings.Waypoints.WaypointPanelFilter)) {
            if (useRegex) {
                tempCache = tempCache.Where(x => Regex.IsMatch(x.Value.Name, Settings.Waypoints.WaypointPanelFilter, RegexOptions.IgnoreCase) || x.Value.MatchEffect(Settings.Waypoints.WaypointPanelFilter) || x.Value.Content.Any(x => x.Value.Name == Settings.Waypoints.WaypointPanelFilter)).ToDictionary(x => x.Key, x => x.Value);
            } else {
                tempCache = tempCache.Where(x => x.Value.Name.Contains(Settings.Waypoints.WaypointPanelFilter, StringComparison.CurrentCultureIgnoreCase) || x.Value.MatchEffect(Settings.Waypoints.WaypointPanelFilter) || x.Value.Content.Any(x => x.Value.Name == Settings.Waypoints.WaypointPanelFilter)).ToDictionary(x => x.Key, x => x.Value);
            }
        }
        
        // ��������� ���������� �� ���������� ������
        switch (sortBy)
        {
            case "Distance":
                try 
                {
                    // Всегда используем центр экрана (определенную точку)
                    Vector2 referencePos = screenCenter;
                    referencePositionText = "from geographic center";
                    
                    // Обновляем массив с их расстояниями для сортировки
                    mapItems.Clear();
                    
                    // Создаем список объектов MapSearchItem с расстояниями
                    foreach (var kvp in tempCache) {
                        var node = kvp.Value;
                        if (node == null || node.MapNode?.Element == null) continue;
                        
                        float distance = float.MaxValue;
                        try {
                            distance = Vector2.Distance(referencePos, node.MapNode.Element.GetClientRect().Center);
                        } catch {}
                        
                        mapItems.Add(new MapSearchItem(node, distance));
                    }
                    
                    // Сортируем по возрастанию расстояния (ближние карты первыми)
                    results = mapItems.OrderBy(item => item.Distance)
                                               .Select(item => item.MapNode)
                                               .ToList();
                }
                catch 
                {
                    // При ошибке сортируем по весу
                    results = tempCache.Values.OrderByDescending(x => x.Weight).ToList();
                }
                break;
                
            case "Name":
                results = tempCache.Values.OrderBy(n => n.Name).ToList();
                break;
                
            case "Weight":
            default:
                results = tempCache.Values.OrderByDescending(n => n.Weight).ToList();
                break;
        }
        
        //   
        results = results.Take(Settings.Waypoints.WaypointPanelMaxItems).ToList();

        //       
        filteredMapCache = new Dictionary<Vector2i, Node>(tempCache);
        
        var flags = ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.NoSavedSettings;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2)); // Adjust the padding values as needed
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(2, 2)); // A
        if (ImGui.BeginTable("atlas_list_table", 8, flags))
        {                                                            
            ImGui.TableSetupColumn("Map Name", ImGuiTableColumnFlags.WidthFixed, 200);   
            ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthFixed, 60);     
            ImGui.TableSetupColumn("Modifiers", ImGuiTableColumnFlags.WidthFixed, 100); 
            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Unlocked", ImGuiTableColumnFlags.WidthFixed, 28);
            ImGui.TableSetupColumn("Way", ImGuiTableColumnFlags.WidthFixed, 32);
            ImGui.TableHeadersRow();                    

            // �������� ��������� ������� � ��������� �����
            RenderFilteredMapTable(tempCache);
            
            ImGui.PopStyleVar(2);
            ImGui.EndTable();
        }
    }
    
    // ������������ ����� ���������� ����������� ������
    private void UpdateSearchResults() {
        try {
            // ��������� ������������� ���������� ������
            if (!needSearchUpdate && DateTime.Now.Subtract(lastSearchUpdate).TotalSeconds <= 0.5 && 
                Settings.Search.SearchQuery == previousSearchQuery) {
                return;
            }
            
            lastSearchUpdate = DateTime.Now;
            needSearchUpdate = false;
            previousSearchQuery = Settings.Search.SearchQuery;
            
            if (!AtlasPanel?.IsVisible == true) {
                searchResults.Clear();
                mapItems.Clear();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(Settings.Search.SearchQuery)) {
                searchResults.Clear();
                mapItems.Clear();
                return;
            }

            string searchQuery = Settings.Search.SearchQuery.ToLower();
            // Check for property search syntax: prop:value
            bool isPropertySearch = false;
            string propertyName = "";
            string propertyValue = "";
            
            if (searchQuery.Contains(":")) {
                var parts = searchQuery.Split(':', 2);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1])) {
                    isPropertySearch = true;
                    propertyName = parts[0].Trim().ToLower();
                    propertyValue = parts[1].Trim().ToLower();
                }
            }

            searchResults.Clear();
            previousSearchQuery = searchQuery;

            var searchTask = Task.Run(() => {
                List<Node> results = new List<Node>();
                
                lock (mapCacheLock) {
                    IEnumerable<Node> query;
                    
                    if (isPropertySearch) {
                        query = mapCache.Values
                            // Exclude completed maps (visited and unlocked)
                            .Where(node => !(node.IsVisited && node.IsUnlocked))
                            .Where(node => {
                            // Search by property type
                            switch (propertyName) {
                                case "content":
                                case "type":
                                    return node.Content != null && 
                                           node.Content.Any(c => c.Value.Name.ToLower().Contains(propertyValue));
                                
                                case "effect":
                                case "mod":
                                    return node.Effects != null && 
                                           node.Effects.Any(e => e.Value.Name.ToLower().Contains(propertyValue) || 
                                                               e.Value.Description.ToLower().Contains(propertyValue));
                                
                                case "biome":
                                    return node.Biomes != null && 
                                           node.Biomes.Any(b => b.Value.Name.ToLower().Contains(propertyValue));
                                
                                case "name":
                                    return node.Name.ToLower().Contains(propertyValue) ||
                                          (node.MapType?.Name?.ToLower()?.Contains(propertyValue) == true);
                                
                                case "status":
                                    if (propertyValue.Contains("visit"))
                                        return node.IsVisited;
                                    else if (propertyValue.Contains("unlock"))
                                        return node.IsUnlocked;
                                    else if (propertyValue.Contains("lock"))
                                        return !node.IsUnlocked;
                                    else if (propertyValue.Contains("hidden"))
                                        return !node.IsVisible;
                                    else if (propertyValue.Contains("tower"))
                                        return node.IsTower;
                                    else if (propertyValue.Contains("fail"))
                                        return node.IsVisited && !node.IsUnlocked;
                                    return false;
                                
                                default:
                                    // Try generic property search across all properties
                                    return node.Name.ToLower().Contains(propertyValue) ||
                                          (node.MapType?.Name?.ToLower()?.Contains(propertyValue) == true) ||
                                           node.Effects.Any(e => (e.Value.Name.ToLower().Contains(propertyValue) || 
                                                               e.Value.Description.ToLower().Contains(propertyValue))) ||
                                           node.Content.Any(c => c.Value.Name.ToLower().Contains(propertyValue)) ||
                                           node.Biomes.Any(b => b.Value.Name.ToLower().Contains(propertyValue));
                            }
                        });
                    } else {
                        // Standard search (no property specified)
                        query = mapCache.Values
                            // Exclude completed maps (visited and unlocked)
                            .Where(node => !(node.IsVisited && node.IsUnlocked))
                            .Where(node => node.Name.ToLower().Contains(searchQuery) || 
                                   (node.MapType?.Name?.ToLower()?.Contains(searchQuery) == true) ||
                                   node.Effects.Any(e => (e.Value.Name.ToLower().Contains(searchQuery) || 
                                                       e.Value.Description.ToLower().Contains(searchQuery))) ||
                                   node.Content.Any(c => c.Value.Name.ToLower().Contains(searchQuery)) ||
                                   node.Biomes.Any(b => b.Value.Name.ToLower().Contains(searchQuery)));
                    }
                    
                    // Apply sorting
                    switch (Settings.Search.SortBy) {
                        case "Distance":
                            try {
                                // �������� ��������� ������� ��� ����������
                                var cursorNode = GetClosestNodeToCursor();
                                Vector2 referencePos;
                                
                                if (cursorNode != null) {
                                    // ���� ������ ��� ������, ���������� ��� �������
                                    referencePos = cursorNode.MapNode.Element.GetClientRect().Center;
                                    referencePositionText = $"�� ����� {cursorNode.Name}";
                                } else {
                                    // ���� ������ �� ��� ������, ���������� ����� ������
                                    referencePos = screenCenter;
                                    referencePositionText = "�� ������ ������";
                                }
                                
                                // ��������� ����� � �� ������������ ��� �����������
                                mapItems.Clear();
                                
                                // ������� ������ �������� MapSearchItem � ������������
                                foreach (var node in query) {
                                    if (node == null || node.MapNode?.Element == null) continue;
                                    
                                    float distance = float.MaxValue;
                                    try {
                                        distance = Vector2.Distance(referencePos, node.MapNode.Element.GetClientRect().Center);
                                    } catch {}
                                    
                                    mapItems.Add(new MapSearchItem(node, distance));
                                }
                                
                                // ��������� �� ����������� ���������� (������� ����� �������)
                                results = mapItems.OrderBy(item => item.Distance)
                                                       .Select(item => item.MapNode)
                                                       .ToList();
                            }
                            catch (Exception) {
                                // �������� ������� ��� ������ - ���������� �� ����
                                results = query.OrderByDescending(n => n.Weight).ToList();
                            }
                            break;
                        case "Name":
                            results = query.OrderBy(n => n.Name).ToList();
                            break;
                        case "Status":
                            results = query.OrderBy(n => n.IsVisited)
                                               .ThenBy(n => !n.IsUnlocked)
                                               .ThenBy(n => !n.IsVisible)
                                               .ThenByDescending(n => n.Weight)
                                               .ToList();
                            break;
                        case "Weight":
                        default:
                            results = query.OrderByDescending(n => n.Weight).ToList();
                            break;
                    }
                }
                
                return results;
            });
            
            // ������������ ����� �������� ����������� ������
            if (searchTask.Wait(500)) {
                searchResults = searchTask.Result;
            }
        }
        catch (Exception ex) {
            LogError($"Error in UpdateSearchResults: {ex.Message}");
        }
    }

    private void DrawSearchPanel() {
        if (!Settings.Search.PanelIsOpen || !AtlasPanel?.IsVisible == true) return;

        // ��������� ��������� ������� ���� � ���������
        var windowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.HorizontalScrollbar;
        
        ImGui.SetNextWindowPos(searchPanelPosition, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(900, 600), ImGuiCond.FirstUseEver);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5, 5));
        
        bool isOpen = Settings.Search.PanelIsOpen;
        if (ImGui.Begin("Map Search###MapSearchPanel", ref isOpen, windowFlags)) {
            Settings.Search.PanelIsOpen = isOpen;
            searchPanelPosition = ImGui.GetWindowPos();
            
            // Header section with better organized search controls
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.17f, 0.17f, 0.2f, 0.8f));
            ImGui.BeginChild("SearchHeader", new Vector2(-1, 80), ImGuiChildFlags.None);
            
            // Search input row with better alignment
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Search maps:"); 
            ImGui.SameLine();
            string searchQuery = Settings.Search.SearchQuery;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 80);
            
            // �������� ��������� ����� � ���� ������, ��������� ���������� �����
            if (ImGui.InputText("###SearchQuery", ref searchQuery, 100, ImGuiInputTextFlags.None))
            {
                // ������ ��������� ��������� �����, �� �� ���������� �����
                Settings.Search.SearchQuery = searchQuery;
                // ��������, ��� ����� ����� ��������, �� ������ ��� � ���������
                needSearchUpdate = true;
            }
            
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.7f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.8f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.4f, 0.6f, 1.0f));
            // ������������ ����� ��� ������� �� ������ ��� �� �������
            if (ImGui.Button("Search", new Vector2(75, 0))) {
                UpdateSearchResults();
            }
            ImGui.PopStyleColor(3);
            
            // Controls row
            ImGui.Spacing();
            ImGui.BeginGroup();
            
            // Sorting controls with better styling
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Sort by:");
            ImGui.SameLine();
            
            string[] sortOptions = new[] { "Distance", "Weight", "Name", "Status" };
            int sortIndex = Array.IndexOf(sortOptions, Settings.Search.SortBy);
            if (sortIndex < 0) sortIndex = 0;
            
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.25f, 1.0f));
            ImGui.SetNextItemWidth(120);
            if (ImGui.Combo("###SortBy", ref sortIndex, sortOptions, sortOptions.Length)) {
                Settings.Search.SortBy = sortOptions[sortIndex];
                UpdateSearchResults();
            }
            ImGui.PopStyleColor();
            
            // ���������� ������� ����� ������� ��� ��������� ����������, ���� ������� ���������� �� ����������
            if (Settings.Search.SortBy == "Distance") {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 0.5f, 1.0f), $"({referencePositionText})");
                if (ImGui.IsItemHovered()) {
                    ImGui.BeginTooltip();
                    ImGui.Text("���������� ���������� �� �������������� ������ (����� ������).");
                    ImGui.EndTooltip();
                }
            }
            
            ImGui.SameLine(0, 20);
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Max items:");
            ImGui.SameLine();
            
            int maxItems = Settings.Search.SearchPanelMaxItems;
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.25f, 1.0f));
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("###MaxItems", ref maxItems, 5, 10)) {
                if (maxItems < 5) maxItems = 5;
                if (maxItems > 100) maxItems = 100;
                Settings.Search.SearchPanelMaxItems = maxItems;
            }
            ImGui.PopStyleColor();
            
            ImGui.EndGroup();
            ImGui.EndChild();
            ImGui.PopStyleColor();
            
            // Main layout with results and help panel
            ImGui.Columns(2, "search_layout", false);
            ImGui.SetColumnWidth(0, ImGui.GetWindowWidth() * 0.72f); // results get 72% of width
            
            // LEFT COLUMN - Results table with more height for results
            if (ImGui.BeginTable("search_results_table", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, ImGui.GetContentRegionAvail().Y - 40))) {
                ImGui.TableSetupColumn("Map Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Coordinates", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 90);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 60);
                
                // ���������� ������� � �����������, ���� ������� ���������� �� ����������
                if (Settings.Search.SortBy == "Distance") {
                    ImGui.TableSetupColumn("����������", ImGuiTableColumnFlags.WidthFixed, 90);
                }
                
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80);
                
                ImGui.TableSetupScrollFreeze(0, 1); // Freeze header row
                ImGui.TableHeadersRow();
                
                ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(0.19f, 0.19f, 0.22f, 0.9f));
                ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(0.22f, 0.22f, 0.25f, 0.9f));
                
                int visibleRows = 0;
                foreach (var node in searchResults) {
                    if (visibleRows >= Settings.Search.SearchPanelMaxItems) break;
                    
                    if (node == null) continue;
                    visibleRows++;
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    
                    // Map Name
                    ImGui.TextColored(
                        node.IsUnlocked 
                            ? new Vector4(0.9f, 0.9f, 1.0f, 1.0f) 
                            : new Vector4(0.6f, 0.6f, 0.7f, 1.0f), 
                        node.Name);
                    
                    // Coordinates
                    ImGui.TableNextColumn();
                    ImGui.Text($"{node.Coordinates.X}, {node.Coordinates.Y}");
                    
                    // Status
                    ImGui.TableNextColumn();
                    string status = node.IsVisited ? "Visited" : node.IsUnlocked ? "Unlocked" : node.IsVisible ? "Locked" : "Hidden";
                    ImGui.TextColored(
                        node.IsVisited 
                            ? new Vector4(0.5f, 0.5f, 0.5f, 1.0f) 
                            : node.IsUnlocked 
                                ? new Vector4(0.2f, 0.8f, 0.2f, 1.0f) 
                                : node.IsVisible 
                                    ? new Vector4(0.8f, 0.2f, 0.2f, 1.0f) 
                                    : new Vector4(0.8f, 0.8f, 0.2f, 1.0f), 
                        status);
                    
                    // Type/Content - show icons or text for map content
                    ImGui.TableNextColumn();
                    if (node.Content.Count > 0) {
                        string contentTypes = string.Join(", ", node.Content.Select(c => c.Value.Name));
                        ImGui.TextWrapped(contentTypes);
                        if (ImGui.IsItemHovered() && contentTypes.Length > 10) {
                            ImGui.BeginTooltip();
                            ImGui.Text(contentTypes);
                            ImGui.EndTooltip();
                        }
                    } else {
                        ImGui.Text("-");
                    }
                    
                    // Weight
                    ImGui.TableNextColumn();
                    float normalizedWeight = (node.Weight - minMapWeight) / (maxMapWeight - minMapWeight);
                    ImGui.TextColored(
                        ColorHelper.GetColorForWeight(normalizedWeight),
                        $"{node.Weight:0.#}");
                        
                    // Distance column (only shown if sorting by distance)
                    if (Settings.Search.SortBy == "Distance") {
                        ImGui.TableNextColumn();
                        
                        // ������� ��������������� ������� � ����������� � ����������
                        float distance = mapItems.FirstOrDefault(i => i.MapNode == node)?.Distance ?? float.MaxValue;
                        
                        if (distance < float.MaxValue) {
                            // ����������� ���������� ��� ������ (�������� 50 ��� ����� ������)
                            float normalizedDistance = Math.Min(distance / 50f, 1.0f);
                            // ����������� ��� ����� (������� - �������, ������� - �������)
                            Vector4 distanceColor = ColorHelper.GetColorForDistance(1.0f - normalizedDistance);
                            
                            // ���������� ���������� � �������� ����������
                            ImGui.TextColored(distanceColor, $"{distance:0}");
                        } else {
                            ImGui.Text("-");
                        }
                    }
                    
                    // Actions
                    ImGui.TableNextColumn();
                    
                    string buttonId = $"waypoint_{node.Coordinates}";
                    bool hasWaypoint = Settings.Waypoints.Waypoints.ContainsKey(node.Coordinates.ToString());
                    
                    // Show add/remove waypoint button
                    if (hasWaypoint) {
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1.0f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1.0f));
                        if (ImGui.Button($"Remove##{buttonId}")) {
                            RemoveWaypoint(node);
                        }
                        ImGui.PopStyleColor(2);
                    } else {
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1.0f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.3f, 1.0f));
                        if (ImGui.Button($"Add##{buttonId}")) {
                            AddWaypoint(node);
                        }
                        ImGui.PopStyleColor(2);
                    }
                }
                
                ImGui.PopStyleColor(2); // Pop the TableRow colors
                ImGui.EndTable();
            }
            
            // RIGHT COLUMN - Help section and filters
            ImGui.NextColumn();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.17f, 0.17f, 0.2f, 0.8f));
            ImGui.BeginChild("HelpPanel", new Vector2(0, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.None);
            
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.9f, 0.9f, 1.0f));
            ImGui.TextWrapped("��������� �������:");
            ImGui.Separator();
            
            ImGui.TextWrapped("����������� ������� ����� ��� ������ �� �������� �����, �������� ��� �����������.");
            ImGui.Spacing();
            ImGui.TextWrapped("��� ������������ ������ ����������� ������ [���]:[��������]:");
            ImGui.Spacing();
            
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.9f, 0.7f, 1.0f));
            ImGui.Bullet(); ImGui.SameLine(); ImGui.TextWrapped("name:beach - ����� ���� � 'beach' � ��������");
            ImGui.Bullet(); ImGui.SameLine(); ImGui.TextWrapped("content:breach - ����� ���� � ��������� 'breach'");
            ImGui.Bullet(); ImGui.SameLine(); ImGui.TextWrapped("effect:ritual - ����� ���� � �������� 'ritual'");
            ImGui.Bullet(); ImGui.SameLine(); ImGui.TextWrapped("biome:cave - ����� ���� � ������ 'cave'");
            ImGui.Bullet(); ImGui.SameLine(); ImGui.TextWrapped("status:locked - ����� ��������������� ����");
            ImGui.PopStyleColor();
            
            ImGui.Spacing();
            ImGui.TextWrapped("���������� �� ����������:");
            ImGui.Separator();
            ImGui.TextWrapped("��� ���������� �� ���������� ������ ���������� �� ��������� ������ ������� �� ������.");
            ImGui.Spacing();
            ImGui.TextWrapped("�������� ������ �� ����� ����� ������, ����� ������������ � ��� ����� �������.");
            ImGui.PopStyleColor();
            
            ImGui.EndChild();
            ImGui.PopStyleColor();
            
            ImGui.Columns(1);
            ImGui.End();
        }
        ImGui.PopStyleVar(3);
    }
    #endregion

    private void SortDistanceColumn()
    {
        try
        {
            // Получаем позицию игрока для расчета расстояния
            Vector2 referencePosition = GetPlayerPositionForDistance();
            
            // Словарь для хранения расстояний до карт
            var cachedDistances = new Dictionary<string, float>();
            
            // Рассчитываем расстояние для каждой карты
            foreach (var mapItem in currentItems)
            {
                try
                {
                    string mapItemName = mapItem.Name;
                    if (string.IsNullOrEmpty(mapItemName)) continue;
                    
                    float distance = float.MaxValue; // Значение по умолчанию
                    
                    // Ищем по названию карты в словаре узлов
                    if (nodesByName.TryGetValue(mapItemName, out var matchingNodes) && 
                        matchingNodes != null && matchingNodes.Count > 0)
                    {
                        // Берем первый подходящий узел
                        var node = matchingNodes.FirstOrDefault();
                        if (node?.MapNode?.Element != null)
                        {
                            Vector2 nodePos = node.MapNode.Element.GetClientRect().Center;
                            distance = Vector2.Distance(referencePosition, nodePos);
                        }
                    }
                    
                    // Сохраняем расстояние в кэше
                    cachedDistances[mapItemName] = distance;
                }
                catch (Exception ex)
                {
                    // Если произошла ошибка при расчете расстояния для карты, устанавливаем максимальное расстояние
                    try
                    {
                        LogError($"Ошибка при расчете расстояния для карты: {ex.Message}");
                        cachedDistances[mapItem.Name] = float.MaxValue;
                    }
                    catch
                    {
                        // Игнорируем ошибки в обработчике ошибок
                    }
                }
            }
            
            // Сортируем карты по расстоянию (ближайшие первыми)
            currentItems.Sort((a, b) => 
            {
                float distA = cachedDistances.TryGetValue(a.Name, out float dA) ? dA : float.MaxValue;
                float distB = cachedDistances.TryGetValue(b.Name, out float dB) ? dB : float.MaxValue;
                return distA.CompareTo(distB);
            });
        }
        catch (Exception ex)
        {
            try
            {
                LogError($"Ошибка при сортировке карт по расстоянию: {ex.Message}");
            }
            catch
            {
                // Игнорируем ошибки в обработчике ошибок
            }
        }
    }

    /// <summary>
    /// Получает позицию игрока для расчета расстояния в картах
    /// </summary>
    private Vector2 GetPlayerPositionForDistance()
    {
        try
        {
            // Проверяем, используется ли ручная точка отсчета
            if (useManualReferencePosition && manualReferencePosition != Vector2.Zero)
            {
                referencePositionText = "from custom point";
                LogMessage($"Distance is calculated from manual point: {manualReferencePosition}");
                return manualReferencePosition;
            }
            
            // Приоритет - позиция игрока
            if (GameController?.Game?.IngameState?.Data?.LocalPlayer != null)
            {
                try {
                    // Пытаемся получить позицию игрока из доступных компонентов
                    var player = GameController.Game.IngameState.Data.LocalPlayer;
                    var playerPos = Vector2.Zero;
                    
                    // Пробуем разные способы получить позицию игрока
                    try {
                        // Метод 1: получаем позицию через адрес игрока
                        if (player != null && player.Address != IntPtr.Zero) {
                            try {
                                // Самый простой вариант - прямой доступ к Pos
                                var pos = player.Pos;
                                if (pos != null) {
                                    // Преобразуем координаты в Vector2
                                    playerPos = new Vector2(pos.X, pos.Y);
                                    LogMessage($"Player position obtained: {playerPos}");
                                }
                                else {
                                    LogMessage("Player position not defined, using screen center");
                                }
                            }
                            catch (Exception ex) {
                                LogError($"Error getting position: {ex.Message}");
                            }
                        }
                        else {
                            LogMessage("Entity player is not accessible, using screen center");
                        }
                    }
                    catch (Exception ex) { 
                        LogError($"Error accessing player data: {ex.Message}");
                    }
                    
                    // Если позиция все еще не получена, используем последнюю известную позицию
                    if (playerPos == Vector2.Zero) {
                        playerPos = screenCenter;
                    }
                    
                    referencePositionText = "from player position";
                    LogMessage($"Distance is calculated from player position: {playerPos}");
                    return playerPos;
                } catch (Exception ex) {
                    LogError($"Error getting player position: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                LogError($"Error getting position for distance calculation: {ex.Message}");
            }
            catch
            {
                // Игнорируем ошибки в обработчике ошибок
            }
        }
        
        // Используем центр экрана как запасной вариант
        referencePositionText = "from screen center";
        LogMessage($"Distance is calculated from screen center: {screenCenter}");
        return screenCenter;
    }

    private void DrawExtraMapSearch()
    {
        // Проверяем, нужно ли отображать панель поиска
        if (!Settings.Search.ShowMapSearch) return;
        
        // Получаем состояние кнопки сортировки по расстоянию
        if (distanceHeaderClicked) {
            SortDistanceColumn();
            distanceHeaderClicked = false;
        }
        
        // Отрисовываем панель поиска карт
        Vector2 panelPos = new Vector2(GameController.Window.GetWindowRectangle().Width - 350, 100);
        ImGui.SetNextWindowPos(panelPos, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
        
        bool showMapSearch = Settings.Search.ShowMapSearch;
        if (ImGui.Begin("Quick Map Search##quick_map_search", ref showMapSearch))
        {
            // Обновляем значение в настройках
            Settings.Search.ShowMapSearch = showMapSearch;
            
            // Поле поиска
            ImGui.InputText("Search", ref query, 128);
            
            if (ImGui.Button("Find") || ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                // Выполняем поиск
                UpdateSearchResults();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Clear"))
            {
                query = "";
                results.Clear();
            }
            
            // Отображаем результаты
            if (results.Count > 0)
            {
                ImGui.Separator();
                
                if (ImGui.BeginTable("search_results", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableHeadersRow();
                    
                    foreach (var node in results.Take(Settings.Search.SearchPanelMaxItems))
                    {
                        ImGui.TableNextRow();
                        
                        ImGui.TableNextColumn();
                        ImGui.Text(node.Name);
                        
                        ImGui.TableNextColumn();
                        float distance = Vector2.Distance(screenCenter, node.MapNode.Element.GetClientRect().Center);
                        ImGui.Text($"{distance:F0}");
                        
                        ImGui.TableNextColumn();
                        if (ImGui.Button($"Waypoint##add_{node.Coordinates.X}_{node.Coordinates.Y}"))
                        {
                            AddWaypoint(node);
                        }
                    }
                    
                    ImGui.EndTable();
                }
            }
            else if (!string.IsNullOrEmpty(query))
            {
                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "No results found for your query");
            }
        }
        
        ImGui.End();
    }

    // Добавляем недостающие методы для работы с путевыми точками
    private void DrawWaypoint(Waypoint waypoint)
    {
        if (!Settings.Waypoints.ShowWaypoints || !waypoint.Show) return;
        
        try {
            Vector2 position = new Vector2(waypoint.Coordinates.X, waypoint.Coordinates.Y);
            float size = 25.0f * waypoint.Scale;
            
            // Рисуем иконку маркера на карте
            var rect = new RectangleF(position.X - size/2, position.Y - size/2, size, size);
            // Используем угол как RectangleF (в API ExileCore2 ожидается RectangleF для угла)
            var angleRect = new RectangleF(0, 0, 0, 0);
            Graphics.DrawImage(ArrowTexturePath, rect, angleRect, waypoint.Color);
            
            // Рисуем название, если оно есть
            if (!string.IsNullOrEmpty(waypoint.Name)) {
                Vector2 textPos = position + new Vector2(0, size / 2 + 5);
                DrawCenteredTextWithBackground(waypoint.Name, textPos, waypoint.Color, 
                    Color.FromArgb(200, 0, 0, 0), true, 10, 5);
            }
        }
        catch (Exception e) {
            LogError($"Error drawing waypoint: {e.Message}\n{e.StackTrace}");
        }
    }

    private void DrawWaypointArrow(Waypoint waypoint)
    {
        if (!Settings.Waypoints.ShowWaypointArrows || !waypoint.Arrow) return;
        
        try {
            Vector2 position = new Vector2(waypoint.Coordinates.X, waypoint.Coordinates.Y);
            Vector2 direction = position - screenCenter;
            
            // Если точка слишком близко или уже на экране, не рисуем стрелку
            if (direction.Length() < 100 || IsOnScreen(position)) return;
            
            // Вычисляем угол для направления стрелки
            float angle = (float)Math.Atan2(direction.Y, direction.X);
            
            // Смещаем стрелку на край экрана
            float screenWidth = GameController.Window.GetWindowRectangle().Width;
            float screenHeight = GameController.Window.GetWindowRectangle().Height;
            
            // Находим точку пересечения с краем экрана
            Vector2 edgePoint = CalculateScreenEdgePoint(screenCenter, direction, screenWidth, screenHeight);
            
            // Рисуем стрелку
            float arrowSize = 30.0f;
            var rect = new RectangleF(edgePoint.X - arrowSize/2, edgePoint.Y - arrowSize/2, arrowSize, arrowSize);
            // Создаем RectangleF для угла, как требуется в API
            var angleRect = new RectangleF(angle, 0, 0, 0);
            Graphics.DrawImage(ArrowTexturePath, rect, angleRect, waypoint.Color);
            
            // Рисуем расстояние до точки
            Vector2 distancePos = edgePoint + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (arrowSize + 5);
            int distance = (int)Vector2.Distance(screenCenter, position);
            DrawCenteredTextWithBackground($"{waypoint.Name} - {distance}", distancePos, waypoint.Color, 
                Color.FromArgb(200, 0, 0, 0), true, 10, 5);
        }
        catch (Exception e) {
            LogError($"Error drawing waypoint arrow: {e.Message}\n{e.StackTrace}");
        }
    }

    private Vector2 CalculateScreenEdgePoint(Vector2 center, Vector2 direction, float width, float height)
    {
        // Нормализуем направление
        Vector2 normalizedDir = Vector2.Normalize(direction);
        
        // Находим расстояние до краев экрана
        float distanceToTop = (0 - center.Y) / normalizedDir.Y;
        float distanceToBottom = (height - center.Y) / normalizedDir.Y;
        float distanceToLeft = (0 - center.X) / normalizedDir.X;
        float distanceToRight = (width - center.X) / normalizedDir.X;
        
        // Выбираем кратчайшее положительное расстояние
        float distance = float.MaxValue;
        if (distanceToTop > 0 && distanceToTop < distance) distance = distanceToTop;
        if (distanceToBottom > 0 && distanceToBottom < distance) distance = distanceToBottom;
        if (distanceToLeft > 0 && distanceToLeft < distance) distance = distanceToLeft;
        if (distanceToRight > 0 && distanceToRight < distance) distance = distanceToRight;
        
        // Вычисляем точку на краю экрана
        return center + normalizedDir * distance;
    }

    private void AddWaypoint(Node node)
    {
        try {
            if (node == null) return;
            
            string waypointId = $"waypoint_{node.Coordinates.X}_{node.Coordinates.Y}";
            
            // Проверяем, существует ли уже эта путевая точка
            if (Settings.Waypoints.Waypoints.ContainsKey(waypointId)) {
                LogMessage($"Waypoint already exists: {node.Name}");
                return;
            }
            
            // Создаем новую путевую точку
            var waypoint = new Waypoint
            {
                ID = waypointId,
                Name = node.Name,
                Coordinates = node.Coordinates,
                Show = true,
                Line = true,
                Arrow = true,
                Scale = 1.0f,
                Color = Color.Yellow
            };
            
            // Добавляем путевую точку
            Settings.Waypoints.Waypoints.Add(waypointId, waypoint);
            LogMessage($"Added waypoint: {node.Name}");
        }
        catch (Exception e) {
            LogError($"Error adding waypoint: {e.Message}\n{e.StackTrace}");
        }
    }

    private void RemoveWaypoint(Node node)
    {
        try {
            if (node == null) return;
            
            string waypointId = $"waypoint_{node.Coordinates.X}_{node.Coordinates.Y}";
            
            // Проверяем, существует ли путевая точка
            if (!Settings.Waypoints.Waypoints.ContainsKey(waypointId)) {
                // Ищем путевую точку по координатам узла
                var matchingWaypoint = Settings.Waypoints.Waypoints
                    .FirstOrDefault(w => w.Value.Coordinates.X == node.Coordinates.X && 
                                       w.Value.Coordinates.Y == node.Coordinates.Y);
                
                if (matchingWaypoint.Value != null) {
                    waypointId = matchingWaypoint.Key;
                }
                else {
                    LogMessage($"No waypoint found for: {node.Name}");
                    return;
                }
            }
            
            // Удаляем путевую точку
            Settings.Waypoints.Waypoints.Remove(waypointId);
            LogMessage($"Removed waypoint: {node.Name}");
        }
        catch (Exception e) {
            LogError($"Error removing waypoint: {e.Message}\n{e.StackTrace}");
        }
    }

    private void DrawWaypointManagementTab()
    {
        ImGui.Text("Current waypoints:");
        ImGui.Spacing();
        
        // Таблица путевых точек
        if (ImGui.BeginTable("waypoints_table", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("X", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Y", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Show", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();
            
            // Отображаем все путевые точки
            foreach (var (id, waypoint) in Settings.Waypoints.Waypoints)
            {
                ImGui.TableNextRow();
                
                // Название
                ImGui.TableNextColumn();
                string name = waypoint.Name ?? "Unnamed";
                ImGui.InputText($"##name_{id}", ref name, 128);
                if (name != waypoint.Name) {
                    waypoint.Name = name;
                }
                
                // Координата X
                ImGui.TableNextColumn();
                int x = waypoint.Coordinates.X;
                if (ImGui.InputInt($"##x_{id}", ref x, 0, 0))
                {
                    waypoint.Coordinates = new Vector2i(x, waypoint.Coordinates.Y);
                }
                
                // Координата Y
                ImGui.TableNextColumn();
                int y = waypoint.Coordinates.Y;
                if (ImGui.InputInt($"##y_{id}", ref y, 0, 0))
                {
                    waypoint.Coordinates = new Vector2i(waypoint.Coordinates.X, y);
                }
                
                // Показать
                ImGui.TableNextColumn();
                bool show = waypoint.Show;
                if (ImGui.Checkbox($"##show_{id}", ref show))
                {
                    waypoint.Show = show;
                }
                
                // Цвет
                ImGui.TableNextColumn();
                Vector4 color = waypoint.Color.ToImguiVec4();
                if (ImGui.ColorEdit4($"##color_{id}", ref color, ImGuiColorEditFlags.NoInputs))
                {
                    waypoint.Color = Color.FromArgb(
                        (int)(color.W * 255),
                        (int)(color.X * 255),
                        (int)(color.Y * 255),
                        (int)(color.Z * 255)
                    );
                }
                
                // Действия
                ImGui.TableNextColumn();
                if (ImGui.Button($"Delete##remove_{id}"))
                {
                    // Помечаем для удаления
                    Settings.Waypoints.Waypoints.Remove(id);
                    break; // Прерываем цикл, т.к. коллекция изменилась
                }
            }
            
            ImGui.EndTable();
        }
        
        ImGui.Spacing();
        
        // Кнопки для управления всеми путевыми точками
        if (ImGui.Button("Delete All Waypoints"))
        {
            if (ImGui.BeginPopupModal("Delete Confirmation", ref WaypointPanelIsOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Are you sure you want to delete all waypoints?");
                ImGui.Spacing();
                
                if (ImGui.Button("Yes", new Vector2(120, 0)))
                {
                    Settings.Waypoints.Waypoints.Clear();
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("No", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.EndPopup();
            }
            
            ImGui.OpenPopup("Delete Confirmation");
        }
    }

    // Вспомогательный метод для парсинга HTML-цвета
    private Vector4 ParseHtmlColor(string htmlColor)
    {
        try {
            if (string.IsNullOrEmpty(htmlColor)) return new Vector4(1, 1, 1, 1);
            
            // Удаляем символ # в начале, если он есть
            if (htmlColor.StartsWith("#")) {
                htmlColor = htmlColor.Substring(1);
            }
            
            // Парсим цвет из шестнадцатеричной записи
            int r = Convert.ToInt32(htmlColor.Substring(0, 2), 16);
            int g = Convert.ToInt32(htmlColor.Substring(2, 2), 16);
            int b = Convert.ToInt32(htmlColor.Substring(4, 2), 16);
            
            return new Vector4(r / 255f, g / 255f, b / 255f, 1.0f);
        }
        catch {
            return new Vector4(1, 1, 1, 1); // Белый цвет по умолчанию
        }
    }

    // Для поиска
    private string GetMapNameFromDescription(AtlasNodeDescription nodeDesc)
    {
        try {
            if (nodeDesc == null) return string.Empty;
            
            // Пытаемся получить имя из объекта AtlasNodeDescription
            string name = nodeDesc.ToString();
            
            // Проверяем различные свойства, в которых может быть имя
            if (string.IsNullOrEmpty(name) || name.Contains("Unknown")) {
                // Пробуем получить имя из координат
                if (nodeDesc.Coordinate != null) {
                    name = $"Map #{nodeDesc.Coordinate.X}_{nodeDesc.Coordinate.Y}";
                }
                else {
                    name = "Unknown Map";
                }
            }
            
            return name;
        }
        catch (Exception e) {
            LogError($"Error getting map name: {e.Message}");
            return "Unknown Map";
        }
    }
}

public static class ColorHelper
{
    public static Vector4 GetColorForWeight(float normalizedValue)
    {
        return new Vector4(
            (1 - normalizedValue) * 0.8f + 0.2f,  // �� �������� � ��������
            normalizedValue * 0.8f + 0.2f,
            0.2f,
            1.0f
        );
    }
    
    public static Vector4 GetColorForDistance(float normalizedValue)
    {
        return new Vector4(
            (1 - normalizedValue) * 0.8f + 0.2f,  // �� �������� � ��������
            normalizedValue * 0.8f + 0.2f,
            0.2f,
            1.0f
        );
    }
}


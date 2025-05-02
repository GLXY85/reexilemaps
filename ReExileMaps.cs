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

using GameOffsets2.Native;

using ImGuiNET;

using RectangleF = ExileCore2.Shared.RectangleF;
using ReExileMaps.Classes;

namespace ReExileMaps;

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

    private const string defaultMapsPath = "json\\maps.json";
    private const string defaultModsPath = "json\\mods.json";
    private const string defaultBiomesPath = "json\\biomes.json";
    private const string defaultContentPath = "json\\content.json";
    private const string ArrowPath = "textures\\arrow.png";
    private const string IconsFile = "Icons.png";
    
    public IngameUIElements UI;
    public AtlasPanel AtlasPanel;

    private Vector2 screenCenter;
    private Vector2 playerAtlasPosition;
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
    private string pendingSearchQuery = "";
    
    // Для сортировки по расстоянию
    private Dictionary<string, float> cachedDistances = new Dictionary<string, float>();
    private List<MapSearchItem> mapItems = new List<MapSearchItem>();
    private string referencePositionText = "от центра экрана";

    // Кэширование результатов
    private Dictionary<Vector2i, Node> filteredMapCache = new Dictionary<Vector2i, Node>();
    private DateTime lastSearchUpdate = DateTime.MinValue;
    private DateTime lastWaypointUpdate = DateTime.MinValue;
    private bool needSearchUpdate = true;
    private bool needWaypointUpdate = true;
    private Dictionary<string, (float Distance, Vector4 Color)> distanceCache = new();
    private bool needsDistanceRecalculation = false;

    // Маршрутизация
    private string activeRouteWaypointKey = null;
    private List<Node> currentRoute = new List<Node>();
    private DateTime lastRouteUpdate = DateTime.MinValue;
    private bool routeNeedsUpdate = true;
    private Vector2i lastPlayerAtlasNode = default;

    // Состояние поиска
    private volatile bool isSearchInProgress = false;
    private volatile float searchProgress = 0f;
    private volatile string searchStatusText = "";

    // Состояние игрока
    private bool playerPositionDetermined = false;
    private string lastAreaName = string.Empty;

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
                DumpAtlasElements();
            }

            AtlasHasBeenClosed = false;

            cacheTicks++;
            if (cacheTicks % 30 == 0) {
                if (AtlasPanel.Descriptions != null && AtlasPanel.Descriptions.Count > mapCache.Count)
                    refreshCache = true;
                cacheTicks = 0;
            }
            
            if (GameController?.Window?.GetWindowRectangle() == null) return;
            screenCenter = GameController.Window.GetWindowRectangle().Center - GameController.Window.GetWindowRectangle().Location;
            
            UpdatePlayerAtlasPosition();
            
            if (Settings.Waypoints.RoutingSettings.EnableRoutes && 
                Settings.Waypoints.RoutingSettings.AutoUpdateRoute && 
                activeRouteWaypointKey != null && 
                routeNeedsUpdate)
            {
                UpdateActiveRoute();
            }

            // Update distance cache when player position changes
            if (lastPlayerAtlasNode != default)
            {
                needsDistanceRecalculation = true;
            }

            UpdateDistanceCache();
            
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
    }

    public override void Render()
    {
        try
        {
            CheckKeybinds();
            if (WaypointPanelIsOpen) DrawWaypointPanel();
            if (Settings.Search.PanelIsOpen) DrawSearchPanel();

            TickCount++;
            if (Settings.Graphics.RenderNTicks.Value % TickCount != 0) return;  

            TickCount = 0;

            if (!AtlasPanel.IsVisible) return;
            
            // Автоматически обновляем кэш, если он пуст
            if (mapCache.Count == 0) {
                var job = new Job($"{nameof(ReExileMapsCore)}InitialRefreshCache", () => {
                    RefreshMapCache();
                    refreshCache = false;
                });
                job.Start();
                return; // Пропускаем этот кадр, отрисуем в следующем
            }
            
            // Filter out nodes based on settings.
            List<Node> selectedNodes;
            lock (mapCacheLock) {
                selectedNodes = mapCache.Values.Where(x => Settings.Features.ProcessVisitedNodes || !x.IsVisited || x.IsAttempted)
                    .Where(x => (Settings.Features.ProcessHiddenNodes && !x.IsVisible) || x.IsVisible || x.IsTower)            
                    .Where(x => (Settings.Features.ProcessLockedNodes && !x.IsUnlocked) || x.IsUnlocked)
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
                        .Where(x => !Settings.Features.WaypointsUseAtlasRange || Vector2.Distance(playerAtlasPosition, x.MapNode.Element.GetClientRect().Center) <= (Settings.Features.AtlasRange ?? 2000)).AsParallel().ToList();
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

            // Отрисовка активного маршрута
            if (Settings.Waypoints.RoutingSettings.EnableRoutes && currentRoute.Count > 0) {
                DrawCurrentRoute();
            }
        }
        catch (Exception ex)
        {
            LogError($"Error in Render: {ex.Message}\n{ex.StackTrace}");
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
                // Запускаем обновление кэша в отдельном потоке, чтобы избежать зависания
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
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Map Boss");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Cleansed");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Corrupted");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Corrupted Nexus");
            ringCount += DrawContentRings(cachedNode, nodeCurrentPosition, ringCount, "Unique Map");
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
                
            // Получаем текущее положение курсора относительно игрового окна
            Vector2 cursorPos = Vector2.Zero;
            try {
                // Используем положение курсора мыши через элемент интерфейса
                if (GameController?.Game?.IngameState?.UIHoverElement != null)
                    cursorPos = GameController.Game.IngameState.UIHoverElement.Tooltip.GetClientRect().Center;
                else
                    cursorPos = screenCenter; // Запасной вариант - центр экрана
                
                // Логирование для отладки
                LogMessage($"Курсор находится в позиции: X={cursorPos.X}, Y={cursorPos.Y}");
            }
            catch (Exception ex) {
                LogError($"Ошибка при получении позиции курсора: {ex.Message}");
                cursorPos = screenCenter; // Запасной вариант - центр экрана
            }
            
            // Ищем ближайшую ноду к позиции курсора
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
                // Игнорируем ошибки в самом логировании
            }
            return null;
        }
    }

    private Node GetClosestNodeToCenterScreen() {
        if (AtlasPanel?.Descriptions == null || AtlasPanel.Descriptions.Count == 0)
            return null;

        Node closestNode = null;
        float minDistance = float.MaxValue;

        var center = screenCenter;
        
        foreach (var desc in AtlasPanel.Descriptions)
        {
            if (desc?.Element == null) continue;

            try
            {
                var nodePos = desc.Element.GetClientRect().Center;
                var distance = Vector2.Distance(center, nodePos);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    if (mapCache.TryGetValue(desc.Coordinate, out Node node))
                    {
                        closestNode = node;
                    }
                }
            }
            catch { continue; }
        }

        return closestNode;
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
        // --- Первый проход: добавляем все ноды ---
        foreach (var node in atlasNodes) {
            if (mapCache.TryGetValue(node.Coordinate, out Node cachedNode))
                count += RefreshCachedMapNode(node, cachedNode);
            else
                count += CacheNewMapNode(node);
        }
        // --- Второй проход: заполняем связи ---
        foreach (var node in mapCache.Values) {
            CacheMapConnections(node);
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
                AddNodeContentTypesFromTextures(node, newNode);

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
        AddNodeContentTypesFromTextures(node, cachedNode);

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
        // Очищаем связи перед заполнением, чтобы не было устаревших соседей
        cachedNode.Neighbors.Clear();
        
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
            
            // УБРАНО: фильтрация по DrawVisitedNodeConnections
            // if (!Settings.Features.DrawVisitedNodeConnections && (destinationNode.IsVisited || cachedNode.IsVisited))
            //     continue;

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
        // Используем позицию игрока вместо центра экрана
        var distance = Vector2.Distance(playerAtlasPosition, nodeCurrentPosition.Center);

        if (distance < 400)
            return;

        // Используем позицию игрока для рендеринга линии
        Vector2 position = Vector2.Lerp(playerAtlasPosition, nodeCurrentPosition.Center, Settings.Graphics.LabelInterpolationScale);
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
            // Отображаем расстояние от позиции игрока
            DrawCenteredTextWithBackground($"{cachedNode.Name} ({Vector2.Distance(playerAtlasPosition, nodeCurrentPosition.Center):0})", position, cachedNode.MapType.NameColor, Settings.Graphics.BackgroundColor, true, 10, 4);
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
        // Используем позицию игрока на атласе вместо центра экрана
        return Vector2.Distance(playerAtlasPosition, cachedNode.MapNode.Element.GetClientRect().Center);
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
        
        if (!ImGui.Begin("Управление пунктами назначения##waypoint_panel", ref window_open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.End();
            ImGui.PopStyleVar(3);
            WaypointPanelIsOpen = window_open;
            return;
        }
        
        WaypointPanelIsOpen = window_open;

        // Настройки
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.25f, 0.25f, 0.25f, 1.0f));
        
        if (ImGui.CollapsingHeader("Настройки отображения", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Уменьшаем количество элементов в таблице для более компактного отображения
            if (ImGui.BeginTable("waypoint_settings_table", 2, ImGuiTableFlags.None, new Vector2(0, 0)))
            {
                // Настройки вэйпоинтов на интерфейсе
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

        // Tab menu вверху панели
        if (ImGui.BeginTabBar("WaypointsTabBar", ImGuiTabBarFlags.None))
        {
            // Вкладка управления вэйпоинтами
            if (ImGui.BeginTabItem("Управление вэйпоинтами"))
            {
                DrawWaypointManagementTab();
                ImGui.EndTabItem();
            }
            
            // Вкладка поиска по карте атласа
            if (ImGui.BeginTabItem("Поиск карт"))
            {
                // Проверяем необходимость обновления данных (не чаще чем раз в 0.5 секунды)
                if (needWaypointUpdate || DateTime.Now.Subtract(lastWaypointUpdate).TotalSeconds > 0.5)
                {
                    DrawAtlasSearchTab();
                    lastWaypointUpdate = DateTime.Now;
                    needWaypointUpdate = false;
                }
                else
                {
                    // Используем кэшированные данные для рендеринга без выполнения тяжелых операций
                    DrawAtlasSearchTabCached();
                }
                ImGui.EndTabItem();
            }
            else
            {
                // Если вкладка не активна, помечаем что при следующем открытии нужно обновить данные
                needWaypointUpdate = true;
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.End();
        ImGui.PopStyleVar(3);
    }
    
    // Упрощенная версия DrawAtlasSearchTab, использующая кэшированные данные
    private void DrawAtlasSearchTabCached()
    {
        var panelSize = ImGui.GetContentRegionAvail();
        string regex = Settings.Waypoints.WaypointPanelFilter;

        // Выбор сортировки - рендерим UI элементы без выполнения тяжелых операций
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Сортировка: ");
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
        
        ImGui.Text("Макс. элементов: ");
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
        if (ImGui.Checkbox("Только разблокированные", ref unlockedOnly)) {
            Settings.Waypoints.ShowUnlockedOnly = unlockedOnly;
            needWaypointUpdate = true;
        }

        ImGui.Spacing();
        
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Поиск: ");
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
            ImGui.SetTooltip("Ищите по названию карты или модификатору. Нажмите Enter для поиска.");
        }

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        bool useRegex = Settings.Waypoints.WaypointsUseRegex;
        if (ImGui.Checkbox("Регулярное выражение", ref useRegex)) {
            Settings.Waypoints.WaypointsUseRegex = useRegex;
            searchChanged = true;
        }
        
        if (searchChanged) {
            needWaypointUpdate = true;
        }
        
        ImGui.Separator();
    
        // Отображаем кэшированную таблицу без выполнения тяжелых операций фильтрации и сортировки
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

            // Используем кэшированные данные
            RenderFilteredMapTable(filteredMapCache);
            
            ImGui.PopStyleVar(2);
            ImGui.EndTable();
        }
    }
    
    // Отображение таблицы карт без повторной фильтрации и сортировки
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

        // Выбор сортировки
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Сортировка: ");
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
        
        ImGui.Text("Макс. элементов: ");
        ImGui.SameLine();
        int maxItems = Settings.Waypoints.WaypointPanelMaxItems;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("##maxItems", ref maxItems))
            Settings.Waypoints.WaypointPanelMaxItems = maxItems; 

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        bool unlockedOnly = Settings.Waypoints.ShowUnlockedOnly;
        if (ImGui.Checkbox("Только разблокированные", ref unlockedOnly))
            Settings.Waypoints.ShowUnlockedOnly = unlockedOnly;

        ImGui.Spacing();
        
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Поиск: ");
        ImGui.SameLine();
        
        ImGui.SetNextItemWidth(panelSize.X - 120);
        if (ImGui.InputText("##search", ref regex, 32, ImGuiInputTextFlags.EnterReturnsTrue)) {
            Settings.Waypoints.WaypointPanelFilter = regex;
        } else if (ImGui.IsItemDeactivatedAfterEdit()) {
            Settings.Waypoints.WaypointPanelFilter = regex;
        } else if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Ищите по названию карты или модификатору. Нажмите Enter для поиска.");
        }

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        bool useRegex = Settings.Waypoints.WaypointsUseRegex;
        if (ImGui.Checkbox("Регулярное выражение", ref useRegex))
            Settings.Waypoints.WaypointsUseRegex = useRegex;
        
        ImGui.Separator();
    
        // Заменяем AsParallel().ToDictionary на обычный где возможно
        var tempCache = mapCache.Where(x => !x.Value.IsVisited && (!Settings.Waypoints.ShowUnlockedOnly || x.Value.IsUnlocked)).ToDictionary(x => x.Key, x => x.Value);    
        // if search isnt blank
        if (!string.IsNullOrEmpty(Settings.Waypoints.WaypointPanelFilter)) {
            if (useRegex) {
                tempCache = tempCache.Where(x => Regex.IsMatch(x.Value.Name, Settings.Waypoints.WaypointPanelFilter, RegexOptions.IgnoreCase) || x.Value.MatchEffect(Settings.Waypoints.WaypointPanelFilter) || x.Value.Content.Any(x => x.Value.Name == Settings.Waypoints.WaypointPanelFilter)).ToDictionary(x => x.Key, x => x.Value);
            } else {
                tempCache = tempCache.Where(x => x.Value.Name.Contains(Settings.Waypoints.WaypointPanelFilter, StringComparison.CurrentCultureIgnoreCase) || x.Value.MatchEffect(Settings.Waypoints.WaypointPanelFilter) || x.Value.Content.Any(x => x.Value.Name == Settings.Waypoints.WaypointPanelFilter)).ToDictionary(x => x.Key, x => x.Value);
            }
        }
        
        // Применяем сортировку по выбранному методу
        switch (sortBy)
        {
            case "Distance":
                try 
                {
                    // Сортировка по расстоянию от центра экрана (ближние карты первыми)
                    tempCache = tempCache.OrderBy(x => {
                        try {
                            return Vector2.Distance(screenCenter, x.Value.MapNode.Element.GetClientRect().Center);
                        }
                        catch {
                            return float.MaxValue;
                        }
                    }).ToDictionary(x => x.Key, x => x.Value);
                }
                catch 
                {
                    // При ошибке сортируем по весу
                    tempCache = tempCache.OrderByDescending(x => x.Value.Weight).ToDictionary(x => x.Key, x => x.Value);
                }
                break;
                
            case "Name":
                tempCache = tempCache.OrderBy(x => x.Value.Name).ToDictionary(x => x.Key, x => x.Value);
                break;
                
            case "Weight":
            default:
                tempCache = tempCache.OrderByDescending(x => x.Value.Weight).ToDictionary(x => x.Key, x => x.Value);
                break;
        }
        
        // Ограничиваем количество элементов
        tempCache = tempCache.Take(Settings.Waypoints.WaypointPanelMaxItems).ToDictionary(x => x.Key, x => x.Value);

        // Сохраняем отфильтрованный и отсортированный кэш для последующего использования
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

            // Передаем отрисовку таблицы в отдельный метод
            RenderFilteredMapTable(tempCache);
            
            ImGui.PopStyleVar(2);
            ImGui.EndTable();
        }
    }
    
    // Оптимизируем метод обновления результатов поиска
    private void UpdateSearchResults() {
        try {
            isSearchInProgress = true;
            searchProgress = 0f;
            searchStatusText = "Поиск...";
            
            if (!needSearchUpdate && DateTime.Now.Subtract(lastSearchUpdate).TotalSeconds <= 0.5 && 
                Settings.Search.SearchQuery == previousSearchQuery) {
                isSearchInProgress = false;
                return;
            }
            
            lastSearchUpdate = DateTime.Now;
            needSearchUpdate = false;
            previousSearchQuery = Settings.Search.SearchQuery;
            
            if (!AtlasPanel?.IsVisible == true) {
                searchResults.Clear();
                mapItems.Clear();
                isSearchInProgress = false;
                return;
            }
            
            if (string.IsNullOrWhiteSpace(Settings.Search.SearchQuery)) {
                searchResults.Clear();
                mapItems.Clear();
                isSearchInProgress = false;
                return;
            }

            LogMessage($"Начинаем поиск: '{Settings.Search.SearchQuery}'");
            var stopwatch = Stopwatch.StartNew();

            string searchQuery = Settings.Search.SearchQuery.ToLower();
            string[] searchTerms = searchQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            searchResults.Clear();
            mapItems.Clear();
            previousSearchQuery = searchQuery;

            var filteredNodes = new List<Node>();
            int total = 0;
            int processed = 0;
            
            lock (mapCacheLock) {
                total = mapCache.Count;
                filteredNodes = new List<Node>(total);
                
                foreach (var node in mapCache.Values) {
                    processed++;
                    if (node == null || node.MapNode == null || node.MapNode.Element == null) continue;
                    if (node.IsVisited && node.IsUnlocked) continue;

                    bool matchesAllTerms = true;
                    
                    foreach (var term in searchTerms) {
                        bool termMatches = false;
                        
                        // Проверяем все возможные свойства на совпадение
                        termMatches |= node.Name.ToLower().Contains(term);
                        termMatches |= node.MapType?.Name?.ToLower()?.Contains(term) == true;
                        
                        // Поиск по эффектам
                        termMatches |= node.Effects.Any(e => e.Value.Name.ToLower().Contains(term) || 
                                                           e.Value.Description.ToLower().Contains(term));
                        
                        // Поиск по контенту
                        termMatches |= node.Content.Any(c => c.Value.Name.ToLower().Contains(term));
                        
                        // Поиск по биомам
                        termMatches |= node.Biomes.Any(b => b.Value.Name.ToLower().Contains(term));
                        
                        // Поиск по статусу
                        switch (term) {
                            case "visited":
                                termMatches |= node.IsVisited;
                                break;
                            case "unlocked":
                                termMatches |= node.IsUnlocked && !node.IsVisited;
                                break;
                            case "locked":
                                termMatches |= !node.IsUnlocked && node.IsVisible;
                                break;
                            case "hidden":
                                termMatches |= !node.IsVisible;
                                break;
                            case "tower":
                                termMatches |= node.IsTower;
                                break;
                            case "failed":
                            case "attempted":
                                termMatches |= node.IsAttempted;
                                break;
                        }

                        // Если хотя бы один термин не найден, прекращаем поиск
                        if (!termMatches) {
                            matchesAllTerms = false;
                            break;
                        }
                    }

                    if (matchesAllTerms)
                        filteredNodes.Add(node);

                    if (processed % 100 == 0 || processed == total) {
                        searchProgress = total > 0 ? (float)processed / total : 1f;
                        searchStatusText = $"Обработано {processed} из {total}";
                    }
                }
            }
            
            searchProgress = 1f;
            searchStatusText = $"Готово! Найдено: {filteredNodes.Count}";

            // Применяем сортировку
            switch (Settings.Search.SortBy) {
                case "Distance":
                    if (!Settings.Waypoints.ShowWaypoints) {
                        searchResults = filteredNodes.OrderByDescending(n => n.Weight).ToList();
                        break;
                    }
                    try {
                        referencePositionText = "от позиции игрока";
                        Vector2 referencePos = playerAtlasPosition;
                        foreach (var node in filteredNodes) {
                            if (node == null || node.MapNode?.Element == null) continue;
                            float distance = float.MaxValue;
                            try {
                                distance = Vector2.Distance(referencePos, node.MapNode.Element.GetClientRect().Center);
                            } catch {}
                            mapItems.Add(new MapSearchItem(node, distance));
                        }
                        searchResults = mapItems.OrderBy(item => item.Distance).Select(item => item.MapNode).ToList();
                    } catch (Exception ex) {
                        LogError($"Error during distance sorting: {ex.Message}");
                        searchResults = filteredNodes.OrderByDescending(n => n.Weight).ToList();
                    }
                    break;
                case "Name":
                    searchResults = filteredNodes.OrderBy(n => n.Name).ToList();
                    break;
                case "Status":
                    searchResults = filteredNodes.OrderBy(n => n.IsVisited)
                                           .ThenBy(n => !n.IsUnlocked)
                                           .ThenBy(n => !n.IsVisible)
                                           .ThenByDescending(n => n.Weight)
                                           .ToList();
                    break;
                case "Weight":
                default:
                    searchResults = filteredNodes.OrderByDescending(n => n.Weight).ToList();
                    break;
            }
            
            stopwatch.Stop();
            LogMessage($"Поиск завершен: найдено {searchResults.Count} элементов за {stopwatch.ElapsedMilliseconds}мс");
            isSearchInProgress = false;
        }
        catch (Exception ex) {
            LogError($"Error in UpdateSearchResults: {ex.Message}");
            isSearchInProgress = false;
        }
    }

    private void DrawSearchPanel() {
        if (!Settings.Search.PanelIsOpen || !AtlasPanel?.IsVisible == true) return;

        // Разрешаем изменение размера окна и скроллинг
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

            // --- ПРОГРЕСС-БАР ПОИСКА ---
            if (isSearchInProgress) {
                ImGui.ProgressBar(searchProgress, new Vector2(-1, 0), searchStatusText);
                ImGui.Spacing();
            }
            
            // Header section with better organized search controls
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.17f, 0.17f, 0.2f, 0.8f));
            ImGui.BeginChild("SearchHeader", new Vector2(-1, 80), ImGuiChildFlags.None);
            
            // Search input row with better alignment
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Search maps:"); 
            ImGui.SameLine();
            
            // Используем pendingSearchQuery для поля ввода
            string searchQuery = pendingSearchQuery;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 80);
            
            // Поиск только по Enter
            if (ImGui.InputText("###SearchQuery", ref searchQuery, 100, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                pendingSearchQuery = searchQuery;
                ExecuteSearch();
            }
            else if (searchQuery != pendingSearchQuery)
            {
                pendingSearchQuery = searchQuery;
            }

            ImGui.SameLine();
            
            // Поиск по кнопке Search
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.7f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.8f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.4f, 0.6f, 1.0f));
            if (ImGui.Button("Search", new Vector2(75, 0)) && !isSearchInProgress)
            {
                ExecuteSearch();
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
            
            // Отображаем текущую точку отсчета для измерения расстояния, если выбрана сортировка по расстоянию
            if (Settings.Search.SortBy == "Distance") {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 0.5f, 1.0f), $"({referencePositionText})");
                if (ImGui.IsItemHovered()) {
                    ImGui.BeginTooltip();
                    ImGui.Text("Расстояние измеряется от позиции игрока на карте атласа.");
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
            
            // Отображаем информацию об активном маршруте, если он есть
            if (Settings.Waypoints.RoutingSettings.EnableRoutes && activeRouteWaypointKey != null) {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.3f, 0.2f, 0.7f));
                ImGui.BeginChild("SearchRouteInfo", new Vector2(-1, 40), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);
                
                if (Settings.Waypoints.Waypoints.TryGetValue(activeRouteWaypointKey, out Waypoint waypoint)) {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 1.0f, 0.3f, 1.0f));
                    ImGui.Text($"Активный маршрут: {waypoint.Name}");
                    ImGui.PopStyleColor();
                    
                    ImGui.SameLine(ImGui.GetWindowWidth() - 160);
                    if (ImGui.Button("Обновить##SearchRoute", new Vector2(70, 0))) {
                        routeNeedsUpdate = true;
                        UpdateActiveRoute();
                    }
                    
                    ImGui.SameLine();
                    if (ImGui.Button("Очистить##SearchRoute", new Vector2(70, 0))) {
                        ClearActiveRoute();
                    }
                    
                    ImGui.Text($"Шаги: {currentRoute.Count - 1} | Длина пути: {CalculateRouteDistance():F0}");
                }
                
                ImGui.EndChild();
                ImGui.PopStyleColor();
            }
            
            // Main layout with results
            if (!isSearchInProgress) {
                if (ImGui.BeginTable("search_results_table", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, ImGui.GetContentRegionAvail().Y - 40))) {
                    ImGui.TableSetupColumn("Map Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthFixed, 90);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 90);
                    ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 120);
                    
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
                        
                        // Map Name
                        ImGui.TableNextColumn();
                        ImGui.TextColored(
                            node.IsUnlocked 
                                ? new Vector4(0.9f, 0.9f, 1.0f, 1.0f) 
                                : new Vector4(0.6f, 0.6f, 0.7f, 1.0f), 
                            node.Name);
                        
                        // Distance - use cached values
                        ImGui.TableNextColumn();
                        if (distanceCache.TryGetValue(node.Name, out var distanceInfo))
                        {
                            if (distanceInfo.Distance < float.MaxValue)
                            {
                                ImGui.TextColored(distanceInfo.Color, $"{distanceInfo.Distance:0}");
                            }
                            else
                            {
                                ImGui.Text("-");
                            }
                        }
                        else
                        {
                            ImGui.Text("-");
                        }
                        
                        // Type/Content
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
                        
                        // Weight
                        ImGui.TableNextColumn();
                        float normalizedWeight = (node.Weight - minMapWeight) / (maxMapWeight - minMapWeight);
                        ImGui.TextColored(
                            ColorHelper.GetColorForWeight(normalizedWeight),
                            $"{node.Weight:0.#}");
                        
                        // Actions
                        ImGui.TableNextColumn();
                        string buttonId = $"waypoint_{node.Coordinates}";
                        bool hasWaypoint = Settings.Waypoints.Waypoints.ContainsKey(node.Coordinates.ToString());
                        
                        // Кнопка для навигации к карте (создания маршрута)
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1.0f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.6f, 0.9f, 1.0f));
                        
                        string routeText = activeRouteWaypointKey == node.Coordinates.ToString() ? "Cancel" : "Route";
                        if (ImGui.Button($"{routeText}##{buttonId}_route", new Vector2(48, 0))) {
                            if (activeRouteWaypointKey == node.Coordinates.ToString()) {
                                ClearActiveRoute();
                            } else {
                                if (!hasWaypoint) {
                                    AddWaypoint(node);
                                    hasWaypoint = true;
                                }
                                SetActiveRoute(node.Coordinates.ToString());
                            }
                        }
                        ImGui.PopStyleColor(2);
                        
                        ImGui.SameLine();
                        
                        if (hasWaypoint) {
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1.0f));
                            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1.0f));
                            if (ImGui.Button($"Remove##{buttonId}", new Vector2(48, 0))) {
                                if (activeRouteWaypointKey == node.Coordinates.ToString()) {
                                    ClearActiveRoute();
                                }
                                RemoveWaypoint(node);
                            }
                            ImGui.PopStyleColor(2);
                        } else {
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1.0f));
                            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.3f, 1.0f));
                            if (ImGui.Button($"Add##{buttonId}", new Vector2(48, 0))) {
                                AddWaypoint(node);
                            }
                            ImGui.PopStyleColor(2);
                        }
                    }
                    
                    ImGui.PopStyleColor(2); // Pop the TableRow colors
                    ImGui.EndTable();
                }
            }
            
            ImGui.End();
        }
        ImGui.PopStyleVar(3);
    }
    #endregion

    private void SortDistanceColumn() {
        try {
            var referencePosText = "положение игрока";
            Vector2 referencePos = playerAtlasPosition;
            
            // Изменяем текст для новой точки отсчета - позиции игрока на атласе
            referencePositionText = "от позиции игрока";
            
            // Используем для расчетов положение игрока на атласе
            lock (mapCacheLock) {
                foreach (var node in AtlasPanel.Descriptions) {
                    if (node == null || node.Element == null)
                        continue;
                        
                    string mapName = GetMapNameFromDescription(node);
                    if (string.IsNullOrEmpty(mapName))
                        continue;
                    
                float distance = float.MaxValue; // Значение по умолчанию
                
                try {
                        // Используем позицию игрока для расчета расстояния
                        distance = Vector2.Distance(referencePos, node.Element.GetClientRect().Center);
                    } catch {
                        // Игнорируем ошибки
                    }
                    
                    var mapItem = mapItems.FirstOrDefault(x => x.Name == mapName);
                    if (mapItem != null) {
                cachedDistances[mapItem.Name] = distance;
                    }
            }
            
                // Сортируем по расстоянию
                searchResults = searchResults
                .OrderBy(x => cachedDistances.ContainsKey(x.Name) ? cachedDistances[x.Name] : float.MaxValue)
                .ToList();
            }
                
            // Обновляем текст для пользовательского интерфейса
            LogMessage($"Sorting by distance from player position");
        }
        catch (Exception ex) {
                LogError($"Error in SortDistanceColumn: {ex.Message}");
        }
    }

    /// <summary>
    /// Извлекает имя карты из элемента AtlasNodeDescription
    /// </summary>
    /// <param name="description">Элемент описания карты</param>
    /// <returns>Имя карты</returns>
    private string GetMapNameFromDescription(AtlasNodeDescription description)
    {
        if (description == null || description.Element == null)
            return string.Empty;
            
        // Используем Element.Area.Name вместо Text
        return description.Element.Area.Name;
    }

    // Возвращаем удаленные методы
    private void DrawWaypointManagementTab()
    {
        var panelSize = ImGui.GetContentRegionAvail();
        
        // Отображаем информацию о текущем активном маршруте
        if (Settings.Waypoints.RoutingSettings.EnableRoutes && activeRouteWaypointKey != null)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.3f, 0.2f, 0.7f));
            ImGui.BeginChild("ActiveRouteInfo", new Vector2(-1, 50), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);
            
            if (Settings.Waypoints.Waypoints.TryGetValue(activeRouteWaypointKey, out Waypoint waypoint))
            {
                ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[1]); // Используем больший шрифт
                ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), $"Активный маршрут: {waypoint.Name}");
                ImGui.PopFont();
                
                ImGui.SameLine(ImGui.GetWindowWidth() - 120);
                if (ImGui.Button("Обновить##route", new Vector2(60, 20)))
                {
                    routeNeedsUpdate = true;
                    UpdateActiveRoute();
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Очистить##route", new Vector2(60, 20)))
                {
                    ClearActiveRoute();
                }
                
                ImGui.Text($"Шаги: {currentRoute.Count - 1} | Длина пути: {CalculateRouteDistance():F0}");
            }
            else
            {
                ImGui.Text("Активный маршрут: цель не найдена");
                
                ImGui.SameLine(ImGui.GetWindowWidth() - 60);
                if (ImGui.Button("Очистить##route", new Vector2(60, 20)))
                {
                    ClearActiveRoute();
                }
            }
            
            ImGui.EndChild();
            ImGui.PopStyleColor();
        ImGui.Separator();
        }
        
        // Отображаем список всех вэйпоинтов
        if (ImGui.BeginTable("waypoints_table", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY, new Vector2(0, panelSize.Y - 80)))
        {
            ImGui.TableSetupColumn("Имя", ImGuiTableColumnFlags.WidthStretch, 0.3f);
            ImGui.TableSetupColumn("Координаты", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Расстояние", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Шаги", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Видимость", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Действия", ImGuiTableColumnFlags.WidthFixed, 140);
            
            ImGui.TableHeadersRow();
            
            ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(0.19f, 0.19f, 0.22f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(0.22f, 0.22f, 0.25f, 0.9f));

            int index = 0;
            foreach (var (key, waypoint) in Settings.Waypoints.Waypoints)
            {
                ImGui.PushID(index++);
                
                ImGui.TableNextRow();
                
                // Имя
                ImGui.TableNextColumn();
                
                // Подсвечиваем активный маршрут
                bool isActiveRoute = key == activeRouteWaypointKey;
                if (isActiveRoute)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 1.0f, 0.3f, 1.0f));
                }
                
                ImGui.Text(waypoint.Name);
                
                if (isActiveRoute)
                {
                    ImGui.PopStyleColor();
                }
                
                // Координаты
                ImGui.TableNextColumn();
                ImGui.Text(waypoint.CoordinatesString);
                
                // Расстояние от игрока
                ImGui.TableNextColumn();
                float distance = float.MaxValue;
                int steps = -1;
                
                // Получаем ноду, соответствующую вэйпоинту
                Node waypointNode = null;
                if (mapCache.TryGetValue(waypoint.Coordinates, out Node node))
                {
                    waypointNode = node;
            }
            else
            {
                    var matchingNode = mapCache.Values.FirstOrDefault(n => 
                        n.Coordinates.Equals(waypoint.Coordinates) || 
                        n.Name.Equals(waypoint.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingNode != null)
                    {
                        waypointNode = matchingNode;
                    }
                }
                
                if (waypointNode != null && waypointNode.MapNode?.Element != null)
                {
                    distance = Vector2.Distance(playerAtlasPosition, waypointNode.MapNode.Element.GetClientRect().Center);
                    
                    // Вычисляем количество шагов (узлов) до вэйпоинта
                    Node startNode = FindClosestAccessibleNode();
                    if (startNode != null)
                    {
                        var tempRoute = FindShortestPath(startNode, waypointNode);
                        steps = tempRoute.Count > 0 ? tempRoute.Count - 1 : -1;
                    }
                }
                
                // Отображаем расстояние с цветовой индикацией
                if (distance < float.MaxValue)
                {
                    // Нормализуем расстояние для цветов (максимум 100 для карты атласа)
                    float normalizedDistance = Math.Min(distance / 100f, 1.0f);
                    Vector4 distanceColor = ColorHelper.GetColorForDistance(1.0f - normalizedDistance);
                    ImGui.TextColored(distanceColor, $"{distance:0}");
                }
                else
                {
                    ImGui.Text("-");
                }
                
                // Шаги
                ImGui.TableNextColumn();
                if (steps >= 0)
                {
                    ImGui.Text($"{steps}");
                }
                else
                {
                    ImGui.Text("-");
                }
                
                // Видимость
                ImGui.TableNextColumn();
                bool show = waypoint.Show;
                if (ImGui.Checkbox("##Show", ref show))
                {
                    waypoint.Show = show;
                }
                
                // Действия
                ImGui.TableNextColumn();
                
                // Кнопка маршрута
                if (isActiveRoute)
                {
                    if (ImGui.Button("Сбросить##route"))
                    {
                        ClearActiveRoute();
                    }
                }
                else
                {
                    if (ImGui.Button("Маршрут##route"))
                    {
                        SetActiveRoute(key);
                    }
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Удалить##waypoint"))
                {
                    // Если удаляем активный маршрут, очищаем его
                    if (key == activeRouteWaypointKey)
                    {
                        ClearActiveRoute();
                    }
                    RemoveWaypoint(waypoint);
                }
                
                ImGui.PopID();
            }
            
            ImGui.PopStyleColor(2);
            ImGui.EndTable();
        }
    }

    // Расчет общей длины маршрута
    private float CalculateRouteDistance()
    {
        try
        {
            if (currentRoute.Count < 2)
            {
                return 0;
            }
            
            float totalDistance = 0;
            
            for (int i = 0; i < currentRoute.Count - 1; i++)
            {
                var fromNode = currentRoute[i];
                var toNode = currentRoute[i + 1];
                
                if (fromNode.MapNode?.Element == null || toNode.MapNode?.Element == null)
                {
                    continue;
                }
                
                float distance = Vector2.Distance(
                    fromNode.MapNode.Element.GetClientRect().Center,
                    toNode.MapNode.Element.GetClientRect().Center
                );
                
                totalDistance += distance;
            }
            
            return totalDistance;
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при расчете длины маршрута: {ex.Message}\n{ex.StackTrace}");
            return 0;
        }
    }
    
    // Находит ближайшую доступную ноду к позиции игрока
    private Node FindClosestAccessibleNode()
    {
        Node closestNode = null;
        float minDistance = float.MaxValue;
        
        lock (mapCacheLock)
        {
            foreach (var node in mapCache.Values)
            {
                if (node.IsUnlocked && node.MapNode?.Element != null)
                {
                    float distance = Vector2.Distance(playerAtlasPosition, node.MapNode.Element.GetClientRect().Center);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestNode = node;
                    }
                }
            }
        }
        
        return closestNode;
    }
    
    // Алгоритм поиска кратчайшего пути (A*)
    private List<Node> FindShortestPath(Node start, Node target)
    {
        // Для оптимизации: если путь к одной и той же ноде, возвращаем пустой список
        if (start == target)
        {
            return new List<Node> { start };
        }
        
        var openSet = new PriorityQueue<Node, float>();
        var closedSet = new HashSet<Node>();
        var cameFrom = new Dictionary<Node, Node>();
        var gScore = new Dictionary<Node, float>();
        var fScore = new Dictionary<Node, float>();
        
        // Инициализация начальной ноды
        gScore[start] = 0;
        fScore[start] = HeuristicCost(start, target);
        openSet.Enqueue(start, fScore[start]);
        
        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            
            if (current == target)
            {
                return ReconstructPath(cameFrom, current);
            }
            
            closedSet.Add(current);
            
            // Проверяем соседей
            foreach (var neighbor in current.Neighbors.Values)
            {
                if (closedSet.Contains(neighbor) || neighbor.MapNode?.Element == null)
                {
                    continue;
                }
                // --- УБРАНО: фильтрация по IsVisible (hidden-ноды теперь участвуют в маршруте) ---
                // if (!neighbor.IsVisible) continue;
                // ---
                // Пропускаем ноды, которые не разблокированы (если опция выключена)
                if (!neighbor.IsUnlocked && !Settings.Waypoints.RoutingSettings.UseLockedNodesInRoute)
                {
                    continue;
                }
                float tentativeGScore = gScore.GetValueOrDefault(current, float.MaxValue) + 1;
                
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + HeuristicCost(neighbor, target);
                    
                    if (!openSet.UnorderedItems.Any(x => x.Element == neighbor))
                    {
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }
        }
        
        // Путь не найден
        return new List<Node>();
    }
    
    // Эвристическая функция для A* (евклидово расстояние)
    private float HeuristicCost(Node a, Node b)
    {
        if (a.MapNode?.Element == null || b.MapNode?.Element == null)
        {
            return float.MaxValue;
        }
        
        return Vector2.Distance(
            a.MapNode.Element.GetClientRect().Center,
            b.MapNode.Element.GetClientRect().Center
        );
    }
    
    // Восстановление пути из cameFrom
    private List<Node> ReconstructPath(Dictionary<Node, Node> cameFrom, Node current)
    {
        var path = new List<Node> { current };
        
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        
        return path;
    }
    
    // Отрисовка текущего маршрута
    private void DrawCurrentRoute()
    {
        if (currentRoute.Count < 2)
        {
            return;
        }
        // Находим стартовую ноду (позиция игрока)
        Node startNode = FindClosestAccessibleNode();
        if (startNode == null || currentRoute[0] != startNode)
        {
            // Если маршрут не начинается с позиции игрока, ищем ближайшую ноду в маршруте к игроку
            int closestIdx = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < currentRoute.Count; i++)
            {
                var node = currentRoute[i];
                if (node.MapNode?.Element == null) continue;
                float dist = Vector2.Distance(playerAtlasPosition, node.MapNode.Element.GetClientRect().Center);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIdx = i;
                }
            }
            // Обрезаем маршрут так, чтобы он начинался с ближайшей к игроку ноды
            if (closestIdx > 0)
                currentRoute = currentRoute.Skip(closestIdx).ToList();
        }
        for (int i = 0; i < currentRoute.Count - 1; i++)
        {
            var fromNode = currentRoute[i];
            var toNode = currentRoute[i + 1];
            if (fromNode.MapNode?.Element == null || toNode.MapNode?.Element == null)
            {
                continue;
            }
            var startPos = fromNode.MapNode.Element.GetClientRect().Center;
            var endPos = toNode.MapNode.Element.GetClientRect().Center;
            Color lineColor;
            if (Settings.Waypoints.RoutingSettings.UseGradient)
            {
                float progress = (float)i / (currentRoute.Count - 1);
                lineColor = ColorUtils.InterpolateColor(
                    Settings.Waypoints.RoutingSettings.RouteStartColor,
                    Settings.Waypoints.RoutingSettings.RouteEndColor,
                    progress
                );
            }
            else
            {
                lineColor = Settings.Waypoints.RoutingSettings.RouteLineColor;
            }
            float lineWidth = Settings.Waypoints.RoutingSettings.RouteLineWidth;
            Graphics.DrawLine(startPos, endPos, lineWidth, lineColor);
            if (Settings.Waypoints.RoutingSettings.ShowDirectionArrows)
            {
                Vector2 midPoint = Vector2.Lerp(startPos, endPos, 0.5f);
                Vector2 direction = Vector2.Normalize(endPos - startPos);
                DrawDirectionArrow(
                    midPoint, 
                    direction, 
                    Settings.Waypoints.RoutingSettings.ArrowSize,
                    lineColor
                );
            }
        }
        
        // Отображаем информацию о маршруте
        if (currentRoute.Count > 0 && Settings.Waypoints.Waypoints.TryGetValue(activeRouteWaypointKey, out Waypoint waypoint))
        {
            var routeStartNode = currentRoute.FirstOrDefault();
            var targetNode = currentRoute.LastOrDefault();
            
            if (routeStartNode != null && targetNode != null && routeStartNode.MapNode?.Element != null)
            {
                var infoPos = routeStartNode.MapNode.Element.GetClientRect().Center;
                infoPos.Y -= 30; // Смещаем текст выше ноды
                
                string routeInfo = $"Маршрут до {waypoint.Name}: {currentRoute.Count - 1} шагов";
                DrawCenteredTextWithBackground(
                    routeInfo,
                    infoPos,
                    Color.FromArgb(255, 255, 255, 0),
                    Color.FromArgb(180, 0, 0, 0),
                    true,
                    10,
                    4
                );
            }
        }
    }
    
    // Рисует стрелку направления для маршрута
    private void DrawDirectionArrow(Vector2 position, Vector2 direction, float size, Color color)
    {
        // Нормализуем направление
        direction = Vector2.Normalize(direction);
        
        // Вычисляем перпендикулярный вектор
        Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
        
        // Вычисляем точки стрелки
        Vector2 tip = position + direction * size * 0.5f;
        Vector2 baseLeft = position - direction * size * 0.3f + perpendicular * size * 0.3f;
        Vector2 baseRight = position - direction * size * 0.3f - perpendicular * size * 0.3f;
        
        // Рисуем треугольник стрелки
        Graphics.DrawLine(baseLeft, tip, 2f, color);
        Graphics.DrawLine(baseRight, tip, 2f, color);
        Graphics.DrawLine(baseLeft, baseRight, 2f, color);
    }
    
    #region Routing
    // Обновление активного маршрута
    private void UpdateActiveRoute()
    {
        try
        {
            if (activeRouteWaypointKey == null || !Settings.Waypoints.Waypoints.TryGetValue(activeRouteWaypointKey, out Waypoint targetWaypoint))
            {
            return;
            }
            
            // Находим ноду, соответствующую вэйпоинту
            Node targetNode = null;
            if (mapCache.TryGetValue(targetWaypoint.Coordinates, out Node node))
            {
                targetNode = node;
            }
            else
            {
                var matchingNode = mapCache.Values.FirstOrDefault(n => 
                    n.Coordinates.Equals(targetWaypoint.Coordinates) || 
                    n.Name.Equals(targetWaypoint.Name, StringComparison.OrdinalIgnoreCase));
                
                if (matchingNode != null)
                {
                    targetNode = matchingNode;
                }
            }
            
            if (targetNode == null || targetNode.MapNode?.Element == null)
            {
                LogMessage($"Не удалось найти целевую ноду для маршрута до {targetWaypoint.Name}");
            return;
            }
            
            // Находим ближайшую доступную ноду к игроку
            Node startNode = FindClosestAccessibleNode();
            if (startNode == null)
            {
                LogMessage("Не удалось найти начальную ноду для построения маршрута");
                return;
            }
            
            // Ищем кратчайший путь от начальной до целевой ноды
            var route = FindShortestPath(startNode, targetNode);
            
            // Обновляем текущий маршрут
            currentRoute = route;
            lastRouteUpdate = DateTime.Now;
            routeNeedsUpdate = false;
            
            LogMessage($"Маршрут до {targetWaypoint.Name} обновлен: {(route.Count > 0 ? route.Count.ToString() : "путь не найден")}");
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при обновлении маршрута: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    // Установка активного маршрута
    private void SetActiveRoute(string waypointKey)
    {
        if (waypointKey == null || !Settings.Waypoints.Waypoints.ContainsKey(waypointKey))
        {
            return;
        }
        
        activeRouteWaypointKey = waypointKey;
        routeNeedsUpdate = true;
        UpdateActiveRoute();
    }
    
    // Очистка активного маршрута
    private void ClearActiveRoute()
    {
        activeRouteWaypointKey = null;
        currentRoute.Clear();
    }
    #endregion

    // =====================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ ВЭЙПОИНТОВ И ПОЗИЦИИ ИГРОКА
    // =====================

    // Метод для определения позиции игрока на атласе
    private void UpdatePlayerAtlasPosition()
    {
        try
        {
            // При открытии атласа или смене локации, определяем позицию по центру атласа
            if (AtlasHasBeenClosed || !playerPositionDetermined)
            {
                // Находим ноду в центре атласа
                Node centerNode = GetClosestNodeToCenterScreen();
                if (centerNode != null && centerNode.MapNode?.Element != null)
                {
                    UpdatePlayerPosition(centerNode, "центр атласа при открытии");
                    AtlasHasBeenClosed = false;
                    return;
                }
            }

            // Если позиция уже определена, проверяем не сместился ли игрок
            if (AtlasPanel != null && AtlasPanel.Children != null && AtlasPanel.Children.Count > 0)
            {
                try
                {
                    var playerIndicator = AtlasPanel.Children
                        .FirstOrDefault(x => x?.TextureName?.Contains("AtlasPlayerLocationBg") == true);

                    if (playerIndicator != null)
                    {
                        var indicatorPosition = playerIndicator.GetClientRect().Center;
                        Node closestNode = null;
                        float minDistance = float.MaxValue;

                        lock (mapCacheLock)
                        {
                            foreach (var node in mapCache.Values)
                            {
                                if (node.MapNode?.Element != null)
                                {
                                    Vector2 nodePosition = node.MapNode.Element.GetClientRect().Center;
                                    float distance = Vector2.Distance(indicatorPosition, nodePosition);
                                    
                                    if (distance < minDistance)
                                    {
                                        minDistance = distance;
                                        closestNode = node;
                                    }
                                }
                            }
                        }

                        if (closestNode != null && minDistance < 150)
                        {
                            UpdatePlayerPosition(closestNode, "индикатор игрока");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Ошибка при обработке индикатора игрока: {ex.Message}");
                }
            }

            // Если все методы не сработали, используем текущую позицию
            if (!playerPositionDetermined)
            {
                if (lastPlayerAtlasNode != default)
                {
                    distanceCache.Clear();
                    cachedDistances.Clear();
                    needsDistanceRecalculation = true;
                }
                playerAtlasPosition = screenCenter;
                lastPlayerAtlasNode = default;
                LogMessage("Используем центр экрана как позицию игрока (fallback)");
            }
        }
        catch (Exception ex)
        {
            LogError($"Ошибка определения позиции игрока: {ex.Message}\n{ex.StackTrace}");
            playerAtlasPosition = screenCenter;
        }
    }

    private void UpdatePlayerPosition(Node node, string source)
    {
        if (node?.MapNode?.Element == null) return;

        playerAtlasPosition = node.MapNode.Element.GetClientRect().Center;
        
        if (node.Coordinates != lastPlayerAtlasNode)
        {
            lastPlayerAtlasNode = node.Coordinates;
            distanceCache.Clear();
            cachedDistances.Clear();
            routeNeedsUpdate = true;
            needSearchUpdate = true;
            needsDistanceRecalculation = true;
            playerPositionDetermined = true;
            LogMessage($"Позиция игрока определена как {node.Name} ({source})");
        }
    }

    private void UpdateDistanceCache()
    {
        if (!needsDistanceRecalculation) return;

        try
        {
            distanceCache.Clear();
            cachedDistances.Clear();

            lock (mapCacheLock)
            {
                foreach (var node in mapCache.Values)
                {
                    if (node?.MapNode?.Element == null) continue;

                    float distance = Vector2.Distance(playerAtlasPosition, node.MapNode.Element.GetClientRect().Center);
                    string key = node.Name;
                    
                    var normalizedDistance = Math.Min(distance / 50f, 1.0f);
                    var color = ColorHelper.GetColorForDistance(1.0f - normalizedDistance);
                    
                    distanceCache[key] = (distance, color);
                    cachedDistances[key] = distance;
                }
            }

            needsDistanceRecalculation = false;
        }
        catch (Exception ex)
        {
            LogError($"Ошибка обновления кэша расстояний: {ex.Message}");
        }
    }

    // Метод для сброса состояния при закрытии атласа
    private void OnAtlasClosed()
    {
        AtlasHasBeenClosed = true;
    }

    // Рисует вэйпоинт на карте
    private void DrawWaypoint(Waypoint waypoint) {
        if (!Settings.Waypoints.ShowWaypoints || waypoint.MapNode() == null || !waypoint.Show || !IsOnScreen(waypoint.MapNode().Element.GetClientRect().Center))
            return;

        Vector2 waypointSize = new Vector2(48, 48);        
        waypointSize *= waypoint.Scale;

        Vector2 iconPosition = waypoint.MapNode().Element.GetClientRect().Center - new Vector2(0, waypoint.MapNode().Element.GetClientRect().Height / 2);

        if (waypoint.MapNode().Element.GetChildAtIndex(0) != null)
            iconPosition -= new Vector2(0, waypoint.MapNode().Element.GetChildAtIndex(0).GetClientRect().Height);

        iconPosition -= new Vector2(0, 20);
        Vector2 waypointTextPosition = iconPosition - new Vector2(0, 10);
        
        DrawCenteredTextWithBackground(waypoint.Name, waypointTextPosition, Settings.Graphics.FontColor, Settings.Graphics.BackgroundColor, true, 10, 4);
        
        iconPosition -= new Vector2(waypointSize.X / 2, 0);
        RectangleF iconSize = new RectangleF(iconPosition.X, iconPosition.Y, waypointSize.X, waypointSize.Y);
        Graphics.DrawImage(IconsFile, iconSize, SpriteHelper.GetUV(waypoint.Icon), waypoint.Color);
    }

    // Рисует стрелку к вэйпоинту
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

    // Добавляет вэйпоинт
    private void AddWaypoint(Node cachedNode) {
        if (Settings.Waypoints.Waypoints.ContainsKey(cachedNode.Coordinates.ToString()))
            return;

        float weight = (cachedNode.Weight - minMapWeight) / (maxMapWeight - minMapWeight);
        Waypoint newWaypoint = cachedNode.ToWaypoint();
        newWaypoint.Icon = MapIconsIndex.LootFilterLargeWhiteUpsideDownHouse;
        newWaypoint.Color = ColorUtils.InterpolateColor(Settings.MapTypes.BadNodeColor, Settings.MapTypes.GoodNodeColor, weight);

        Settings.Waypoints.Waypoints.Add(cachedNode.Coordinates.ToString(), newWaypoint);
    }

    // Удаляет вэйпоинт по Node
    private void RemoveWaypoint(Node cachedNode) {
        if (!Settings.Waypoints.Waypoints.ContainsKey(cachedNode.Coordinates.ToString()))
            return;

        Settings.Waypoints.Waypoints.Remove(cachedNode.Coordinates.ToString());
    }
    
    // Удаляет вэйпоинт по Waypoint
    private void RemoveWaypoint(Waypoint waypoint) {
        Settings.Waypoints.Waypoints.Remove(waypoint.Coordinates.ToString());
    }

    // ... существующий код ...
    // Логируем все возможные коллекции дочерних элементов AtlasPanel
    private void LogAtlasPanelCollection(string label, IEnumerable<object> collection)
    {
        if (collection == null) return;
        foreach (var child in collection)
        {
            try
            {
                var element = child as dynamic;
                string texture = "";
                try { texture = element.TextureName; } catch { }
                string type = "";
                try { type = element.GetType().Name; } catch { }
                LogMessage($"AtlasPanel {label}: TextureName='{texture}', Type='{type}'");
            }
            catch { }
        }
    }
    
    private void DumpAtlasPanelHierarchy()
    {
        try
        {
            string path = Path.Combine(DirectoryFullName, "AtlasPanelDump.txt");
            using (var writer = new StreamWriter(path, false))
            {
                DumpCollection("Children", AtlasPanel.Children, writer, 0);
                // DumpCollection("Elements", AtlasPanel.Elements, writer, 0); // Удалено
                // DumpCollection("Decorations", AtlasPanel.Decorations, writer, 0); // Удалено
                // DumpCollection("Markers", AtlasPanel.Markers, writer, 0); // Удалено
            }
        }
        catch (Exception ex)
        {
            LogError($"Error dumping AtlasPanel hierarchy: {ex.Message}");
        }
    }

    private void DumpCollection(string label, IEnumerable<object> collection, StreamWriter writer, int indent)
    {
        if (collection == null) return;
        string pad = new string(' ', indent * 2);
        foreach (var child in collection)
        {
            try
            {
                var element = child as dynamic;
                string texture = "";
                try { texture = element.TextureName; } catch { }
                string type = "";
                try { type = element.GetType().Name; } catch { }
                writer.WriteLine($"{pad}{label}: TextureName='{texture}', Type='{type}'");
                // Рекурсивно логируем дочерние элементы, если есть
                try
                {
                    var subChildren = element.Children as IEnumerable<object>;
                    if (subChildren != null)
                        DumpCollection(label + ".Children", subChildren, writer, indent + 1);
                }
                catch { }
            }
            catch { }
        }
    }

    // Метод для дампа элементов AtlasPanel
    private void DumpAtlasElements()
    {
        // Логируем коллекции перед записью в файл
        LogAtlasPanelCollection("Children", AtlasPanel.Children);
        // LogAtlasPanelCollection("Elements", AtlasPanel.Elements); // Удалено
        // LogAtlasPanelCollection("Decorations", AtlasPanel.Decorations); // Удалено
        // LogAtlasPanelCollection("Markers", AtlasPanel.Markers); // Удалено

        try
        {
            string path = Path.Combine(DirectoryFullName, "AtlasDump.txt");
            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("======= ATLAS PANEL DUMP =======");
                writer.WriteLine("Время: " + DateTime.Now.ToString());
                
                // Логирование Children - расширено до 20 элементов
                writer.WriteLine("\n===== CHILDREN =====");
                if (AtlasPanel.Children != null)
                {
                    int maxElements = Math.Min(20, AtlasPanel.Children.Count);
                    for (int index = 0; index < maxElements; index++)
                    {
                        try
                        {
                            var child = AtlasPanel.Children[index];
                            string texture = "неизвестно";
                            try { texture = child.TextureName; } catch { }
                            
                            string typeName = "неизвестно";
                            try { typeName = child.GetType().Name; } catch { }
                            
                            Vector2 position = new Vector2(0, 0);
                            try { position = child.GetClientRect().Center; } catch { }
                            
                            writer.WriteLine($"Child[{index}]: Type={typeName}, Texture={texture}, Position={position}");
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine($"Child[{index}]: Ошибка получения данных: {ex.Message}");
                        }
                    }
                    writer.WriteLine($"... и еще {AtlasPanel.Children.Count - maxElements} элементов");
                }
                else
                {
                    writer.WriteLine("AtlasPanel.Children is null");
                }
                
                // Добавим дамп первых 10 узлов атласа для отладки
                writer.WriteLine("\n===== ATLAS NODES (FIRST 10) =====");
                if (AtlasPanel.Descriptions != null && AtlasPanel.Descriptions.Count > 0)
                {
                    int nodesToShow = Math.Min(10, AtlasPanel.Descriptions.Count);
                    for (int i = 0; i < nodesToShow; i++)
                    {
                        var desc = AtlasPanel.Descriptions[i];
                        try
                        {
                            string name = "неизвестно";
                            try { name = GetMapNameFromDescription(desc); } catch { }
                            
                            string coords = "неизвестно";
                            try { coords = desc.Coordinate.ToString(); } catch { }
                            
                            string status = "неизвестно";
                            try { 
                                status = $"Unlocked={desc.Element.IsUnlocked}, " +
                                        $"Visited={desc.Element.IsVisited}, " +
                                        $"Active={desc.Element.IsActive}"; 
                            } catch { }
                            
                            writer.WriteLine($"Node[{i}]: Name={name}, Coordinate={coords}, Status={status}");
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine($"Node[{i}]: Ошибка получения данных: {ex.Message}");
                        }
                    }
                    writer.WriteLine($"... и еще {AtlasPanel.Descriptions.Count - nodesToShow} узлов");
                }
                else
                {
                    writer.WriteLine("AtlasPanel.Descriptions is null или пуст");
                }
                
                // Сохраняем все свойства AtlasPanel
                writer.WriteLine("\n===== ATLAS PANEL PROPERTIES =====");
                var properties = AtlasPanel.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(AtlasPanel);
                        writer.WriteLine($"Property: {prop.Name} = {value}");
                    }
                    catch { writer.WriteLine($"Property: {prop.Name} = [Error getting value]"); }
                }
                
                writer.WriteLine("\n===== END OF DUMP =====");
            }
            
            LogMessage($"Atlas Panel dump created at {path}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to create Atlas Panel dump: {ex.Message}");
        }
    }

    private void AddNodeContentTypesFromTextures(AtlasNodeDescription node, Node toNode) {
        if (node.Element.GetChildAtIndex(0).GetChildAtIndex(0).Children.Any(x => x.TextureName.Contains("Corrupt")))
            if (Settings.MapContent.ContentTypes.TryGetValue("Corrupted", out Content corruption))
                toNode.Content.TryAdd(corruption.Name, corruption);

        if (node.Element.GetChildAtIndex(0).GetChildAtIndex(0).Children.Any(x => x.TextureName.Contains("CorruptionNexus")))
            if (Settings.MapContent.ContentTypes.TryGetValue("Corrupted Nexus", out Content nexus))
                toNode.Content.TryAdd(nexus.Name, nexus);

        if (node.Element.GetChildAtIndex(0).GetChildAtIndex(0).Children.Any(x => x.TextureName.Contains("Sanctification")))
            if (Settings.MapContent.ContentTypes.TryGetValue("Cleansed", out Content cleansed))
                toNode.Content.TryAdd(cleansed.Name, cleansed);

        if (node.Element.GetChildAtIndex(0).GetChildAtIndex(0).Children.Any(x => x.TextureName.Contains("UniqueMap")))
            if (Settings.MapContent.ContentTypes.TryGetValue("Unique Map", out Content uniqueMap))
                toNode.Content.TryAdd(uniqueMap.Name, uniqueMap);
    }

    private void ExecuteSearch()
    {
        if (!string.IsNullOrEmpty(pendingSearchQuery))
        {
            Settings.Search.SearchQuery = pendingSearchQuery;
            needSearchUpdate = true;
            searchResults.Clear();
            mapItems.Clear();
            isSearchInProgress = true;
            searchProgress = 0f;
            searchStatusText = "Поиск...";
            Task.Run(() => UpdateSearchResults());
        }
    }
}

public static class ColorHelper
{
    public static Vector4 GetColorForWeight(float normalizedValue)
    {
        return new Vector4(
            (1 - normalizedValue) * 0.8f + 0.2f,  // От красного к зеленому
            normalizedValue * 0.8f + 0.2f,
            0.2f,
            1.0f
        );
    }
    
    public static Vector4 GetColorForDistance(float normalizedValue)
    {
        return new Vector4(
            (1 - normalizedValue) * 0.8f + 0.2f,  // От красного к зеленому
            normalizedValue * 0.8f + 0.2f,
            0.2f,
            1.0f
        );
    }
}

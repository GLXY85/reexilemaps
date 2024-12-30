using System.Drawing;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using ImGuiNET;
using System.Numerics;
using System.Drawing;

namespace ExileMaps;

public class ExileMapsSettings : ISettings
{
    //Mandatory setting to allow enabling/disabling your plugin
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    // Feature settings
    [Menu("Toggle Features")]
    public FeatureSettings Features { get; set; } = new FeatureSettings();

    // Highlight settings
    [Menu("Content and Special Map Highlighting")]
    public HighlightSettings Highlights { get; set; } = new HighlightSettings();

    [Menu("Colours and performance settings.")]
    // Graphic settings
    public GraphicSettings Graphics { get; set; } = new GraphicSettings();
    
    [Menu("Individual Map Type Settings")]
    public MapHighlightSettings MapHighlightSettings { get; set; } = new MapHighlightSettings();
}

[Submenu(CollapsedByDefault = false)]
public class FeatureSettings
{
    [Menu("Atlas Range", "Range (from your current viewpoint) to process atlas nodes.")]
    public RangeNode<int> AtlasRange { get; set; } = new(2000, 100, 10000);

    [Menu("Map Node Highlighting", "Draw colored circles for selected map types.")]
    public ToggleNode NodeHighlighting { get; set; } = new ToggleNode(true);

    [Menu("Map Name Highlighting", "Use custom text and background colors for selected map types.")]
    public ToggleNode NameHighlighting { get; set; } = new ToggleNode(true);

    [Menu("Apply to Incomplete Maps", "Features will apply to all accessible and incomplete map nodes.")]
    public ToggleNode UnlockedNodes { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(UnlockedNodes))]
    [Menu("Draw Names for unlocked nodes", "Names will be drawn on the Atlas for unlocked map nodes.")]
    public ToggleNode UnlockedNames { get; set; } = new ToggleNode(true);

    [Menu("Apply to Locked Maps", "Features will apply to all inaccessible map nodes.")]
    public ToggleNode LockedNodes { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(LockedNodes))]
    [Menu("Draw Names for Locked nodes", "Names will be drawn on the Atlas for locked map nodes.")]
    public ToggleNode LockedNames { get; set; } = new ToggleNode(true);

    [Menu("Apply to Unrevealed Maps", "Features will apply to all unrevealed map nodes.")]
    public ToggleNode UnrevealedNodes { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(UnrevealedNodes))]
    [Menu("Draw Icons for Unrevealed Maps", "Icons will be drawn on the Atlas for unrevealed maps.")]
    public ToggleNode UnrevealedIcons { get; set; } = new ToggleNode(true);
    [ConditionalDisplay(nameof(UnrevealedNodes))]
    [Menu("Draw Names for Unrevealed Maps", "Names will be drawn on the Atlas for unrevealed maps.")]
    public ToggleNode UnrevealedNames { get; set; } = new ToggleNode(true);

    [Menu("Debug Mode", "Show node addresses on Atlas map")]
    public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
}

[Submenu(CollapsedByDefault = false)]
public class HighlightSettings
{
    [Menu("Highlight Breaches", "Highlight breaches with a ring on the Atlas")]
    public ToggleNode HighlightBreaches { get; set; } = new ToggleNode(true);

    [Menu("Highlight Delirium", "Highlight delirium with a ring on the Atlas")]
    public ToggleNode HighlightDelirium { get; set; } = new ToggleNode(true);

    [Menu("Highlight Expedition", "Highlight expeditions with a ring on the Atlas")]
    public ToggleNode HighlightExpedition { get; set; } = new ToggleNode(true);

    [Menu("Highlight Rituals", "Highlight rituals with a ring on the Atlas")]
    public ToggleNode HighlightRitual { get; set; } = new ToggleNode(true);

    [Menu("Highlight Bosses", "Highlight rituals with a ring on the Atlas")]
    public ToggleNode HighlightBosses { get; set; } = new ToggleNode(true);

    [Menu("Highlight Untainted Paradise Maps", "Highlight untainted paradise with a ring on the Atlas")]
    public ToggleNode HighlightUntaintedParadise { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(HighlightUntaintedParadise))]
    [Menu("Draw Lines to Untainted Paradise", "Draw lines to incomplete Untained Paradise maps on the Atlas")]
    public ToggleNode LineToParadise { get; set; } = new ToggleNode(false);
    
    [Menu("Draw Lines to Hideout", "Draw lines to incomplete Hideout maps on the Atlas")]
    public ToggleNode LineToHideout { get; set; } = new ToggleNode(false);
    [ConditionalDisplay(nameof(LineToHideout))]
    public TextNode HideoutFilter { get; set; } = new TextNode("Hideout");

    [Menu("Highlight Trader Maps", "Highlight traders with a ring on the Atlas")]
    public ToggleNode HighlightTrader { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(HighlightTrader))]
    [Menu("Draw Lines to Traders", "Draw lines to incomplete traders on the Atlas")]
    public ToggleNode LineToTrader { get; set; } = new ToggleNode(false);

    [Menu("Highlight Citadels", "Highlight citadels with a ring on the Atlas")]
    public ToggleNode HighlightCitadel { get; set; } = new ToggleNode(true);

    [ConditionalDisplay(nameof(HighlightCitadel))]
    [Menu("Draw Lines to Citadels", "Draw lines to incomplete citadels on the Atlas")]
    public ToggleNode LineToCitadel { get; set; } = new ToggleNode(false);

    [Menu("Draw Distance Markers on Lines", "Draw the name and distance to the node on the line")]
    public ToggleNode DrawDistanceOnLine { get; set; } = new ToggleNode(true);

    [Menu("Text Scale", "Text scale for distance markers on lines")]
    public RangeNode<float> DrawDistanceOnLineScale { get; set; } = new RangeNode<float>(0.03f, 0, 1);

    [Menu("Line Width")]
    public RangeNode<float> MapLineWidth { get; set; } = new RangeNode<float>(4.0f, 0, 10);
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

    [Menu("Breach Color", "Color of the ring around breaches on the Atlas")]
    public ColorNode breachColor { get; set; } = new ColorNode(Color.FromArgb(200, 143, 82, 246));

    [Menu("Delirium Color", "Color of the ring around delirium on the Atlas")]
    public ColorNode deliriumColor { get; set; } = new ColorNode(Color.FromArgb(200, 200, 200, 200));

    [Menu("Expedition Color", "Color of the ring around expeditions on the Atlas")]
    public ColorNode expeditionColor { get; set; } = new ColorNode(Color.FromArgb(200, 101, 129, 172));

    [Menu("Ritual Color", "Color of the ring around rituals on the Atlas")]
    public ColorNode ritualColor { get; set; } = new ColorNode(Color.FromArgb(200, 252, 3, 3));

    [Menu("Boss Color", "Color of the ring around bosses on the Atlas")]
    public ColorNode bossColor { get; set; } = new ColorNode(Color.FromArgb(200, 195, 156, 105));

    [Menu("Untainted Paradise Color", "Color of the ring around untainted paradise on the Atlas")]
    public ColorNode untaintedParadiseColor { get; set; } = new ColorNode(Color.FromArgb(200, 50, 200, 50));

    [Menu("Trader Color", "Color of the ring around traders on the Atlas")]
    public ColorNode traderColor { get; set; } = new ColorNode(Color.FromArgb(100, 0, 0, 0));

    [Menu("Citadel Color", "Color of the ring around citadels on the Atlas")]
    public ColorNode citadelColor { get; set; } = new ColorNode(Color.FromArgb(100, 0, 0, 0));

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
                if (ImGui.BeginTable("maps_table", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Enable", ImGuiTableColumnFlags.WidthFixed, 25f);
                    ImGui.TableSetupColumn("Map Name", ImGuiTableColumnFlags.WidthFixed, 150f);                                                              
                    ImGui.TableSetupColumn("Node Color", ImGuiTableColumnFlags.WidthFixed, 45f);     
                    ImGui.TableSetupColumn("Name Color", ImGuiTableColumnFlags.WidthFixed, 45f);               
                    ImGui.TableSetupColumn("BG Color", ImGuiTableColumnFlags.WidthFixed, 45f);          
                    ImGui.TableSetupColumn("Biomes", ImGuiTableColumnFlags.WidthStretch, 200f);      
                    ImGui.TableHeadersRow();

                    foreach (var map in Maps)
                    {
                        ImGui.TableNextRow();

                        // Enable Highlight
                        bool isMapHighlighted = map.Value.Highlight;

                        ImGui.TableNextColumn();
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

                        // Map Biomes
                        ImGui.TableNextColumn();
                        ImGui.Text("");



                        // Color highlightColor = Settings.Maps.Find(x => x.ID == map.ID).HighlightColor;                
                        // ImGui.TableNextColumn();
                        // if(ImGui.ColorEdit4($"##{map.ID}_color", ref highlightColor))
                        // {
                        //     Settings.Maps.Find(x => x.ID == map.ID).HighlightColor = highlightColor;
                        // }

                    }                
                }

                ImGui.EndTable();
            
            }
        };
    }
}

        
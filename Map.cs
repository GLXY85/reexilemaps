using System;
using System.Collections.Generic;
using System.Drawing;
using ExileCore2;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExileMaps;



public class Map
{
    public string Name { get; set; }
    public string ID { get; set; }
    public string[] BiomeList { get; set; }
    
    [JsonConverter(typeof(JsonColorConverter))]
    public Color NameColor { get; set; }

    [JsonConverter(typeof(JsonColorConverter))]
    public Color BackgroundColor { get; set; }

    [JsonConverter(typeof(JsonColorConverter))]
    public Color NodeColor { get; set; }

    

    public bool Highlight { get; set; } = false;
}

public class JsonColorConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string colorString = reader.GetString();
        return ColorTranslator.FromHtml(colorString);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        string colorString = ColorTranslator.ToHtml(value);
        writer.WriteStringValue(colorString);
    }
}
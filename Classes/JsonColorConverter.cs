using System;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace ExileMaps.Classes;

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
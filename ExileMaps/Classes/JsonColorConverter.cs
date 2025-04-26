using System;
using System.Drawing;
using Newtonsoft.Json;

namespace ReExileMaps.Classes;

public class JsonColorConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Color);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        string colorString = reader.Value?.ToString();
        return ParseColor(colorString);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        Color color = (Color)value;
        string colorString = $"{color.A}, {color.R}, {color.G}, {color.B}";
        writer.WriteValue(colorString);
    }

    private Color ParseColor(string colorString)
    {
        try
        {
            var parts = colorString.Split(',');
            if (parts.Length == 4)
            {
                int r = int.Parse(parts[1].Trim());
                int g = int.Parse(parts[2].Trim());
                int b = int.Parse(parts[3].Trim());
                int a = int.Parse(parts[0].Trim());
                return Color.FromArgb(a, r, g, b);
            }
            else
            {
                throw new ArgumentException("Invalid color format");
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error parsing color: {colorString}", ex);
        }
    }
}
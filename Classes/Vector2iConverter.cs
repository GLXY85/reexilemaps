using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameOffsets2.Native;

namespace ExileMaps.Classes
{
    public class Vector2iConverter : JsonConverter<Vector2i>
    {
        public override Vector2i Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string[] parts = reader.GetString().Trim('{', '}').Split(',');
            if (parts.Length == 2)
            {
                return new Vector2i(int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));
            }
            else
            {
                throw new ArgumentException("Invalid Vector2i format");
            }
        }

        public override void Write(Utf8JsonWriter writer, Vector2i value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"{value.X}, {value.Y}");
        }
    }
}
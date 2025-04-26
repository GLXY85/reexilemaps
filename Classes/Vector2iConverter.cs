using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GameOffsets.Native;

namespace ReExileMaps.Classes
{
    public class Vector2iConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector2i);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                string s = (string)reader.Value;
                string[] parts = s.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                {
                    return new Vector2i(x, y);
                }
            }
            return new Vector2i(0, 0);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var v = (Vector2i)value;
            writer.WriteValue($"{v.X},{v.Y}");
        }
    }
}


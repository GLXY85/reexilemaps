using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GameOffsets2.Native;

namespace ExileMaps.Classes
{
    public class Vector2iConverter : JsonConverter<Vector2i>
    {
        public override void WriteJson(JsonWriter writer, Vector2i value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.X},{value.Y}");
        }

        public override Vector2i ReadJson(JsonReader reader, Type objectType, Vector2i existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = reader.Value.ToString().Split(',');
            return new Vector2i(int.Parse(value[0]), int.Parse(value[1]));
        }

        

    }
}
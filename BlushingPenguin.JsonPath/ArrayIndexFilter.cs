using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace BlushingPenguin.JsonPath
{
    internal class ArrayIndexFilter : PathFilter
    {
        public int? Index { get; set; }

        public override IEnumerable<JsonElement> ExecuteFilter(JsonElement root, IEnumerable<JsonElement> current, bool errorWhenNoMatch)
        {
            foreach (JsonElement t in current)
            {
                if (Index != null)
                {
                    JsonElement? v = GetTokenIndex(t, errorWhenNoMatch, Index.GetValueOrDefault());

                    if (v.HasValue)
                    {
                        yield return v.Value;
                    }
                }
                else
                {
                    if (t.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement v in t.EnumerateArray())
                        {
                            yield return v;
                        }
                    }
                    else
                    {
                        if (errorWhenNoMatch)
                        {
                            throw new JsonException("Index * not valid on {0}.".FormatWith(CultureInfo.InvariantCulture, t.GetType().Name));
                        }
                    }
                }
            }
        }
    }
}

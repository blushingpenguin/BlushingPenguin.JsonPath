using System.Collections.Generic;
using System.Text.Json;

namespace BlushingPenguin.JsonPath
{
    internal class ScanFilter : PathFilter
    {
        internal string? Name;

        public ScanFilter(string? name)
        {
            Name = name;
        }

        public override IEnumerable<JsonElement> ExecuteFilter(JsonElement root, IEnumerable<JsonElement> current, bool errorWhenNoMatch)
        {
            foreach (JsonElement c in current)
            {
                foreach (var e in GetScanValues(c))
                {
                    if (e.Name == Name)
                    {
                        yield return e.Value;
                    }
                }
            }
        }
    }
}

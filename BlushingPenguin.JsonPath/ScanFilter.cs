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
                // if (Name == null)
                // {
                //     yield return c;
                // }

                foreach (var e in GetScanValues(c))
                {
                    System.Diagnostics.Debug.WriteLine($"sv = {e}, me name = {Name}");
                    if (e.Name == Name)
                    {
                        yield return e.Value;
                    }
                }
            }
        }
    }
}

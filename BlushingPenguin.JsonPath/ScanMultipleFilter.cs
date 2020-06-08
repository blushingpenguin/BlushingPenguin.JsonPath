using System.Collections.Generic;
using System.Text.Json;

namespace BlushingPenguin.JsonPath
{
    internal class ScanMultipleFilter : PathFilter
    {
        private List<string> _names;

        public ScanMultipleFilter(List<string> names)
        {
            _names = names;
        }

        public override IEnumerable<JsonElement> ExecuteFilter(JsonElement root, IEnumerable<JsonElement> current, bool errorWhenNoMatch)
        {
            foreach (JsonElement c in current)
            {
                JsonElement? value = c;

                foreach (var e in GetScanValues(c))
                {
                    if (e.Name != null)
                    {
                        foreach (string name in _names)
                        {
                            if (e.Name == name)
                            {
                                yield return e.Value;
                            }
                        }
                    }
                }
            }
        }
    }
}

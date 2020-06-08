using System.Text.Json;
using System.Collections.Generic;

namespace BlushingPenguin.JsonPath
{
    internal class ArrayMultipleIndexFilter : PathFilter
    {
        internal List<int> Indexes;

        public ArrayMultipleIndexFilter(List<int> indexes)
        {
            Indexes = indexes;
        }

        public override IEnumerable<JsonElement> ExecuteFilter(JsonElement root, IEnumerable<JsonElement> current, bool errorWhenNoMatch)
        {
            foreach (JsonElement t in current)
            {
                foreach (int i in Indexes)
                {
                    JsonElement? v = GetTokenIndex(t, errorWhenNoMatch, i);

                    if (v.HasValue)
                    {
                        yield return v.Value;
                    }
                }
            }
        }
    }
}

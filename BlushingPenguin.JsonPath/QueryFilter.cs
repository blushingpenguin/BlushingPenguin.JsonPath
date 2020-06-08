using System;
using System.Collections.Generic;
using System.Text.Json;

namespace BlushingPenguin.JsonPath
{
    internal class QueryFilter : PathFilter
    {
        internal QueryExpression Expression;

        public QueryFilter(QueryExpression expression)
        {
            Expression = expression;
        }

        public override IEnumerable<JsonElement> ExecuteFilter(JsonElement root, IEnumerable<JsonElement> current, bool errorWhenNoMatch)
        {
            foreach (JsonElement t in current)
            {
                if (t.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement v in t.EnumerateArray())
                    {
                        if (Expression.IsMatch(root, v))
                        {
                            yield return v;
                        }
                    }
                }
                else if (t.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty v in t.EnumerateObject())
                    {
                        if (Expression.IsMatch(root, v.Value))
                        {
                            yield return v.Value;
                        }
                    }
                }
            }
        }
    }
}

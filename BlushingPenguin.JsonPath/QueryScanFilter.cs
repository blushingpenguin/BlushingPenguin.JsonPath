using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace BlushingPenguin.JsonPath
{
    internal class QueryScanFilter : PathFilter
    {
        internal QueryExpression Expression;

        public QueryScanFilter(QueryExpression expression)
        {
            Expression = expression;
        }

        public override IEnumerable<JsonElement> ExecuteFilter(JsonElement root, IEnumerable<JsonElement> current, bool errorWhenNoMatch)
        {
            foreach (JsonElement t in current)
            {
                foreach (var d in GetScanValues(t))
                // foreach (var d in t.DescendantsAndSelf())
                {
                    if (Expression.IsMatch(root, d.Value))
                    {
                        yield return d.Value;
                    }
                }
            }
        }
    }
}

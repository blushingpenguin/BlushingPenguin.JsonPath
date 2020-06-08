using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlushingPenguin.JsonPath
{
    internal enum QueryOperator
    {
        None = 0,
        Equals = 1,
        NotEquals = 2,
        Exists = 3,
        LessThan = 4,
        LessThanOrEquals = 5,
        GreaterThan = 6,
        GreaterThanOrEquals = 7,
        And = 8,
        Or = 9,
        RegexEquals = 10,
        StrictEquals = 11,
        StrictNotEquals = 12
    }

    internal abstract class QueryExpression
    {
        internal QueryOperator Operator;

        public QueryExpression(QueryOperator @operator)
        {
            Operator = @operator;
        }

        public abstract bool IsMatch(JsonElement root, JsonElement t);
    }

    internal class CompositeExpression : QueryExpression
    {
        public List<QueryExpression> Expressions { get; set; }

        public CompositeExpression(QueryOperator @operator) : base(@operator)
        {
            Expressions = new List<QueryExpression>();
        }

        public override bool IsMatch(JsonElement root, JsonElement t)
        {
            switch (Operator)
            {
                case QueryOperator.And:
                    foreach (QueryExpression e in Expressions)
                    {
                        if (!e.IsMatch(root, t))
                        {
                            return false;
                        }
                    }
                    return true;
                case QueryOperator.Or:
                    foreach (QueryExpression e in Expressions)
                    {
                        if (e.IsMatch(root, t))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    internal class BooleanQueryExpression : QueryExpression
    {
        public readonly object Left;
        public readonly object? Right;

        public BooleanQueryExpression(QueryOperator @operator, object left, object? right) : base(@operator)
        {
            Left = left;
            Right = right;
        }

        private IEnumerable<JsonElement> GetResult(JsonElement root, JsonElement t, object? o)
        {
            if (o is JsonElement resultToken)
            {
                return new[] { resultToken };
            }

            if (o is List<PathFilter> pathFilters)
            {
                return JPath.Evaluate(pathFilters, root, t, false);
            }

            return Array.Empty<JsonElement>();
        }

        public override bool IsMatch(JsonElement root, JsonElement t)
        {
            if (Operator == QueryOperator.Exists)
            {
                return GetResult(root, t, Left).Any();
            }

            using (IEnumerator<JsonElement> leftResults = GetResult(root, t, Left).GetEnumerator())
            {
                if (leftResults.MoveNext())
                {
                    IEnumerable<JsonElement> rightResultsEn = GetResult(root, t, Right);
                    ICollection<JsonElement> rightResults = rightResultsEn as ICollection<JsonElement> ?? rightResultsEn.ToList();

                    do
                    {
                        JsonElement leftResult = leftResults.Current;
                        foreach (JsonElement rightResult in rightResults)
                        {
                            if (MatchTokens(leftResult, rightResult))
                            {
                                return true;
                            }
                        }
                    } while (leftResults.MoveNext());
                }
            }

            return false;
        }

        internal static bool TryGetNumberValue(JsonElement value, out double num)
        {
            if (value.ValueKind == JsonValueKind.Number)
            {
                num = value.GetDouble();
                return true;
            }
            if (value.ValueKind == JsonValueKind.String &&
                Double.TryParse(value.GetString(), out num))
            {
                return true;
            }
            num = default;
            return false;
        }

        internal static JsonValueKind[] _valueKindSortOrder = new JsonValueKind[]
        {
            JsonValueKind.Undefined,
            JsonValueKind.Null,
            JsonValueKind.Number,
            JsonValueKind.String,
            JsonValueKind.Object,
            JsonValueKind.Array,
            JsonValueKind.False,
            JsonValueKind.True
        };

        internal static int Compare(JsonElement leftValue, JsonElement rightValue)
        {
            if (leftValue.ValueKind == rightValue.ValueKind)
            {
                switch (leftValue.ValueKind)
                {
                    case JsonValueKind.False:
                    case JsonValueKind.True:
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                        return 0;
                    case JsonValueKind.String:
                        return leftValue.GetString().CompareTo(rightValue.GetString());
                    case JsonValueKind.Number:
                        return leftValue.GetDouble().CompareTo(rightValue.GetDouble());
                    default:
                        throw new InvalidOperationException($"Unknown json value kind: {leftValue.ValueKind}");
                }
            }

            // num/string comparison
            if (TryGetNumberValue(leftValue, out var leftNum) &&
                TryGetNumberValue(rightValue, out var rightNum))
            {
                return leftNum.CompareTo(rightNum);
            }

            return Array.IndexOf(_valueKindSortOrder, leftValue.ValueKind).CompareTo(
                Array.IndexOf(_valueKindSortOrder, rightValue.ValueKind));
        }

        public bool IsArrayOrObject(JsonElement v) =>
            v.ValueKind == JsonValueKind.Array || v.ValueKind == JsonValueKind.Object;

        private bool MatchTokens(JsonElement leftValue, JsonElement rightValue)
        {
            if (!IsArrayOrObject(leftValue) && !IsArrayOrObject(rightValue))
            {
                switch (Operator)
                {
                    case QueryOperator.RegexEquals:
                        if (RegexEquals(leftValue, rightValue))
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.Equals:
                        if (EqualsWithStringCoercion(leftValue, rightValue))
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.StrictEquals:
                        if (EqualsWithStrictMatch(leftValue, rightValue))
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.NotEquals:
                        if (!EqualsWithStringCoercion(leftValue, rightValue))
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.StrictNotEquals:
                        if (!EqualsWithStrictMatch(leftValue, rightValue))
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.GreaterThan:
                        if (Compare(leftValue, rightValue) > 0)
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.GreaterThanOrEquals:
                        if (Compare(leftValue, rightValue) >= 0)
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.LessThan:
                        if (Compare(leftValue, rightValue) < 0)
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.LessThanOrEquals:
                        if (Compare(leftValue, rightValue) <= 0)
                        {
                            return true;
                        }
                        break;
                    case QueryOperator.Exists:
                        return true;
                }
            }
            else
            {
                switch (Operator)
                {
                    case QueryOperator.Exists:
                    // you can only specify primitive types in a comparison
                    // notequals will always be true
                    case QueryOperator.NotEquals:
                        return true;
                }
            }

            return false;
        }

        private static bool RegexEquals(JsonElement input, JsonElement pattern)
        {
            if (input.ValueKind != JsonValueKind.String || pattern.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string regexText = pattern.GetString();
            int patternOptionDelimiterIndex = regexText.LastIndexOf('/');

            string patternText = regexText.Substring(1, patternOptionDelimiterIndex - 1);
            string optionsText = regexText.Substring(patternOptionDelimiterIndex + 1);

            return Regex.IsMatch(input.GetString(), patternText, MiscellaneousUtils.GetRegexOptions(optionsText));
        }

        internal static bool EqualsWithStringCoercion(JsonElement value, JsonElement queryValue)
        {
            if (value.Equals(queryValue))
            {
                return true;
            }

            // Handle comparing an integer with a float
            // e.g. Comparing 1 and 1.0
            if (value.ValueKind == JsonValueKind.Number && queryValue.ValueKind == JsonValueKind.Number)
            {
                return value.GetDouble() == queryValue.GetDouble();
            }

            if (queryValue.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            
            return string.Equals(value.ToString(), queryValue.GetString(), StringComparison.Ordinal);
        }

        internal static bool EqualsWithStrictMatch(JsonElement value, JsonElement queryValue)
        {
            // ?ValidationUtils.ArgumentNotNull(value, nameof(value));
            // ?ValidationUtils.ArgumentNotNull(queryValue, nameof(queryValue));
            if (value.ValueKind != queryValue.ValueKind)
            {
                return false;
            }

            // Handle comparing an integer with a float
            // e.g. Comparing 1 and 1.0
            return Compare(value, queryValue) == 0;
        }
    }
}

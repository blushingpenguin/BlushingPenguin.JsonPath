#region License
// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace BlushingPenguin.JsonPath.Test
{
    // https://stackoverflow.com/questions/60580743/what-is-equivalent-in-jtoken-deepequal-in-system-text-json
    public class JsonElementComparer : IEqualityComparer<JsonElement>
    {
        public static JsonElementComparer Instance { get; } = new JsonElementComparer();

        public JsonElementComparer() : this(-1) { }

        public JsonElementComparer(int maxHashDepth) => this.MaxHashDepth = maxHashDepth;

        int MaxHashDepth { get; } = -1;

        public bool Equals(JsonElement x, JsonElement y)
        {
            if (x.ValueKind != y.ValueKind)
            {
                return false;
            }

            switch (x.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Undefined:
                    return true;

                case JsonValueKind.Number:
                    return x.GetRawText() == y.GetRawText();

                case JsonValueKind.String:
                    return x.GetString() == y.GetString();

                case JsonValueKind.Array:
                    return x.EnumerateArray().SequenceEqual(y.EnumerateArray(), this);

                case JsonValueKind.Object:
                    {
                        // Surprisingly, JsonDocument fully supports duplicate property names.
                        // I.e. it's perfectly happy to parse {"Value":"a", "Value" : "b"} and will store both
                        // key/value pairs inside the document!
                        // A close reading of https://tools.ietf.org/html/rfc8259#section-4 seems to indicate that
                        // such objects are allowed but not recommended, and when they arise, interpretation of 
                        // identically-named properties is order-dependent.  
                        // So stably sorting by name then comparing values seems the way to go.
                        var xPropertiesUnsorted = x.EnumerateObject().ToList();
                        var yPropertiesUnsorted = y.EnumerateObject().ToList();
                        if (xPropertiesUnsorted.Count != yPropertiesUnsorted.Count)
                        {
                            return false;
                        }
                        var xProperties = xPropertiesUnsorted.OrderBy(p => p.Name, StringComparer.Ordinal);
                        var yProperties = yPropertiesUnsorted.OrderBy(p => p.Name, StringComparer.Ordinal);
                        foreach (var (px, py) in xProperties.Zip(yProperties))
                        {
                            if (px.Name != py.Name)
                            {
                                return false;
                            }
                            if (!Equals(px.Value, py.Value))
                            {
                                return false;
                            }
                        }
                        return true;
                    }

                default:
                    throw new JsonException(string.Format("Unknown JsonValueKind {0}", x.ValueKind));
            }
        }

        public int GetHashCode(JsonElement obj)
        {
            var hash = new HashCode(); // New in .Net core: https://docs.microsoft.com/en-us/dotnet/api/system.hashcode
            ComputeHashCode(obj, ref hash, 0);
            return hash.ToHashCode();
        }

        void ComputeHashCode(JsonElement obj, ref HashCode hash, int depth)
        {
            hash.Add(obj.ValueKind);

            switch (obj.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Undefined:
                    break;

                case JsonValueKind.Number:
                    hash.Add(obj.GetRawText());
                    break;

                case JsonValueKind.String:
                    hash.Add(obj.GetString());
                    break;

                case JsonValueKind.Array:
                    if (depth != MaxHashDepth)
                        foreach (var item in obj.EnumerateArray())
                            ComputeHashCode(item, ref hash, depth + 1);
                    else
                        hash.Add(obj.GetArrayLength());
                    break;

                case JsonValueKind.Object:
                    foreach (var property in obj.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        hash.Add(property.Name);
                        if (depth != MaxHashDepth)
                            ComputeHashCode(property.Value, ref hash, depth + 1);
                    }
                    break;

                default:
                    throw new JsonException(string.Format("Unknown JsonValueKind {0}", obj.ValueKind));
            }
        }
    }

    public static class JsonElementExtensions
    {
        public static bool DeepEquals(this JsonElement left, JsonElement? right)
        {
            if (right == null)
            {
                return false;
            }
            return JsonElementComparer.Instance.Equals(left, right.Value);
        }

        public static bool DeepEquals(this JsonDocument left, JsonElement? right)
        {
            return DeepEquals(left.RootElement, right);
        }

        public static bool DeepEquals(this JsonDocument left, JsonDocument? right)
        {
            return DeepEquals(left.RootElement, right?.RootElement);
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class JPathExecuteTests
    {
        public const string IsoDateFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFK";

        [Test]
        public void GreaterThanIssue1518()
        {
            string statusJson = @"{""usingmem"": ""214376""}";//214,376
            var jObj = JsonDocument.Parse(statusJson);

            var aa = jObj.SelectToken("$..[?(@.usingmem>10)]");//found,10
            Assert.IsTrue(jObj.DeepEquals(aa));

            var bb = jObj.SelectToken("$..[?(@.usingmem>27000)]");//null, 27,000
            Assert.IsTrue(jObj.DeepEquals(bb));

            var cc = jObj.SelectToken("$..[?(@.usingmem>21437)]");//found, 21,437
            Assert.IsTrue(jObj.DeepEquals(cc));

            var dd = jObj.SelectToken("$..[?(@.usingmem>21438)]");//null,21,438
            Assert.IsTrue(jObj.DeepEquals(dd));
        }

        [Test]
        public void GreaterThanWithIntegerParameterAndStringValue()
        {
            string json = @"{
  ""persons"": [
    {
      ""name""  : ""John"",
      ""age"": ""26""
    },
    {
      ""name""  : ""Jane"",
      ""age"": ""2""
    }
  ]
}";

            var models = JsonDocument.Parse(json);

            var results = models.SelectTokens("$.persons[?(@.age > 3)]").ToList();

            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void GreaterThanWithStringParameterAndIntegerValue()
        {
            string json = @"{
  ""persons"": [
    {
      ""name""  : ""John"",
      ""age"": 26
    },
    {
      ""name""  : ""Jane"",
      ""age"": 2
    }
  ]
}";

            JsonDocument models = JsonDocument.Parse(json);

            var results = models.SelectTokens("$.persons[?(@.age > '3')]").ToList();

            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void RecursiveWildcard()
        {
            string json = @"{
    ""a"": [
        {
            ""id"": 1
        }
    ],
    ""b"": [
        {
            ""id"": 2
        },
        {
            ""id"": 3,
            ""c"": {
                ""id"": 4
            }
        }
    ],
    ""d"": [
        {
            ""id"": 5
        }
    ]
}";

            JsonDocument models = JsonDocument.Parse(json);

            var results = models.SelectTokens("$.b..*.id").ToList();

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(2, results[0].GetInt32());
            Assert.AreEqual(3, results[1].GetInt32());
            Assert.AreEqual(4, results[2].GetInt32());
        }

        [Test]
        public void ScanFilter()
        {
            string json = @"{
  ""elements"": [
    {
      ""id"": ""A"",
      ""children"": [
        {
          ""id"": ""AA"",
          ""children"": [
            {
              ""id"": ""AAA""
            },
            {
              ""id"": ""AAB""
            }
          ]
        },
        {
          ""id"": ""AB""
        }
      ]
    },
    {
      ""id"": ""B"",
      ""children"": []
    }
  ]
}";

            JsonDocument models = JsonDocument.Parse(json);

            var results = models.SelectTokens("$.elements..[?(@.id=='AAA')]").ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(models.RootElement
                .GetProperty("elements")[0].GetProperty("children")[0].GetProperty("children")[0],
                results[0]);
            // Assert.AreEqual(models["elements"][0]["children"][0]["children"][0], results[0]);
        }

        [Test]
        public void FilterTrue()
        {
            string json = @"{
  ""elements"": [
    {
      ""id"": ""A"",
      ""children"": [
        {
          ""id"": ""AA"",
          ""children"": [
            {
              ""id"": ""AAA""
            },
            {
              ""id"": ""AAB""
            }
          ]
        },
        {
          ""id"": ""AB""
        }
      ]
    },
    {
      ""id"": ""B"",
      ""children"": []
    }
  ]
}";

            JsonDocument models = JsonDocument.Parse(json);

            var results = models.SelectTokens("$.elements[?(true)]").ToList();

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(results[0], models.RootElement.GetProperty("elements")[0]);
            Assert.AreEqual(results[1], models.RootElement.GetProperty("elements")[1]);
        }

        [Test]
        public void ScanFilterTrue()
        {
            string json = @"{
  ""elements"": [
    {
      ""id"": ""A"",
      ""children"": [
        {
          ""id"": ""AA"",
          ""children"": [
            {
              ""id"": ""AAA""
            },
            {
              ""id"": ""AAB""
            }
          ]
        },
        {
          ""id"": ""AB""
        }
      ]
    },
    {
      ""id"": ""B"",
      ""children"": []
    }
  ]
}";

            JsonDocument models = JsonDocument.Parse(json);

            var results = models.SelectTokens("$.elements..[?(true)]").ToList();

            Assert.AreEqual(25, results.Count);
        }

        [Test]
        public void ScanQuoted()
        {
            string json = @"{
    ""Node1"": {
        ""Child1"": {
            ""Name"": ""IsMe"",
            ""TargetNode"": {
                ""Prop1"": ""Val1"",
                ""Prop2"": ""Val2""
            }
        },
        ""My.Child.Node"": {
            ""TargetNode"": {
                ""Prop1"": ""Val1"",
                ""Prop2"": ""Val2""
            }
        }
    },
    ""Node2"": {
        ""TargetNode"": {
            ""Prop1"": ""Val1"",
            ""Prop2"": ""Val2""
        }
    }
}";

            JsonDocument models = JsonDocument.Parse(json);

            int result = models.SelectTokens("$..['My.Child.Node']").Count();
            Assert.AreEqual(1, result);

            result = models.SelectTokens("..['My.Child.Node']").Count();
            Assert.AreEqual(1, result);
        }

        [Test]
        public void ScanMultipleQuoted()
        {
            string json = @"{
    ""Node1"": {
        ""Child1"": {
            ""Name"": ""IsMe"",
            ""TargetNode"": {
                ""Prop1"": ""Val1"",
                ""Prop2"": ""Val2""
            }
        },
        ""My.Child.Node"": {
            ""TargetNode"": {
                ""Prop1"": ""Val3"",
                ""Prop2"": ""Val4""
            }
        }
    },
    ""Node2"": {
        ""TargetNode"": {
            ""Prop1"": ""Val5"",
            ""Prop2"": ""Val6""
        }
    }
}";

            JsonDocument models = JsonDocument.Parse(json);

            var results = models.SelectTokens("$..['My.Child.Node','Prop1','Prop2']").ToList();
            Assert.AreEqual("Val1", results[0].GetString());
            Assert.AreEqual("Val2", results[1].GetString());
            Assert.AreEqual(JsonValueKind.Object, results[2].ValueKind);
            Assert.AreEqual("Val3", results[3].GetString());
            Assert.AreEqual("Val4", results[4].GetString());
            Assert.AreEqual("Val5", results[5].GetString());
            Assert.AreEqual("Val6", results[6].GetString());
        }

        [Test]
        public void ParseWithEmptyArrayContent()
        {
            var json = @"{
    ""controls"": [
        {
            ""messages"": {
                ""addSuggestion"": {
                    ""en-US"": ""Add""
                }
            }
        },
        {
            ""header"": {
                ""controls"": []
            },
            ""controls"": [
                {
                    ""controls"": [
                        {
                            ""defaultCaption"": {
                                ""en-US"": ""Sort by""
                            },
                            ""sortOptions"": [
                                {
                                    ""label"": {
                                        ""en-US"": ""Name""
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        }
    ]
}";
            JsonDocument jToken = JsonDocument.Parse(json);
            IList<JsonElement> tokens = jToken.SelectTokens("$..en-US").ToList();

            Assert.AreEqual(3, tokens.Count);
            Assert.AreEqual("Add", tokens[0].GetString());
            Assert.AreEqual("Sort by", tokens[1].GetString());
            Assert.AreEqual("Name", tokens[2].GetString());
        }

        [Test]
        public void SelectTokenAfterEmptyContainer()
        {
            string json = @"{
    ""cont"": [],
    ""test"": ""no one will find me""
}";

            var o = JsonDocument.Parse(json);

            var results = o.SelectTokens("$..test").ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("no one will find me", results[0].GetString());
        }

        [Test]
        public void EvaluatePropertyWithRequired()
        {
            string json = "{\"bookId\":\"1000\"}";
            var o = JsonDocument.Parse(json);

            var bookId = o.SelectToken("bookId", true)?.GetString();

            Assert.AreEqual("1000", bookId);
        }

        [Test]
        public void EvaluateEmptyPropertyIndexer()
        {
            var o = JsonDocument.Parse(@"{ """": 1 }");

            var t = o.SelectToken("['']");
            Assert.AreEqual(1, t?.GetInt32());
        }

        [Test]
        public void EvaluateEmptyString()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            var t = o.SelectToken("");
            Assert.IsTrue(o.DeepEquals(t));

            t = o.SelectToken("['']");
            Assert.AreEqual(null, t);
        }

        [Test]
        public void EvaluateEmptyStringWithMatchingEmptyProperty()
        {
            var o = JsonDocument.Parse(@"{ "" "": 1 }");

            var t = o.SelectToken("[' ']");
            Assert.AreEqual(1, t?.GetInt32());
        }

        [Test]
        public void EvaluateWhitespaceString()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            var t = o.SelectToken(" ");
            Assert.IsTrue(o.DeepEquals(t));
        }

        [Test]
        public void EvaluateDollarString()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            var t = o.SelectToken("$");
            Assert.IsTrue(o.DeepEquals(t));
        }

        [Test]
        public void EvaluateDollarTypeString()
        {
            var o = JsonDocument.Parse(@"{ ""$values"": [1, 2, 3] }");

            var t = o.SelectToken("$values[1]");
            Assert.AreEqual(2, t?.GetInt32());
        }

        [Test]
        public void EvaluateSingleProperty()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            var t = o.SelectToken("Blah");
            Assert.IsNotNull(t);
            Assert.AreEqual(JsonValueKind.Number, t?.ValueKind);
            Assert.AreEqual(1, t?.GetInt32());
        }

        [Test]
        public void EvaluateWildcardProperty()
        {
            var o = JsonDocument.Parse(@"
            {
                ""Blah"": 1,
                ""Blah2"": 2
            }");

            var t = o.SelectTokens("$.*").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual(1, t[0].GetInt32());
            Assert.AreEqual(2, t[1].GetInt32());
        }

        [Test]
        public void QuoteName()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            var t = o.SelectToken("['Blah']");
            Assert.IsNotNull(t);
            Assert.AreEqual(JsonValueKind.Number, t?.ValueKind);
            Assert.AreEqual(1, t?.GetInt32());
        }

        [Test]
        public void EvaluateMissingProperty()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            var t = o.SelectToken("Missing[1]");
            Assert.IsNull(t);
        }

        [Test]
        public void EvaluateIndexerOnObject()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            var t = o.SelectToken("[1]");
            Assert.IsNull(t);
        }

        [Test]
        public void EvaluateIndexerOnObjectWithError()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            Assert.Throws<JsonException>(() => o.SelectToken("[1]", true),
                @"Index 1 not valid on JsonDocument.");
        }

        [Test]
        public void EvaluateWildcardIndexOnObjectWithError()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            Assert.Throws<JsonException>(() => o.SelectToken("[*]", true),
                @"Index * not valid on JsonDocument.");
        }

        [Test]
        public void EvaluateSliceOnObjectWithError()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            Assert.Throws<JsonException>(() => o.SelectToken("[:]", true),
                @"Array slice is not valid on JsonDocument.");
        }

        [Test]
        public void EvaluatePropertyOnArray()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5]");

            var t = a.SelectToken("BlahBlah");
            Assert.IsNull(t);
        }

        [Test]
        public void EvaluateMultipleResultsError()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5]");

            Assert.Throws<JsonException>(() => a.SelectToken("[0, 1]"),
                @"Path returned multiple tokens.");
        }

        [Test]
        public void EvaluatePropertyOnArrayWithError()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5]");

            Assert.Throws<JsonException>(() => a.SelectToken("BlahBlah", true),
                @"Property 'BlahBlah' not valid on JsonArray.");
        }

        [Test]
        public void EvaluateNoResultsWithMultipleArrayIndexes()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5]");

            Assert.Throws<JsonException>(() => a.SelectToken("[9,10]", true),
                @"Index 9 outside the bounds of JsonArray.");
        }

        [Test]
        public void EvaluateMissingPropertyWithError()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");
            Assert.Throws<JsonException>(() => o.SelectToken("Missing", true),
                "Property 'Missing' does not exist on JsonDocument.");
        }

        [Test]
        public void EvaluatePropertyWithoutError()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            var v = o.SelectToken("Blah", true);
            Assert.IsTrue(v?.GetInt32() == 1);
        }

        [Test]
        public void EvaluateMissingPropertyIndexWithError()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": 1 }");

            Assert.Throws<JsonException>(() => o.SelectToken("['Missing','Missing2']", true),
                "Property 'Missing' does not exist on JsonDocument.");
        }

        [Test]
        public void EvaluateMultiPropertyIndexOnArrayWithError()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5]");

            Assert.Throws<JsonException>(() => a.SelectToken("['Missing','Missing2']", true),
                "Properties 'Missing', 'Missing2' not valid on JsonArray.");
        }

        [Test]
        public void EvaluateArraySliceWithError()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5]");

            Assert.Throws<JsonException>(() => a.SelectToken("[99:]", true),
                "Array slice of 99 to * returned no results.");

            Assert.Throws<JsonException>(() => a.SelectToken("[1:-19]", true),
                "Array slice of 1 to -19 returned no results.");

            Assert.Throws<JsonException>(() => a.SelectToken("[:-19]", true),
                "Array slice of * to -19 returned no results.");

            a = JsonDocument.Parse("[]");

            Assert.Throws<JsonException>(() => a.SelectToken("[:]", true),
                "Array slice of * to * returned no results.");
        }

        [Test]
        public void EvaluateOutOfBoundsIndxer()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5]");

            var t = a.SelectToken("[1000].Ha");
            Assert.IsNull(t);
        }

        [Test]
        public void EvaluateArrayOutOfBoundsIndxerWithError()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5]");

            Assert.Throws<JsonException>(() => a.SelectToken("[1000].Ha", true),
                "Index 1000 outside the bounds of JsonArray.");
        }

        [Test]
        public void EvaluateArray()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4]");

            var t = a.SelectToken("[1]");
            Assert.IsNotNull(t);
            Assert.AreEqual(JsonValueKind.Number, t?.ValueKind);
            Assert.AreEqual(2, t?.GetInt32());
        }

        [Test]
        public void EvaluateArraySlice()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4, 5, 6, 7, 8, 9]");
            List<JsonElement>? t = null;

            t = a.SelectTokens("[-3:]").ToList();
            Assert.AreEqual(3, t.Count);
            Assert.AreEqual(7, t[0].GetInt32());
            Assert.AreEqual(8, t[1].GetInt32());
            Assert.AreEqual(9, t[2].GetInt32());

            t = a.SelectTokens("[-1:-2:-1]").ToList();
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual(9, t[0].GetInt32());

            t = a.SelectTokens("[-2:-1]").ToList();
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual(8, t[0].GetInt32());

            t = a.SelectTokens("[1:1]").ToList();
            Assert.AreEqual(0, t.Count);

            t = a.SelectTokens("[1:2]").ToList();
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual(2, t[0].GetInt32());

            t = a.SelectTokens("[::-1]").ToList();
            Assert.AreEqual(9, t.Count);
            Assert.AreEqual(9, t[0].GetInt32());
            Assert.AreEqual(8, t[1].GetInt32());
            Assert.AreEqual(7, t[2].GetInt32());
            Assert.AreEqual(6, t[3].GetInt32());
            Assert.AreEqual(5, t[4].GetInt32());
            Assert.AreEqual(4, t[5].GetInt32());
            Assert.AreEqual(3, t[6].GetInt32());
            Assert.AreEqual(2, t[7].GetInt32());
            Assert.AreEqual(1, t[8].GetInt32());

            t = a.SelectTokens("[::-2]").ToList();
            Assert.AreEqual(5, t.Count);
            Assert.AreEqual(9, t[0].GetInt32());
            Assert.AreEqual(7, t[1].GetInt32());
            Assert.AreEqual(5, t[2].GetInt32());
            Assert.AreEqual(3, t[3].GetInt32());
            Assert.AreEqual(1, t[4].GetInt32());
        }

        [Test]
        public void EvaluateWildcardArray()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4]");

            List<JsonElement> t = a.SelectTokens("[*]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(4, t.Count);
            Assert.AreEqual(1, t[0].GetInt32());
            Assert.AreEqual(2, t[1].GetInt32());
            Assert.AreEqual(3, t[2].GetInt32());
            Assert.AreEqual(4, t[3].GetInt32());
        }

        [Test]
        public void EvaluateArrayMultipleIndexes()
        {
            var a = JsonDocument.Parse(@"[1, 2, 3, 4]");

            IEnumerable<JsonElement> t = a.SelectTokens("[1,2,0]");
            Assert.IsNotNull(t);
            Assert.AreEqual(3, t.Count());
            Assert.AreEqual(2, t.ElementAt(0).GetInt32());
            Assert.AreEqual(3, t.ElementAt(1).GetInt32());
            Assert.AreEqual(1, t.ElementAt(2).GetInt32());
        }

        [Test]
        public void EvaluateScan()
        {
            JsonDocument o1 = JsonDocument.Parse(@"{ ""Name"": 1 }");
            JsonDocument o2 = JsonDocument.Parse(@"{ ""Name"": 2 }");
            var a = JsonDocument.Parse(@"[{ ""Name"": 1 }, { ""Name"": 2 }]");

            var t = a.SelectTokens("$..Name").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual(1, t[0].GetInt32());
            Assert.AreEqual(2, t[1].GetInt32());
        }

        [Test]
        public void EvaluateWildcardScan()
        {
            JsonDocument o1 = JsonDocument.Parse(@"{ ""Name"": 1 }");
            JsonDocument o2 = JsonDocument.Parse(@"{ ""Name"": 2 }");
            var a = JsonDocument.Parse(@"[{ ""Name"": 1 }, { ""Name"": 2 }]");

            var t = a.SelectTokens("$..*").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(5, t.Count);
            Assert.IsTrue(a.DeepEquals(t[0]));
            Assert.IsTrue(o1.DeepEquals(t[1]));
            Assert.AreEqual(1, t[2].GetInt32());
            Assert.IsTrue(o2.DeepEquals(t[3]));
            Assert.AreEqual(2, t[4].GetInt32());
        }

        [Test]
        public void EvaluateScanNestResults()
        {
            JsonDocument o1 = JsonDocument.Parse(@"{ ""Name"": 1 }");
            JsonDocument o2 = JsonDocument.Parse(@"{ ""Name"": 2 }");
            JsonDocument o3 = JsonDocument.Parse(@"{ ""Name"": { ""Name"": [ 3 ] } }");
            var a = JsonDocument.Parse(@"[
                { ""Name"": 1 },
                { ""Name"": 2 },
                { ""Name"": { ""Name"": [ 3 ] } }
            ]");

            var t = a.SelectTokens("$..Name").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(4, t.Count);
            Assert.AreEqual(1, t[0].GetInt32());
            Assert.AreEqual(2, t[1].GetInt32());
            Assert.IsTrue(JsonDocument.Parse(@"{ ""Name"": [ 3 ] }").DeepEquals(t[2]));
            Assert.IsTrue(JsonDocument.Parse("[3]").DeepEquals(t[3]));
        }

        [Test]
        public void EvaluateWildcardScanNestResults()
        {
            JsonDocument o1 = JsonDocument.Parse(@"{ ""Name"": 1 }");
            JsonDocument o2 = JsonDocument.Parse(@"{ ""Name"": 2 }");
            JsonDocument o3 = JsonDocument.Parse(@"{ ""Name"": { ""Name"": [ 3 ] } }");
            var a = JsonDocument.Parse(@"[
                { ""Name"": 1 },
                { ""Name"": 2 },
                { ""Name"": { ""Name"": [ 3 ] } }
            ]");

            var t = a.SelectTokens("$..*").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(9, t.Count);

            Assert.IsTrue(a.DeepEquals(t[0]));
            Assert.IsTrue(o1.DeepEquals(t[1]));
            Assert.AreEqual(1, t[2].GetInt32());
            Assert.IsTrue(o2.DeepEquals(t[3]));
            Assert.AreEqual(2, t[4].GetInt32());
            Assert.IsTrue(o3.DeepEquals(t[5]));
            Assert.IsTrue(JsonDocument.Parse(@"{ ""Name"": [3] }").DeepEquals(t[6]));
            Assert.IsTrue(JsonDocument.Parse("[3]").DeepEquals(t[7]));
            Assert.AreEqual(3, t[8].GetInt32());
            Assert.IsTrue(JsonDocument.Parse("[3]").DeepEquals(t[7]));
        }

        [Test]
        public void EvaluateSinglePropertyReturningArray()
        {
            var o = JsonDocument.Parse(@"{ ""Blah"": [ 1, 2, 3 ] }");

            var t = o.SelectToken("Blah");
            Assert.IsNotNull(t);
            Assert.AreEqual(JsonValueKind.Array, t?.ValueKind);

            t = o.SelectToken("Blah[2]");
            Assert.AreEqual(JsonValueKind.Number, t?.ValueKind);
            Assert.AreEqual(3, t?.GetInt32());
        }

        [Test]
        public void EvaluateLastSingleCharacterProperty()
        {
            JsonDocument o2 = JsonDocument.Parse(@"{""People"":[{""N"":""Jeff""}]}");
            var a2 = o2.SelectToken("People[0].N")?.GetString();

            Assert.AreEqual("Jeff", a2);
        }

        [Test]
        public void ExistsQuery()
        {
            var a = JsonDocument.Parse(@"[
                { ""hi"": ""ho"" },
                { ""hi2"": ""ha"" }
            ]");

            var t = a.SelectTokens("[ ?( @.hi ) ]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(1, t.Count);
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": ""ho"" }").DeepEquals(t[0]));
        }

        [Test]
        public void EqualsQuery()
        {
            var a = JsonDocument.Parse(@"[
                { ""hi"": ""ho"" },
                { ""hi"": ""ha"" }
            ]");

            var t = a.SelectTokens("[ ?( @.['hi'] == 'ha' ) ]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(1, t.Count);
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": ""ha"" }").DeepEquals(t[0]));
        }

        [Test]
        public void NotEqualsQuery()
        {
            var a = JsonDocument.Parse(@"[
                { ""hi"": ""ho"" },
                { ""hi"": ""ha"" }
            ]");

            var t = a.SelectTokens("[ ?( @..hi <> 'ha' ) ]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(1, t.Count);
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": ""ho"" }").DeepEquals(t[0]));
        }

        [Test]
        public void NoPathQuery()
        {
            var a = JsonDocument.Parse("[1, 2, 3]");

            var t = a.SelectTokens("[ ?( @ > 1 ) ]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual(2, t[0].GetInt32());
            Assert.AreEqual(3, t[1].GetInt32());
        }

        [Test]
        public void MultipleQueries()
        {
            var a = JsonDocument.Parse("[1, 2, 3, 4, 5, 6, 7, 8, 9]");

            // json path does item based evaluation - http://www.sitepen.com/blog/2008/03/17/jsonpath-support/
            // first query resolves array to ints
            // int has no children to query
            var t = a.SelectTokens("[?(@ <> 1)][?(@ <> 4)][?(@ < 7)]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(0, t.Count);
        }

        [Test]
        public void GreaterQuery()
        {
            var a = JsonDocument.Parse(@"
            [
                { ""hi"": 1 },
                { ""hi"": 2 },
                { ""hi"": 3 }
            ]");

            var t = a.SelectTokens("[ ?( @.hi > 1 ) ]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(2, t.Count);
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": 2 }").DeepEquals(t[0]));
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": 3 }").DeepEquals(t[1]));
        }

        [Test]
        public void LesserQuery_ValueFirst()
        {
            var a = JsonDocument.Parse(@"
            [
                { ""hi"": 1 },
                { ""hi"": 2 },
                { ""hi"": 3 }
            ]");

            var t = a.SelectTokens("[ ?( 1 < @.hi ) ]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(2, t.Count);
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": 2 }").DeepEquals(t[0]));
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": 3 }").DeepEquals(t[1]));
        }

        [Test]
        public void GreaterOrEqualQuery()
        {
            var a = JsonDocument.Parse(@"
            [
                { ""hi"": 1 },
                { ""hi"": 2 },
                { ""hi"": 2.0 },
                { ""hi"": 3 }
            ]");

            var t = a.SelectTokens("[ ?( @.hi >= 1 ) ]").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(4, t.Count);
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": 1 }").DeepEquals(t[0]));
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": 2 }").DeepEquals(t[1]));
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": 2.0 }").DeepEquals(t[2]));
            Assert.IsTrue(JsonDocument.Parse(@"{ ""hi"": 3 }").DeepEquals(t[3]));
        }

        [Test]
        public void NestedQuery()
        {
            var a = JsonDocument.Parse(@"
            [
                {
                    ""name"": ""Bad Boys"",
                    ""cast"": [ { ""name"": ""Will Smith"" } ]
                },
                {
                    ""name"": ""Independence Day"",
                    ""cast"": [ { ""name"": ""Will Smith"" } ]
                },
                {
                    ""name"": ""The Rock"",
                    ""cast"": [ { ""name"": ""Nick Cage"" } ]
                }
            ]");

            var t = a.SelectTokens("[?(@.cast[?(@.name=='Will Smith')])].name").ToList();
            Assert.IsNotNull(t);
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual("Bad Boys", t[0].GetString());
            Assert.AreEqual("Independence Day", t[1].GetString());
        }

        [Test]
        public void MultiplePaths()
        {
            var a = JsonDocument.Parse(@"[
  {
    ""price"": 199,
    ""max_price"": 200
  },
  {
    ""price"": 200,
    ""max_price"": 200
  },
  {
    ""price"": 201,
    ""max_price"": 200
  }
]");

            var results = a.SelectTokens("[?(@.price > @.max_price)]").ToList();
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(a.RootElement[2].DeepEquals(results[0]));
        }

        [Test]
        public void Exists_True()
        {
            var a = JsonDocument.Parse(@"[
  {
    ""price"": 199,
    ""max_price"": 200
  },
  {
    ""price"": 200,
    ""max_price"": 200
  },
  {
    ""price"": 201,
    ""max_price"": 200
  }
]");

            var results = a.SelectTokens("[?(true)]").ToList();
            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(a.RootElement[0].DeepEquals(results[0]));
            Assert.IsTrue(a.RootElement[1].DeepEquals(results[1]));
            Assert.IsTrue(a.RootElement[2].DeepEquals(results[2]));
        }

        [Test]
        public void Exists_Null()
        {
            var a = JsonDocument.Parse(@"[
  {
    ""price"": 199,
    ""max_price"": 200
  },
  {
    ""price"": 200,
    ""max_price"": 200
  },
  {
    ""price"": 201,
    ""max_price"": 200
  }
]");

            var results = a.SelectTokens("[?(true)]").ToList();
            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(a.RootElement[0].DeepEquals(results[0]));
            Assert.IsTrue(a.RootElement[1].DeepEquals(results[1]));
            Assert.IsTrue(a.RootElement[2].DeepEquals(results[2]));
        }

        [Test]
        public void WildcardWithProperty()
        {
            var o = JsonDocument.Parse(@"{
    ""station"": 92000041000001,
    ""containers"": [
        {
            ""id"": 1,
            ""text"": ""Sort system"",
            ""containers"": [
                {
                    ""id"": ""2"",
                    ""text"": ""Yard 11""
                },
                {
                    ""id"": ""92000020100006"",
                    ""text"": ""Sort yard 12""
                },
                {
                    ""id"": ""92000020100005"",
                    ""text"": ""Yard 13""
                }
            ]
        },
        {
            ""id"": ""92000020100011"",
            ""text"": ""TSP-1""
        },
        {
            ""id"":""92000020100007"",
            ""text"": ""Passenger 15""
        }
    ]
}");

            var tokens = o.SelectTokens("$..*[?(@.text)]").ToList();
            int i = 0;
            Assert.AreEqual("Sort system", tokens[i++].GetProperty("text").GetString());
            Assert.AreEqual("TSP-1", tokens[i++].GetProperty("text").GetString());
            Assert.AreEqual("Passenger 15", tokens[i++].GetProperty("text").GetString());
            Assert.AreEqual("Yard 11", tokens[i++].GetProperty("text").GetString());
            Assert.AreEqual("Sort yard 12", tokens[i++].GetProperty("text").GetString());
            Assert.AreEqual("Yard 13", tokens[i++].GetProperty("text").GetString());
            Assert.AreEqual(6, tokens.Count);
        }

        [Test]
        public void QueryAgainstNonStringValues()
        {
            IList<object> values = new List<object>
            {
                "ff2dc672-6e15-4aa2-afb0-18f4f69596ad",
                new Guid("ff2dc672-6e15-4aa2-afb0-18f4f69596ad"),
                "http://localhost",
                // new Uri("http://localhost"),
                "2000-12-05T05:07:59Z",
                new DateTime(2000, 12, 5, 5, 7, 59, DateTimeKind.Utc),
                "2000-12-05T05:07:59-10:00",
                // new DateTimeOffset(2000, 12, 5, 5, 7, 59, -TimeSpan.FromHours(10)),
                "SGVsbG8gd29ybGQ=",
                Encoding.UTF8.GetBytes("Hello world"),
                "365.23:59:59"
                // new TimeSpan(365, 23, 59, 59)
            };

            var o = JsonDocument.Parse(@"{
                ""prop"": [ " + 
                    String.Join(", ", values.Select(v => $"{{\"childProp\": {JsonSerializer.Serialize(v)}}}")) +
                @"]
            }");

            var t = o.SelectTokens("$.prop[?(@.childProp =='ff2dc672-6e15-4aa2-afb0-18f4f69596ad')]").ToList();
            Assert.AreEqual(2, t.Count);

            t = o.SelectTokens("$.prop[?(@.childProp =='http://localhost')]").ToList();
            Assert.AreEqual(1, t.Count);

            t = o.SelectTokens("$.prop[?(@.childProp =='2000-12-05T05:07:59Z')]").ToList();
            Assert.AreEqual(2, t.Count);

            t = o.SelectTokens("$.prop[?(@.childProp =='2000-12-05T05:07:59-10:00')]").ToList();
            Assert.AreEqual(1, t.Count);

            t = o.SelectTokens("$.prop[?(@.childProp =='SGVsbG8gd29ybGQ=')]").ToList();
            Assert.AreEqual(2, t.Count);

            t = o.SelectTokens("$.prop[?(@.childProp =='365.23:59:59')]").ToList();
            Assert.AreEqual(1, t.Count);
        }

        [Test]
        public void Example()
        {
            var o = JsonDocument.Parse(@"{
        ""Stores"": [
          ""Lambton Quay"",
          ""Willis Street""
        ],
        ""Manufacturers"": [
          {
            ""Name"": ""Acme Co"",
            ""Products"": [
              {
                ""Name"": ""Anvil"",
                ""Price"": 50
              }
            ]
          },
          {
            ""Name"": ""Contoso"",
            ""Products"": [
              {
                ""Name"": ""Elbow Grease"",
                ""Price"": 99.95
              },
              {
                ""Name"": ""Headlight Fluid"",
                ""Price"": 4
              }
            ]
          }
        ]
      }");

            string? name = o.SelectToken("Manufacturers[0].Name")?.GetString();
            // Acme Co

            decimal? productPrice = o.SelectToken("Manufacturers[0].Products[0].Price")?.GetDecimal();
            // 50

            string? productName = o.SelectToken("Manufacturers[1].Products[0].Name")?.GetString();
            // Elbow Grease

            Assert.AreEqual("Acme Co", name);
            Assert.AreEqual(50m, productPrice);
            Assert.AreEqual("Elbow Grease", productName);

            IList<string> storeNames = o.SelectToken("Stores")!.Value.EnumerateArray().Select(s => s.GetString()).ToList();
            // Lambton Quay
            // Willis Street

            IList<string?> firstProductNames = o.RootElement.GetProperty("Manufacturers")!.EnumerateArray().Select(
                m => m.SelectToken("Products[1].Name")?.GetString()).ToList();
            // null
            // Headlight Fluid

            decimal totalPrice = o.RootElement.GetProperty("Manufacturers")!.EnumerateArray().Aggregate(
                0M, (sum, m) => sum + m.SelectToken("Products[0].Price")!.Value.GetDecimal());
            // 149.95

            Assert.AreEqual(2, storeNames.Count);
            Assert.AreEqual("Lambton Quay", storeNames[0]);
            Assert.AreEqual("Willis Street", storeNames[1]);
            Assert.AreEqual(2, firstProductNames.Count);
            Assert.AreEqual(null, firstProductNames[0]);
            Assert.AreEqual("Headlight Fluid", firstProductNames[1]);
            Assert.AreEqual(149.95m, totalPrice);
        }

        [Test]
        public void NotEqualsAndNonPrimativeValues()
        {
            string json = @"[
  {
    ""name"": ""string"",
    ""value"": ""aString""
  },
  {
    ""name"": ""number"",
    ""value"": 123
  },
  {
    ""name"": ""array"",
    ""value"": [
      1,
      2,
      3,
      4
    ]
  },
  {
    ""name"": ""object"",
    ""value"": {
      ""1"": 1
    }
  }
]";

            var a = JsonDocument.Parse(json);

            var result = a.SelectTokens("$.[?(@.value!=1)]").ToList();
            Assert.AreEqual(4, result.Count);

            result = a.SelectTokens("$.[?(@.value!='2000-12-05T05:07:59-10:00')]").ToList();
            Assert.AreEqual(4, result.Count);

            result = a.SelectTokens("$.[?(@.value!=null)]").ToList();
            Assert.AreEqual(4, result.Count);

            result = a.SelectTokens("$.[?(@.value!=123)]").ToList();
            Assert.AreEqual(3, result.Count);

            result = a.SelectTokens("$.[?(@.value)]").ToList();
            Assert.AreEqual(4, result.Count);
        }

        [Test]
        public void RootInFilter()
        {
            string json = @"[
   {
      ""store"" : {
         ""book"" : [
            {
               ""category"" : ""reference"",
               ""author"" : ""Nigel Rees"",
               ""title"" : ""Sayings of the Century"",
               ""price"" : 8.95
            },
            {
               ""category"" : ""fiction"",
               ""author"" : ""Evelyn Waugh"",
               ""title"" : ""Sword of Honour"",
               ""price"" : 12.99
            },
            {
               ""category"" : ""fiction"",
               ""author"" : ""Herman Melville"",
               ""title"" : ""Moby Dick"",
               ""isbn"" : ""0-553-21311-3"",
               ""price"" : 8.99
            },
            {
               ""category"" : ""fiction"",
               ""author"" : ""J. R. R. Tolkien"",
               ""title"" : ""The Lord of the Rings"",
               ""isbn"" : ""0-395-19395-8"",
               ""price"" : 22.99
            }
         ],
         ""bicycle"" : {
            ""color"" : ""red"",
            ""price"" : 19.95
         }
      },
      ""expensive"" : 10
   }
]";

            var a = JsonDocument.Parse(json);

            var result = a.SelectTokens("$.[?($.[0].store.bicycle.price < 20)]").ToList();
            Assert.AreEqual(1, result.Count);

            result = a.SelectTokens("$.[?($.[0].store.bicycle.price < 10)]").ToList();
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void RootInFilterWithRootObject()
        {
            string json = @"{
                ""store"" : {
                    ""book"" : [
                        {
                            ""category"" : ""reference"",
                            ""author"" : ""Nigel Rees"",
                            ""title"" : ""Sayings of the Century"",
                            ""price"" : 8.95
                        },
                        {
                            ""category"" : ""fiction"",
                            ""author"" : ""Evelyn Waugh"",
                            ""title"" : ""Sword of Honour"",
                            ""price"" : 12.99
                        },
                        {
                            ""category"" : ""fiction"",
                            ""author"" : ""Herman Melville"",
                            ""title"" : ""Moby Dick"",
                            ""isbn"" : ""0-553-21311-3"",
                            ""price"" : 8.99
                        },
                        {
                            ""category"" : ""fiction"",
                            ""author"" : ""J. R. R. Tolkien"",
                            ""title"" : ""The Lord of the Rings"",
                            ""isbn"" : ""0-395-19395-8"",
                            ""price"" : 22.99
                        }
                    ],
                    ""bicycle"" : [
                        {
                            ""color"" : ""red"",
                            ""price"" : 19.95
                        }
                    ]
                },
                ""expensive"" : 10
            }";

            JsonDocument a = JsonDocument.Parse(json);

            var result = a.SelectTokens("$..book[?(@.price <= $['expensive'])]").ToList();
            Assert.AreEqual(2, result.Count);

            result = a.SelectTokens("$.store..[?(@.price > $.expensive)]").ToList();
            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void RootInFilterWithInitializers()
        {
            var minDate = DateTime.MinValue.ToString(IsoDateFormat);

            JsonDocument rootObject = JsonDocument.Parse(@"
            {
                ""referenceDate"": """ + minDate + @""",
                ""dateObjectsArray"": [
                    { ""date"": """ + minDate + @""" },
                    { ""date"": """ + DateTime.MaxValue.ToString(IsoDateFormat) + @""" },
                    { ""date"": """ + DateTime.Now.ToString(IsoDateFormat) + @""" },
                    { ""date"": """ + minDate + @""" }
                ]
            }");

            var result = rootObject.SelectTokens("$.dateObjectsArray[?(@.date == $.referenceDate)]").ToList();
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void IdentityOperator()
        {
            var o = JsonDocument.Parse(@"{
	            ""Values"": [{

                    ""Coercible"": 1,
                    ""Name"": ""Number""

                }, {
		            ""Coercible"": ""1"",
		            ""Name"": ""String""
	            }]
            }");

            // just to verify expected behavior hasn't changed
            IEnumerable<string> sanity1 = o.SelectTokens("Values[?(@.Coercible == '1')].Name").Select(x => x.GetString());
            IEnumerable<string> sanity2 = o.SelectTokens("Values[?(@.Coercible != '1')].Name").Select(x => x.GetString());
            // new behavior
            IEnumerable<string> mustBeNumber1 = o.SelectTokens("Values[?(@.Coercible === 1)].Name").Select(x => x.GetString());
            IEnumerable<string> mustBeString1 = o.SelectTokens("Values[?(@.Coercible !== 1)].Name").Select(x => x.GetString());
            IEnumerable<string> mustBeString2 = o.SelectTokens("Values[?(@.Coercible === '1')].Name").Select(x => x.GetString());
            IEnumerable<string> mustBeNumber2 = o.SelectTokens("Values[?(@.Coercible !== '1')].Name").Select(x => x.GetString());

            // FAILS-- JPath returns { "String" }
            //CollectionAssert.AreEquivalent(new[] { "Number", "String" }, sanity1);
            // FAILS-- JPath returns { "Number" }
            //Assert.IsTrue(!sanity2.Any());
            Assert.AreEqual("Number", mustBeNumber1.Single());
            Assert.AreEqual("String", mustBeString1.Single());
            Assert.AreEqual("Number", mustBeNumber2.Single());
            Assert.AreEqual("String", mustBeString2.Single());
        }

        [Test]
        public void QueryWithEscapedPath()
        {
            var t = JsonDocument.Parse(@"{
""Property"": [
          {
            ""@Name"": ""x"",
            ""@Value"": ""y"",
            ""@Type"": ""FindMe""
          }
   ]
}");

            var tokens = t.SelectTokens("$..[?(@.['@Type'] == 'FindMe')]").ToList();
            Assert.AreEqual(1, tokens.Count);
        }

        [Test]
        public void Equals_FloatWithInt()
        {
            var t = JsonDocument.Parse(@"{
  ""Values"": [
    {
      ""Property"": 1
    }
  ]
}");

            Assert.IsNotNull(t.SelectToken(@"Values[?(@.Property == 1.0)]"));
        }

#if DNXCORE50
        [Theory]
#endif
        [TestCaseSource(nameof(StrictMatchWithInverseTestData))]
        public static void EqualsStrict(string value1, string value2, bool matchStrict)
        {
            if (value1[0] == '\'' && value1[value1.Length - 1] == '\'')
            {
                value1 = '"' + value1.Substring(1, value1.Length - 2) + '"';
            }

            string completeJson = @"{
  ""Values"": [
    {
      ""Property"": " + value1 + @"
    }
  ]
}";
            string completeEqualsStrictPath = "$.Values[?(@.Property === " + value2 + ")]";
            string completeNotEqualsStrictPath = "$.Values[?(@.Property !== " + value2 + ")]";

            var t = JsonDocument.Parse(completeJson);

            bool hasEqualsStrict = t.SelectTokens(completeEqualsStrictPath).Any();
            Assert.AreEqual(
                matchStrict,
                hasEqualsStrict,
                $"Expected {value1} and {value2} to match: {matchStrict}"
                + Environment.NewLine + completeJson + Environment.NewLine + completeEqualsStrictPath);

            bool hasNotEqualsStrict = t.SelectTokens(completeNotEqualsStrictPath).Any();
            Assert.AreNotEqual(
                matchStrict,
                hasNotEqualsStrict,
                $"Expected {value1} and {value2} to match: {!matchStrict}"
                + Environment.NewLine + completeJson + Environment.NewLine + completeEqualsStrictPath);
        }

        public static IEnumerable<object[]> StrictMatchWithInverseTestData()
        {
            foreach (var item in StrictMatchTestData())
            {
                yield return new object[] { item[0], item[1], item[2] };

                if (!item[0].Equals(item[1]))
                {
                    // Test the inverse
                    yield return new object[] { item[1], item[0], item[2] };
                }
            }
        }

        private static IEnumerable<object[]> StrictMatchTestData()
        {
            yield return new object[] { "1", "1", true };
            yield return new object[] { "1", "1.0", true };
            yield return new object[] { "1", "true", false };
            yield return new object[] { "1", "'1'", false };
            yield return new object[] { "'1'", "'1'", true };
            yield return new object[] { "false", "false", true };
            yield return new object[] { "true", "false", false };
            yield return new object[] { "1", "1.1", false };
            yield return new object[] { "1", "null", false };
            yield return new object[] { "null", "null", true };
            yield return new object[] { "null", "'null'", false };
        }
    }
}

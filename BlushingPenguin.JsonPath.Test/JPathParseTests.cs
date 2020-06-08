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
using System.Collections.Generic;
using System.Text.Json;

namespace BlushingPenguin.JsonPath.Test
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class JPathParseTests
    {
        [Test]
        public void BooleanQuery_TwoValues()
        {
            var path = new JPath("[?(1 > 2)]");
            Assert.AreEqual(1, path.Filters.Count);
            BooleanQueryExpression booleanExpression = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(1, ((JsonElement)booleanExpression.Left!).GetInt32());
            Assert.AreEqual(2, ((JsonElement)booleanExpression.Right!).GetInt32());
            Assert.AreEqual(QueryOperator.GreaterThan, booleanExpression.Operator);
        }

        [Test]
        public void BooleanQuery_TwoPaths()
        {
            var path = new JPath("[?(@.price > @.max_price)]");
            Assert.AreEqual(1, path.Filters.Count);
            BooleanQueryExpression booleanExpression = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            List<PathFilter> leftPaths = (List<PathFilter>)booleanExpression.Left;
            List<PathFilter> rightPaths = (List<PathFilter>)booleanExpression.Right!;

            Assert.AreEqual("price", ((FieldFilter)leftPaths[0]).Name);
            Assert.AreEqual("max_price", ((FieldFilter)rightPaths[0]).Name);
            Assert.AreEqual(QueryOperator.GreaterThan, booleanExpression.Operator);
        }

        [Test]
        public void SingleProperty()
        {
            var path = new JPath("Blah");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void SingleQuotedProperty()
        {
            var path = new JPath("['Blah']");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void SingleQuotedPropertyWithWhitespace()
        {
            var path = new JPath("[  'Blah'  ]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void SingleQuotedPropertyWithDots()
        {
            var path = new JPath("['Blah.Ha']");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("Blah.Ha", ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void SingleQuotedPropertyWithBrackets()
        {
            var path = new JPath("['[*]']");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("[*]", ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void SinglePropertyWithRoot()
        {
            var path = new JPath("$.Blah");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void SinglePropertyWithRootWithStartAndEndWhitespace()
        {
            var path = new JPath(" $.Blah ");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void RootWithBadWhitespace()
        {
            Assert.Throws<JsonException>(() => { new JPath("$ .Blah"); }, @"Unexpected character while parsing path:  ");
        }

        [Test]
        public void NoFieldNameAfterDot()
        {
            Assert.Throws<JsonException>(() => { new JPath("$.Blah."); }, @"Unexpected end while parsing path.");
        }

        [Test]
        public void RootWithBadWhitespace2()
        {
            Assert.Throws<JsonException>(() => { new JPath("$. Blah"); }, @"Unexpected character while parsing path:  ");
        }

        [Test]
        public void WildcardPropertyWithRoot()
        {
            var path = new JPath("$.*");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(null, ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void WildcardArrayWithRoot()
        {
            var path = new JPath("$.[*]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(null, ((ArrayIndexFilter)path.Filters[0]).Index);
        }

        [Test]
        public void RootArrayNoDot()
        {
            var path = new JPath("$[1]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(1, ((ArrayIndexFilter)path.Filters[0]).Index);
        }

        [Test]
        public void WildcardArray()
        {
            var path = new JPath("[*]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(null, ((ArrayIndexFilter)path.Filters[0]).Index);
        }

        [Test]
        public void WildcardArrayWithProperty()
        {
            var path = new JPath("[ * ].derp");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual(null, ((ArrayIndexFilter)path.Filters[0]).Index);
            Assert.AreEqual("derp", ((FieldFilter)path.Filters[1]).Name);
        }

        [Test]
        public void QuotedWildcardPropertyWithRoot()
        {
            var path = new JPath("$.['*']");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("*", ((FieldFilter)path.Filters[0]).Name);
        }

        [Test]
        public void SingleScanWithRoot()
        {
            var path = new JPath("$..Blah");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual("Blah", ((ScanFilter)path.Filters[0]).Name);
        }

        [Test]
        public void QueryTrue()
        {
            var path = new JPath("$.elements[?(true)]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("elements", ((FieldFilter)path.Filters[0]).Name);
            Assert.AreEqual(QueryOperator.Exists, ((QueryFilter)path.Filters[1]).Expression.Operator);
        }

        [Test]
        public void ScanQuery()
        {
            var path = new JPath("$.elements..[?(@.id=='AAA')]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("elements", ((FieldFilter)path.Filters[0]).Name);

            BooleanQueryExpression expression = (BooleanQueryExpression)((QueryScanFilter) path.Filters[1]).Expression;

            List<PathFilter> paths = (List<PathFilter>)expression.Left;

            Assert.IsInstanceOf(typeof(FieldFilter), paths[0]);
        }

        [Test]
        public void WildcardScanWithRoot()
        {
            var path = new JPath("$..*");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(null, ((ScanFilter)path.Filters[0]).Name);
        }

        [Test]
        public void WildcardScanWithRootWithWhitespace()
        {
            var path = new JPath("$..* ");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(null, ((ScanFilter)path.Filters[0]).Name);
        }

        [Test]
        public void TwoProperties()
        {
            var path = new JPath("Blah.Two");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            Assert.AreEqual("Two", ((FieldFilter)path.Filters[1]).Name);
        }

        [Test]
        public void OnePropertyOneScan()
        {
            var path = new JPath("Blah..Two");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            Assert.AreEqual("Two", ((ScanFilter)path.Filters[1]).Name);
        }

        [Test]
        public void SinglePropertyAndIndexer()
        {
            var path = new JPath("Blah[0]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            Assert.AreEqual(0, ((ArrayIndexFilter)path.Filters[1]).Index);
        }

        [Test]
        public void SinglePropertyAndExistsQuery()
        {
            var path = new JPath("Blah[ ?( @..name ) ]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.Exists, expressions.Operator);
            List<PathFilter> paths = (List<PathFilter>)expressions.Left;
            Assert.AreEqual(1, paths.Count);
            Assert.AreEqual("name", ((ScanFilter)paths[0]).Name);
        }

        [Test]
        public void SinglePropertyAndFilterWithWhitespace()
        {
            var path = new JPath("Blah[ ?( @.name=='hi' ) ]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.Equals, expressions.Operator);
            Assert.AreEqual("hi", ((JsonElement)expressions.Right!).ToString());
        }

        [Test]
        public void SinglePropertyAndFilterWithEscapeQuote()
        {
            var path = new JPath(@"Blah[ ?( @.name=='h\'i' ) ]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.Equals, expressions.Operator);
            Assert.AreEqual("h'i", ((JsonElement)expressions.Right!).ToString());
        }

        [Test]
        public void SinglePropertyAndFilterWithDoubleEscape()
        {
            var path = new JPath(@"Blah[ ?( @.name=='h\\i' ) ]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.Equals, expressions.Operator);
            Assert.AreEqual("h\\i", ((JsonElement)expressions.Right!).ToString());
        }

        [Test]
        public void SinglePropertyAndFilterWithRegexAndOptions()
        {
            var path = new JPath("Blah[ ?( @.name=~/hi/i ) ]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.RegexEquals, expressions.Operator);
            Assert.AreEqual("/hi/i", ((JsonElement)expressions.Right!).ToString());
        }

        [Test]
        public void SinglePropertyAndFilterWithRegex()
        {
            var path = new JPath("Blah[?(@.title =~ /^.*Sword.*$/)]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.RegexEquals, expressions.Operator);
            Assert.AreEqual("/^.*Sword.*$/", ((JsonElement)expressions.Right!).ToString());
        }

        [Test]
        public void SinglePropertyAndFilterWithEscapedRegex()
        {
            var path = new JPath(@"Blah[?(@.title =~ /[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g)]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.RegexEquals, expressions.Operator);
            Assert.AreEqual(@"/[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g", ((JsonElement)expressions.Right!).ToString());
        }

        [Test]
        public void SinglePropertyAndFilterWithOpenRegex()
        {
            Assert.Throws<JsonException>(() => { new JPath(@"Blah[?(@.title =~ /[\"); }, "Path ended with an open regex.");
        }

        [Test]
        public void SinglePropertyAndFilterWithUnknownEscape()
        {
            Assert.Throws<JsonException>(() => { new JPath(@"Blah[ ?( @.name=='h\i' ) ]"); }, @"Unknown escape character: \i");
        }

        [Test]
        public void SinglePropertyAndFilterWithFalse()
        {
            var path = new JPath("Blah[ ?( @.name==false ) ]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.Equals, expressions.Operator);
            Assert.AreEqual(false, ((JsonElement)expressions.Right!).GetBoolean());
        }

        [Test]
        public void SinglePropertyAndFilterWithTrue()
        {
            var path = new JPath("Blah[ ?( @.name==true ) ]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.Equals, expressions.Operator);
            Assert.AreEqual(true, ((JsonElement)expressions.Right!).GetBoolean());
        }

        [Test]
        public void SinglePropertyAndFilterWithNull()
        {
            var path = new JPath("Blah[ ?( @.name==null ) ]");
            Assert.AreEqual(2, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[1]).Expression;
            Assert.AreEqual(QueryOperator.Equals, expressions.Operator);
            Assert.IsTrue(((JsonElement)expressions.Right!).ValueKind == JsonValueKind.Null);
        }

        [Test]
        public void FilterWithScan()
        {
            var path = new JPath("[?(@..name<>null)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            List<PathFilter> paths = (List<PathFilter>)expressions.Left;
            Assert.AreEqual("name", ((ScanFilter)paths[0]).Name);
        }

        [Test]
        public void FilterWithNotEquals()
        {
            var path = new JPath("[?(@.name<>null)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(QueryOperator.NotEquals, expressions.Operator);
        }

        [Test]
        public void FilterWithNotEquals2()
        {
            var path = new JPath("[?(@.name!=null)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(QueryOperator.NotEquals, expressions.Operator);
        }

        [Test]
        public void FilterWithLessThan()
        {
            var path = new JPath("[?(@.name<null)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(QueryOperator.LessThan, expressions.Operator);
        }

        [Test]
        public void FilterWithLessThanOrEquals()
        {
            var path = new JPath("[?(@.name<=null)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(QueryOperator.LessThanOrEquals, expressions.Operator);
        }

        [Test]
        public void FilterWithGreaterThan()
        {
            var path = new JPath("[?(@.name>null)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(QueryOperator.GreaterThan, expressions.Operator);
        }

        [Test]
        public void FilterWithGreaterThanOrEquals()
        {
            var path = new JPath("[?(@.name>=null)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(QueryOperator.GreaterThanOrEquals, expressions.Operator);
        }

        [Test]
        public void FilterWithInteger()
        {
            var path = new JPath("[?(@.name>=12)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(12, ((JsonElement)expressions.Right!).GetInt32());
        }

        [Test]
        public void FilterWithNegativeInteger()
        {
            var path = new JPath("[?(@.name>=-12)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(-12, ((JsonElement)expressions.Right!).GetInt32());
        }

        [Test]
        public void FilterWithFloat()
        {
            var path = new JPath("[?(@.name>=12.1)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(12.1d, ((JsonElement)expressions.Right!).GetDouble());
        }

        [Test]
        public void FilterExistWithAnd()
        {
            var path = new JPath("[?(@.name&&@.title)]");
            CompositeExpression expressions = (CompositeExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(QueryOperator.And, expressions.Operator);
            Assert.AreEqual(2, expressions.Expressions.Count);

            var first = (BooleanQueryExpression)expressions.Expressions[0];
            var firstPaths = (List<PathFilter>)first.Left;
            Assert.AreEqual("name", ((FieldFilter)firstPaths[0]).Name);
            Assert.AreEqual(QueryOperator.Exists, first.Operator);

            var second = (BooleanQueryExpression)expressions.Expressions[1];
            var secondPaths = (List<PathFilter>)second.Left;
            Assert.AreEqual("title", ((FieldFilter)secondPaths[0]).Name);
            Assert.AreEqual(QueryOperator.Exists, second.Operator);
        }

        [Test]
        public void FilterExistWithAndOr()
        {
            var path = new JPath("[?(@.name&&@.title||@.pie)]");
            CompositeExpression andExpression = (CompositeExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(QueryOperator.And, andExpression.Operator);
            Assert.AreEqual(2, andExpression.Expressions.Count);

            var first = (BooleanQueryExpression)andExpression.Expressions[0];
            var firstPaths = (List<PathFilter>)first.Left;
            Assert.AreEqual("name", ((FieldFilter)firstPaths[0]).Name);
            Assert.AreEqual(QueryOperator.Exists, first.Operator);

            CompositeExpression orExpression = (CompositeExpression)andExpression.Expressions[1];
            Assert.AreEqual(2, orExpression.Expressions.Count);

            var orFirst = (BooleanQueryExpression)orExpression.Expressions[0];
            var orFirstPaths = (List<PathFilter>)orFirst.Left;
            Assert.AreEqual("title", ((FieldFilter)orFirstPaths[0]).Name);
            Assert.AreEqual(QueryOperator.Exists, orFirst.Operator);

            var orSecond = (BooleanQueryExpression)orExpression.Expressions[1];
            var orSecondPaths = (List<PathFilter>)orSecond.Left;
            Assert.AreEqual("pie", ((FieldFilter)orSecondPaths[0]).Name);
            Assert.AreEqual(QueryOperator.Exists, orSecond.Operator);
        }

        [Test]
        public void FilterWithRoot()
        {
            var path = new JPath("[?($.name>=12.1)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            List<PathFilter> paths = (List<PathFilter>)expressions.Left;
            Assert.AreEqual(2, paths.Count);
            Assert.IsInstanceOf(typeof(RootFilter), paths[0]);
            Assert.IsInstanceOf(typeof(FieldFilter), paths[1]);
        }

        [Test]
        public void BadOr1()
        {
            Assert.Throws<JsonException>(() => new JPath("[?(@.name||)]"), "Unexpected character while parsing path query: )");
        }

        [Test]
        public void BaddOr2()
        {
            Assert.Throws<JsonException>(() => new JPath("[?(@.name|)]"), "Unexpected character while parsing path query: |");
        }

        [Test]
        public void BaddOr3()
        {
            Assert.Throws<JsonException>(() => new JPath("[?(@.name|"), "Unexpected character while parsing path query: |");
        }

        [Test]
        public void BaddOr4()
        {
            Assert.Throws<JsonException>(() => new JPath("[?(@.name||"), "Path ended with open query.");
        }

        [Test]
        public void NoAtAfterOr()
        {
            Assert.Throws<JsonException>(() => new JPath("[?(@.name||s"), "Unexpected character while parsing path query: s");
        }

        [Test]
        public void NoPathAfterAt()
        {
            Assert.Throws<JsonException>(() => new JPath("[?(@.name||@"), @"Path ended with open query.");
        }

        [Test]
        public void NoPathAfterDot()
        {
            Assert.Throws<JsonException>(() => new JPath("[?(@.name||@."), @"Unexpected end while parsing path.");
        }

        [Test]
        public void NoPathAfterDot2()
        {
            Assert.Throws<JsonException>(() => new JPath("[?(@.name||@.)]"), @"Unexpected end while parsing path.");
        }

        [Test]
        public void FilterWithFloatExp()
        {
            var path = new JPath("[?(@.name>=5.56789e+0)]");
            BooleanQueryExpression expressions = (BooleanQueryExpression)((QueryFilter)path.Filters[0]).Expression;
            Assert.AreEqual(5.56789e+0, ((JsonElement)expressions.Right!).GetDouble());
        }

        [Test]
        public void MultiplePropertiesAndIndexers()
        {
            var path = new JPath("Blah[0]..Two.Three[1].Four");
            Assert.AreEqual(6, path.Filters.Count);
            Assert.AreEqual("Blah", ((FieldFilter)path.Filters[0]).Name);
            Assert.AreEqual(0, ((ArrayIndexFilter)path.Filters[1]).Index);
            Assert.AreEqual("Two", ((ScanFilter)path.Filters[2]).Name);
            Assert.AreEqual("Three", ((FieldFilter)path.Filters[3]).Name);
            Assert.AreEqual(1, ((ArrayIndexFilter)path.Filters[4]).Index);
            Assert.AreEqual("Four", ((FieldFilter)path.Filters[5]).Name);
        }

        [Test]
        public void BadCharactersInIndexer()
        {
            Assert.Throws<JsonException>(() => { new JPath("Blah[[0]].Two.Three[1].Four"); }, @"Unexpected character while parsing path indexer: [");
        }

        [Test]
        public void UnclosedIndexer()
        {
            Assert.Throws<JsonException>(() => { new JPath("Blah[0"); }, @"Path ended with open indexer.");
        }

        [Test]
        public void IndexerOnly()
        {
            var path = new JPath("[111119990]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(111119990, ((ArrayIndexFilter)path.Filters[0]).Index);
        }

        [Test]
        public void IndexerOnlyWithWhitespace()
        {
            var path = new JPath("[  10  ]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(10, ((ArrayIndexFilter)path.Filters[0]).Index);
        }

        [Test]
        public void MultipleIndexes()
        {
            var path = new JPath("[111119990,3]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(2, ((ArrayMultipleIndexFilter)path.Filters[0]).Indexes.Count);
            Assert.AreEqual(111119990, ((ArrayMultipleIndexFilter)path.Filters[0]).Indexes[0]);
            Assert.AreEqual(3, ((ArrayMultipleIndexFilter)path.Filters[0]).Indexes[1]);
        }

        [Test]
        public void MultipleIndexesWithWhitespace()
        {
            var path = new JPath("[   111119990  ,   3   ]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(2, ((ArrayMultipleIndexFilter)path.Filters[0]).Indexes.Count);
            Assert.AreEqual(111119990, ((ArrayMultipleIndexFilter)path.Filters[0]).Indexes[0]);
            Assert.AreEqual(3, ((ArrayMultipleIndexFilter)path.Filters[0]).Indexes[1]);
        }

        [Test]
        public void MultipleQuotedIndexes()
        {
            var path = new JPath("['111119990','3']");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(2, ((FieldMultipleFilter)path.Filters[0]).Names.Count);
            Assert.AreEqual("111119990", ((FieldMultipleFilter)path.Filters[0]).Names[0]);
            Assert.AreEqual("3", ((FieldMultipleFilter)path.Filters[0]).Names[1]);
        }

        [Test]
        public void MultipleQuotedIndexesWithWhitespace()
        {
            var path = new JPath("[ '111119990' , '3' ]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(2, ((FieldMultipleFilter)path.Filters[0]).Names.Count);
            Assert.AreEqual("111119990", ((FieldMultipleFilter)path.Filters[0]).Names[0]);
            Assert.AreEqual("3", ((FieldMultipleFilter)path.Filters[0]).Names[1]);
        }

        [Test]
        public void SlicingIndexAll()
        {
            var path = new JPath("[111119990:3:2]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(111119990, ((ArraySliceFilter)path.Filters[0]).Start);
            Assert.AreEqual(3, ((ArraySliceFilter)path.Filters[0]).End);
            Assert.AreEqual(2, ((ArraySliceFilter)path.Filters[0]).Step);
        }

        [Test]
        public void SlicingIndex()
        {
            var path = new JPath("[111119990:3]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(111119990, ((ArraySliceFilter)path.Filters[0]).Start);
            Assert.AreEqual(3, ((ArraySliceFilter)path.Filters[0]).End);
            Assert.AreEqual(null, ((ArraySliceFilter)path.Filters[0]).Step);
        }

        [Test]
        public void SlicingIndexNegative()
        {
            var path = new JPath("[-111119990:-3:-2]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(-111119990, ((ArraySliceFilter)path.Filters[0]).Start);
            Assert.AreEqual(-3, ((ArraySliceFilter)path.Filters[0]).End);
            Assert.AreEqual(-2, ((ArraySliceFilter)path.Filters[0]).Step);
        }

        [Test]
        public void SlicingIndexEmptyStop()
        {
            var path = new JPath("[  -3  :  ]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(-3, ((ArraySliceFilter)path.Filters[0]).Start);
            Assert.AreEqual(null, ((ArraySliceFilter)path.Filters[0]).End);
            Assert.AreEqual(null, ((ArraySliceFilter)path.Filters[0]).Step);
        }

        [Test]
        public void SlicingIndexEmptyStart()
        {
            var path = new JPath("[ : 1 : ]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(null, ((ArraySliceFilter)path.Filters[0]).Start);
            Assert.AreEqual(1, ((ArraySliceFilter)path.Filters[0]).End);
            Assert.AreEqual(null, ((ArraySliceFilter)path.Filters[0]).Step);
        }

        [Test]
        public void SlicingIndexWhitespace()
        {
            var path = new JPath("[  -111119990  :  -3  :  -2  ]");
            Assert.AreEqual(1, path.Filters.Count);
            Assert.AreEqual(-111119990, ((ArraySliceFilter)path.Filters[0]).Start);
            Assert.AreEqual(-3, ((ArraySliceFilter)path.Filters[0]).End);
            Assert.AreEqual(-2, ((ArraySliceFilter)path.Filters[0]).Step);
        }

        [Test]
        public void EmptyIndexer()
        {
            Assert.Throws<JsonException>(() => { new JPath("[]"); }, "Array index expected.");
        }

        [Test]
        public void IndexerCloseInProperty()
        {
            Assert.Throws<JsonException>(() => { new JPath("]"); }, "Unexpected character while parsing path: ]");
        }

        [Test]
        public void AdjacentIndexers()
        {
            var path = new JPath("[1][0][0][" + int.MaxValue + "]");
            Assert.AreEqual(4, path.Filters.Count);
            Assert.AreEqual(1, ((ArrayIndexFilter)path.Filters[0]).Index);
            Assert.AreEqual(0, ((ArrayIndexFilter)path.Filters[1]).Index);
            Assert.AreEqual(0, ((ArrayIndexFilter)path.Filters[2]).Index);
            Assert.AreEqual(int.MaxValue, ((ArrayIndexFilter)path.Filters[3]).Index);
        }

        [Test]
        public void MissingDotAfterIndexer()
        {
            Assert.Throws<JsonException>(() => { new JPath("[1]Blah"); }, "Unexpected character following indexer: B");
        }

        [Test]
        public void PropertyFollowingEscapedPropertyName()
        {
            var path = new JPath("frameworks.dnxcore50.dependencies.['System.Xml.ReaderWriter'].source");
            Assert.AreEqual(5, path.Filters.Count);

            Assert.AreEqual("frameworks", ((FieldFilter)path.Filters[0]).Name);
            Assert.AreEqual("dnxcore50", ((FieldFilter)path.Filters[1]).Name);
            Assert.AreEqual("dependencies", ((FieldFilter)path.Filters[2]).Name);
            Assert.AreEqual("System.Xml.ReaderWriter", ((FieldFilter)path.Filters[3]).Name);
            Assert.AreEqual("source", ((FieldFilter)path.Filters[4]).Name);
        }
    }
}

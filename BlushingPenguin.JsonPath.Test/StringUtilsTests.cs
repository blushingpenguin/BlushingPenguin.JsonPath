using NUnit.Framework;
using System.Globalization;

namespace BlushingPenguin.JsonPath.Test
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class StringUtilsTests
    {
        [Test]
        public void Format1()
        {
            Assert.AreEqual(
                "arg1={0}.".FormatWith(
                    CultureInfo.InvariantCulture, "one"),
                "arg1=one.");
        }

        [Test]
        public void Format2()
        {
            Assert.AreEqual(
                "arg1={0}, arg2={1}.".FormatWith(
                    CultureInfo.InvariantCulture, "one", "two"),
                "arg1=one, arg2=two.");
        }

        [Test]
        public void Format3()
        {
            Assert.AreEqual(
                "arg1={0}, arg2={1}, arg3={2}.".FormatWith(
                    CultureInfo.InvariantCulture, "one", "two", "three"),
                "arg1=one, arg2=two, arg3=three.");
        }
        [Test]
        public void Format4()
        {
            Assert.AreEqual(
                "arg1={0}, arg2={1}, arg3={2}, arg4={3}.".FormatWith(
                    CultureInfo.InvariantCulture, "one", "two", "three", "four"),
                "arg1=one, arg2=two, arg3=three, arg4=four.");
        }
    }
}

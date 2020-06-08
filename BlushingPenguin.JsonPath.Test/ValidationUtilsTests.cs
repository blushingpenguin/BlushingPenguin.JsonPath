using NUnit.Framework;
using System;
using System.Globalization;

namespace BlushingPenguin.JsonPath.Test
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class ValidationUtilsTests
    {
        [Test]
        public void ValidOk()
        {
            Assert.DoesNotThrow(
                () => ValidationUtils.ArgumentNotNull("foo", "bar"));
        }

        [Test]
        public void ValidNotOk()
        {
            Assert.Throws<ArgumentNullException>(
                () => ValidationUtils.ArgumentNotNull(null, "bar"));
        }
    }
}

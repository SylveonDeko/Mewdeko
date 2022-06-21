using Mewdeko.Common.Yml;
using NUnit.Framework;
using System;
using System.Text;

namespace Mewdeko.Tests;

public class RandomTests
{
    [SetUp]
    public void Setup()
        => Console.OutputEncoding = Encoding.UTF8;

    [Test]
    public void Utf8CodepointsToEmoji()
    {
        const string point = @"0001F338";
        var hopefullyEmoji = YamlHelper.UnescapeUnicodeCodePoint(point);

        Assert.AreEqual("🌸", hopefullyEmoji, hopefullyEmoji);
    }
}
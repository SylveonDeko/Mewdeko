using System.Threading.Tasks;
using Mewdeko.Common.PubSub;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Mewdeko.Tests;

public class PubSubTests
{
    [Test]
    public async Task Test_EventPubSub_PubSub()
    {
        TypedKey<int> key = "test_key";
        var expected = new Randomizer().Next();
        var pubsub = new EventPubSub();
        await pubsub.Sub(key, data =>
        {
            Assert.AreEqual(expected, data);
            Assert.Pass();
            return default;
        }).ConfigureAwait(false);
        await pubsub.Pub(key, expected).ConfigureAwait(false);
        Assert.Fail("Event not registered");
    }

    [Test]
    public async Task Test_EventPubSub_MeaninglessUnsub()
    {
        TypedKey<int> key = "test_key";
        var expected = new Randomizer().Next();
        var pubsub = new EventPubSub();
        await pubsub.Sub(key, data =>
        {
            Assert.AreEqual(expected, data);
            Assert.Pass();
            return default;
        }).ConfigureAwait(false);
        await pubsub.Unsub(key, _ => default).ConfigureAwait(false);
        await pubsub.Pub(key, expected).ConfigureAwait(false);
        Assert.Fail("Event not registered");
    }

    [Test]
    public async Task Test_EventPubSub_MeaninglessUnsubThatLooksTheSame()
    {
        TypedKey<int> key = "test_key";
        var expected = new Randomizer().Next();
        var pubsub = new EventPubSub();
        await pubsub.Sub(key, data =>
        {
            Assert.AreEqual(expected, data);
            Assert.Pass();
            return default;
        }).ConfigureAwait(false);
        await pubsub.Unsub(key, data =>
        {
            Assert.AreEqual(expected, data);
            Assert.Pass();
            return default;
        }).ConfigureAwait(false);
        await pubsub.Pub(key, expected).ConfigureAwait(false);
        Assert.Fail("Event not registered");
    }

    [Test]
    public async Task Test_EventPubSub_MeaningfullUnsub()
    {
        TypedKey<int> key = "test_key";
        var pubsub = new EventPubSub();

        ValueTask Action(int data)
        {
            Assert.Fail("Event is raised when it shouldn't be");
            return default;
        }

        await pubsub.Sub(key, Action).ConfigureAwait(false);
        await pubsub.Unsub(key, Action).ConfigureAwait(false);
        await pubsub.Pub(key, 0).ConfigureAwait(false);
        Assert.Pass();
    }

    [Test]
    public async Task Test_EventPubSub_ObjectData()
    {
        TypedKey<byte[]> key = "test_key";
        var pubsub = new EventPubSub();

        var localData = new byte[1];

        ValueTask Action(byte[] data)
        {
            Assert.AreEqual(localData, data);
            Assert.Pass();
            return default;
        }

        await pubsub.Sub(key, Action).ConfigureAwait(false);
        await pubsub.Pub(key, localData).ConfigureAwait(false);

        Assert.Fail("Event not raised");
    }

    [Test]
    public async Task Test_EventPubSub_MultiSubUnsub()
    {
        TypedKey<object> key = "test_key";
        var pubsub = new EventPubSub();

        var localData = new object();
        var successCounter = 0;

        ValueTask Action1(object data)
        {
            Assert.AreEqual(localData, data);
            successCounter += 10;
            return default;
        }

        ValueTask Action2(object data)
        {
            Assert.AreEqual(localData, data);
            successCounter++;
            return default;
        }

        await pubsub.Sub(key, Action1).ConfigureAwait(false); // + 10 \
        await pubsub.Sub(key, Action2).ConfigureAwait(false); // + 1 - + = 12
        await pubsub.Sub(key, Action2).ConfigureAwait(false); // + 1 /
        await pubsub.Unsub(key, Action2).ConfigureAwait(false); // - 1/
        await pubsub.Pub(key, localData).ConfigureAwait(false);

        Assert.AreEqual(successCounter, 11, "Not all events are raised.");
    }
}
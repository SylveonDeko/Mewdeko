using Mewdeko.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Covers which instance handles a guild.
/// </summary>
[TestFixture]
public class GuildOwnershipTests
{
    private const ulong Guild = 123456789012345678UL;

    /// <summary>
    ///     With no assignments the master handles every guild and a custom instance handles none.
    /// </summary>
    [Test]
    public void NoRows_MasterHandlesEverything_CustomHandlesNothing()
    {
        var master = new GuildOwnershipMap(true, 1);
        var custom = new GuildOwnershipMap(false, 2);

        Assert.Multiple(() =>
        {
            Assert.That(master.IsMine(Guild), Is.True);
            Assert.That(custom.IsMine(Guild), Is.False);
        });
    }

    /// <summary>
    ///     A guild assigned to this instance is handled by it.
    /// </summary>
    [Test]
    public void AssignedToMe_IsMine()
    {
        var custom = new GuildOwnershipMap(false, 2);
        custom.Set(Guild, 2);

        Assert.That(custom.IsMine(Guild), Is.True);
    }

    /// <summary>
    ///     A guild assigned to another instance is ignored, even by the master.
    /// </summary>
    [Test]
    public void AssignedToAnother_IsNotMine_EvenForMaster()
    {
        var master = new GuildOwnershipMap(true, 1);
        master.Set(Guild, 2);

        Assert.Multiple(() =>
        {
            Assert.That(master.IsMine(Guild), Is.False);
            Assert.That(master.IsMine(Guild + 1), Is.True);
        });
    }

    /// <summary>
    ///     Releasing a guild returns it to the master and takes it away from the custom instance.
    /// </summary>
    [Test]
    public void Release_ReturnsGuildToMaster()
    {
        var master = new GuildOwnershipMap(true, 1);
        var custom = new GuildOwnershipMap(false, 2);
        master.Set(Guild, 2);
        custom.Set(Guild, 2);

        master.Remove(Guild);
        custom.Remove(Guild);

        Assert.Multiple(() =>
        {
            Assert.That(master.IsMine(Guild), Is.True);
            Assert.That(custom.IsMine(Guild), Is.False);
        });
    }

    /// <summary>
    ///     An instance whose id is not resolved yet does not claim assigned guilds.
    /// </summary>
    [Test]
    public void UnresolvedInstanceId_DoesNotClaimAssignedGuilds()
    {
        var custom = new GuildOwnershipMap(false);
        custom.Set(Guild, 2);

        Assert.That(custom.IsMine(Guild), Is.False);

        custom.InstanceId = 2;
        Assert.That(custom.IsMine(Guild), Is.True);
    }

    /// <summary>
    ///     Replacing the map drops assignments that no longer exist and keeps the rest.
    /// </summary>
    [Test]
    public void Replace_DropsStaleAssignments()
    {
        var master = new GuildOwnershipMap(true, 1);
        master.Set(Guild, 2);
        master.Set(Guild + 1, 2);

        master.Replace([new KeyValuePair<ulong, int>(Guild + 1, 2)]);

        Assert.Multiple(() =>
        {
            Assert.That(master.IsMine(Guild), Is.True);
            Assert.That(master.IsMine(Guild + 1), Is.False);
            Assert.That(master.Count, Is.EqualTo(1));
        });
    }

    /// <summary>
    ///     Only the master handles work with no guild.
    /// </summary>
    [Test]
    public void GlobalWork_IsMasterOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new GuildOwnershipMap(true).IsMaster, Is.True);
            Assert.That(new GuildOwnershipMap(false).IsMaster, Is.False);
        });
    }
}

using Mewdeko.Common.Yml;

namespace Mewdeko.Modules.Xp.Common;

public sealed class XpConfig
{
    [Comment(@"DO NOT CHANGE")]
    public int Version { get; set; } = 1;

    [Comment(@"How much XP will the users receive per message")]
    public int XpPerMessage { get; set; } = 3;

    [Comment(@"How often can the users receive XP in minutes")]
    public int MessageXpCooldown { get; set; } = 5;

    [Comment(@"Average amount of xp earned per minute in VC")]
    public double VoiceXpPerMinute { get; set; } = 0;

    [Comment(@"The maximum amount of minutes the bot will keep track of a user in a voice channel")]
    public int VoiceMaxMinutes { get; set; } = 720;
}
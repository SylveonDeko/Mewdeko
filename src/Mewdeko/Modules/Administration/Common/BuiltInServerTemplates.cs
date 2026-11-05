namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     The starter templates that come with the bot, built as ordinary backup data without messages or emojis.
/// </summary>
public static class BuiltInServerTemplates
{
    /// <summary>
    ///     The names of the starter templates.
    /// </summary>
    public static readonly string[] Names = ["community", "gaming", "support"];

    private const ulong View = (ulong)ChannelPermission.ViewChannel;
    private const ulong Send = (ulong)ChannelPermission.SendMessages;
    private const ulong Connect = (ulong)ChannelPermission.Connect;

    /// <summary>
    ///     Builds a starter template.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <returns>The template, or null if there is none with that name.</returns>
    public static ServerBackupData? Build(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "community" => Community(),
            "gaming" => Gaming(),
            "support" => Support(),
            _ => null
        };
    }

    private static ServerBackupData Community()
    {
        var b = new Builder();
        var admin = b.Role("Admin", [GuildPermission.Administrator], 0xE74C3C, true);
        var mod = b.Role("Moderator", ModeratorPermissions, 0x3498DB, true);
        b.Role("Member", [], 0x95A5A6);

        var info = b.Category("Information");
        b.Text("welcome", info, "Start here.", readOnly: true);
        b.Text("rules", info, "Read these before you chat.", readOnly: true);
        b.Text("announcements", info, "News from the staff.", readOnly: true);

        var chat = b.Category("Community");
        b.Text("general", chat, "Talk about anything.");
        b.Text("media", chat, "Share pictures and clips.");
        b.Text("bot-commands", chat, "Use bot commands here.");

        var voice = b.Category("Voice");
        b.Voice("Lounge", voice);
        b.Voice("Music", voice);
        b.Voice("Chill", voice);

        var staff = b.Category("Staff", admin, mod);
        b.Text("staff-chat", staff, "For staff only.", staff: [admin, mod]);
        b.Text("mod-log", staff, "Moderation logs.", staff: [admin, mod]);
        b.Voice("Staff Room", staff, staff: [admin, mod]);
        return b.Data;
    }

    private static ServerBackupData Gaming()
    {
        var b = new Builder();
        var admin = b.Role("Admin", [GuildPermission.Administrator], 0xE74C3C, true);
        var mod = b.Role("Moderator", ModeratorPermissions, 0x3498DB, true);
        b.Role("Member", [], 0x2ECC71);

        var info = b.Category("Information");
        b.Text("welcome", info, "Start here.", readOnly: true);
        b.Text("rules", info, "Read these before you play.", readOnly: true);
        b.Text("announcements", info, "News from the staff.", readOnly: true);

        var games = b.Category("Gaming");
        b.Text("general", games, "Talk about anything.");
        b.Text("looking-for-group", games, "Find people to play with.");
        b.Text("clips-and-screenshots", games, "Show your best moments.");
        b.Text("game-news", games, "Updates and patch notes.");
        b.Text("memes", games, "Keep it friendly.");
        b.Text("bot-commands", games, "Use bot commands here.");

        var voice = b.Category("Voice");
        b.Voice("General", voice);
        b.Voice("Squad 1", voice, userLimit: 4);
        b.Voice("Squad 2", voice, userLimit: 4);
        b.Voice("Squad 3", voice, userLimit: 4);
        b.Voice("AFK", voice);

        var staff = b.Category("Staff", admin, mod);
        b.Text("staff-chat", staff, "For staff only.", staff: [admin, mod]);
        b.Text("mod-log", staff, "Moderation logs.", staff: [admin, mod]);
        return b.Data;
    }

    private static ServerBackupData Support()
    {
        var b = new Builder();
        var admin = b.Role("Admin", [GuildPermission.Administrator], 0xE74C3C, true);
        var staffRole = b.Role("Support Staff", ModeratorPermissions, 0x3498DB, true);
        b.Role("Member", [], 0x95A5A6);

        var info = b.Category("Information");
        b.Text("welcome", info, "Start here.", readOnly: true);
        b.Text("rules", info, "Read these before you ask for help.", readOnly: true);
        b.Text("announcements", info, "News and downtime notices.", readOnly: true);
        b.Text("faq", info, "Answers to common questions.", readOnly: true);

        var help = b.Category("Support");
        b.Text("help", help, "Ask your question here.", slowMode: 10);
        b.Text("bug-reports", help, "Report something that is broken.", slowMode: 10);
        b.Text("suggestions", help, "Tell us what to build next.");
        b.Text("feedback", help, "Share what you think.");

        var chat = b.Category("Community");
        b.Text("general", chat, "Talk about anything.");
        b.Text("off-topic", chat, "Anything else.");
        b.Voice("Support Call", chat);

        var staff = b.Category("Staff", admin, staffRole);
        b.Text("staff-chat", staff, "For staff only.", staff: [admin, staffRole]);
        b.Text("case-notes", staff, "Notes on open cases.", staff: [admin, staffRole]);
        return b.Data;
    }

    private static GuildPermission[] ModeratorPermissions =>
    [
        GuildPermission.KickMembers, GuildPermission.BanMembers, GuildPermission.ManageMessages,
        GuildPermission.ModerateMembers, GuildPermission.MuteMembers, GuildPermission.MoveMembers,
        GuildPermission.ManageNicknames, GuildPermission.ViewAuditLog
    ];

    private sealed class Builder
    {
        private ulong nextId = 1;
        private int rolePosition = 1;
        private int channelPosition;

        public Builder()
        {
            Data.Roles.Add(new BackupRole
            {
                Id = nextId++, IsEveryone = true, Name = "@everyone",
                Permissions = GuildPermissions.None.RawValue
            });
        }

        public ServerBackupData Data { get; } = new();

        public ulong Role(string name, GuildPermission[] permissions, uint color, bool hoist = false)
        {
            var raw = 0UL;
            foreach (var permission in permissions)
                raw |= (ulong)permission;

            var id = nextId++;
            Data.Roles.Add(new BackupRole
            {
                Id = id, Name = name, Permissions = raw, Color = color, Hoist = hoist, Position = rolePosition++
            });
            return id;
        }

        public ulong Category(string name, params ulong[] staff)
        {
            var id = nextId++;
            Data.Channels.Add(new BackupChannel
            {
                Id = id, Type = "category", Name = name, Position = channelPosition++,
                Overwrites = staff.Length == 0 ? [] : Private(staff)
            });
            return id;
        }

        public void Text(string name, ulong parent, string topic, bool readOnly = false, int slowMode = 0,
            ulong[]? staff = null)
        {
            var overwrites = staff is null ? [] : Private(staff);
            if (readOnly)
            {
                overwrites.Add(new BackupOverwrite
                {
                    IsRole = true, TargetId = 1, Deny = Send
                });
            }

            Data.Channels.Add(new BackupChannel
            {
                Id = nextId++, Type = "text", Name = name, Topic = topic, ParentId = parent, SlowMode = slowMode,
                Position = channelPosition++, Overwrites = overwrites
            });
        }

        public void Voice(string name, ulong parent, int? userLimit = null, ulong[]? staff = null)
        {
            var overwrites = staff is null ? [] : Private(staff, Connect);
            Data.Channels.Add(new BackupChannel
            {
                Id = nextId++, Type = "voice", Name = name, ParentId = parent, UserLimit = userLimit,
                Bitrate = 64000, Position = channelPosition++, Overwrites = overwrites
            });
        }

        private static List<BackupOverwrite> Private(ulong[] roles, ulong extraAllow = 0)
        {
            var list = new List<BackupOverwrite>
            {
                new()
                {
                    IsRole = true, TargetId = 1, Deny = View
                }
            };
            list.AddRange(roles.Select(role => new BackupOverwrite
            {
                IsRole = true, TargetId = role, Allow = View | extraAllow
            }));
            return list;
        }
    }
}

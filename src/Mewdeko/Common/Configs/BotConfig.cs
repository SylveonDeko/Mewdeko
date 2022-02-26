using System.Globalization;
using Mewdeko.Common.Yml;
using SixLabors.ImageSharp.PixelFormats;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Configs;

public sealed class BotConfig
{
    public BotConfig()
    {
        Version = 1;
        var color = new ColorConfig();
        Color = color;
        DefaultLocale = new CultureInfo("en-US");
        ConsoleOutputType = ConsoleOutputType.Normal;
        ForwardMessages = false;
        ForwardToAllOwners = false;
        DmHelpText = @"{""description"": ""Type `%prefix%h` for help.""}";
        HelpText = @"{
  ""title"": ""To invite me to your server, use this link"",
  ""description"": ""https://discordapp.com/oauth2/authorize?client_id={0}&scope=bot&permissions=66186303"",
  ""color"": 53380,
  ""thumbnail"": ""https://i.imgur.com/nKYyqMK.png"",
  ""fields"": [
    {
      ""name"": ""Useful help commands"",
      ""value"": ""`%bot.prefix%modules` Lists all bot modules.
`%prefix%h CommandName` Shows some help about a specific command.
`%prefix%commands ModuleName` Lists all commands in a module."",
      ""inline"": false
    },
    {
      ""name"": ""List of all Commands"",
      ""value"": ""https://Mewdeko.bot/commands"",
      ""inline"": false
    },
    {
      ""name"": ""Mewdeko Support Server"",
      ""value"": ""https://discord.Mewdeko.bot/ "",
      ""inline"": true
    }
  ]
}";
        var blocked = new BlockedConfig();
        Blocked = blocked;
        Prefix = ".";
        RotateStatuses = false;
        GroupGreets = false;
    }

    [Comment(@"DO NOT CHANGE")] public int Version { get; set; }

    [Comment(@"Most commands, when executed, have a small colored line
next to the response. The color depends whether the command
is completed, errored or in progress (pending)
Color settings below are for the color of those lines.
To get color's hex, you can go here https://htmlcolorcodes.com/
and copy the hex code fo your selected color (marked as #)")]
    public ColorConfig Color { get; set; }

    [Comment("Default bot language. It has to be in the list of supported languages (.langli)")]
    public CultureInfo DefaultLocale { get; set; }

    [Comment(@"Style in which executed commands will show up in the console.
Allowed values: Simple, Normal, None")]
    public ConsoleOutputType ConsoleOutputType { get; set; }

//         [Comment(@"For what kind of updates will the bot check.
// Allowed values: Release, Commit, None")]
//         public UpdateCheckType CheckForUpdates { get; set; }

    // [Comment(@"How often will the bot check for updates, in hours")]
    // public int CheckUpdateInterval { get; set; }

    [Comment(@"Do you want any messages sent by users in Bot's DM to be forwarded to the owner(s)?")]
    public bool ForwardMessages { get; set; }

    [Comment(
        @"Do you want the message to be forwarded only to the first owner specified in the list of owners (in creds.yml),
or all owners? (this might cause the bot to lag if there's a lot of owners specified)")]
    public bool ForwardToAllOwners { get; set; }

    [Comment(@"When a user DMs the bot with a message which is not a command
they will receive this message. Leave empty for no response. The string which will be sent whenever someone DMs the bot.
Supports embeds. How it looks: https://puu.sh/B0BLV.png"), YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string DmHelpText { get; set; }

    [Comment(@"This is the response for the .h command"), YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string HelpText { get; set; }

    [Comment(@"List of modules and commands completely blocked on the bot")]
    public BlockedConfig Blocked { get; set; }

    [Comment(@"Which string will be used to recognize the commands")]
    public string Prefix { get; set; }

    [Comment(@"Toggles whether your bot will group greet/bye messages into a single message every 5 seconds.
1st user who joins will get greeted immediately
If more users join within the next 5 seconds, they will be greeted in groups of 5.
This will cause %user.mention% and other placeholders to be replaced with multiple users. 
Keep in mind this might break some of your embeds - for example if you have %user.avatar% in the thumbnail,
it will become invalid, as it will resolve to a list of avatars of grouped users.
note: This setting is primarily used if you're afraid of raids, or you're running medium/large bots where some
      servers might get hundreds of people join at once. This is used to prevent the bot from getting ratelimited,
      and (slightly) reduce the greet spam in those servers.")]
    public bool GroupGreets { get; set; }

    [Comment(@"Whether the bot will rotate through all specified statuses.
This setting can be changed via .rots command.
See RotatingStatuses submodule in Administration.")]
    public bool RotateStatuses { get; set; }

//         [Comment(@"Whether the prefix will be a suffix, or prefix.
// For example, if your prefix is ! you will run a command called 'cash' by typing either
// '!cash @Someone' if your prefixIsSuffix: false or
// 'cash @Someone!' if your prefixIsSuffix: true")]
//         public bool PrefixIsSuffix { get; set; }

    // public string Prefixed(string text) => PrefixIsSuffix
    //     ? text + Prefix
    //     : Prefix + text;

    public string Prefixed(string text) => Prefix + text;
}

public class BlockedConfig
{
    public BlockedConfig()
    {
        Modules = new HashSet<string>();
        Commands = new HashSet<string>();
    }

    public HashSet<string> Commands { get; set; }
    public HashSet<string> Modules { get; set; }
}

public class ColorConfig
{
    public ColorConfig()
    {
        Ok = Rgba32.ParseHex("00e584");
        Error = Rgba32.ParseHex("ee281f");
        Pending = Rgba32.ParseHex("faa61a");
    }

    [Comment(@"Color used for embed responses when command successfully executes")]
    public Rgba32 Ok { get; set; }

    [Comment(@"Color used for embed responses when command has an error")]
    public Rgba32 Error { get; set; }

    [Comment(@"Color used for embed responses while command is doing work or is in progress")]
    public Rgba32 Pending { get; set; }
}

public enum ConsoleOutputType
{
    Normal = 0,
    Simple = 1,
    None = 2
}
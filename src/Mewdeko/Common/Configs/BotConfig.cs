using System.Globalization;
using Mewdeko.Common.Yml;
using SkiaSharp;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Configs;

public sealed class BotConfig
{
    public BotConfig()
    {
        Version = 1;
        Color = new ColorConfig();
        DefaultLocale = new CultureInfo("en-US");
        ConsoleOutputType = ConsoleOutputType.Normal;
        ForwardMessages = false;
        ForwardToAllOwners = false;
        DmHelpText = """{"description": "Type `%prefix%h` for help."}""";
        HelpText = @"change this in bot.yml";
        Blocked = new BlockedConfig();
        Prefix = ".";
        RotateStatuses = false;
        GroupGreets = false;
        ShowInviteButton = true;
        LoadingEmote = "<a:HaneMeow:968564817784877066>";
        ErrorEmote = "<:HaneNo:914307917954576414>";
        SuccessEmote = "<:hane_wow:945005763829575680>";
        SupportServer = "https://discord.gg/mewdeko";
        RedirectUrl = "https://mewdeko.tech/auth.html";
        YoutubeSupport = true;
        ChatGptInitPrompt =
            "Your name is Mewdeko. You are a discord bot. Your profile picture is of the character Hanekawa Tsubasa in Black Hanekawa form. You were created by sylveondeko";
        ChatGptMaxTokens = 1000;
        ChatGptTemperature = 0.9;
        QuarantineNotification = true;
    }

    [Comment(@"DO NOT CHANGE")]
    public int Version { get; set; }

    [Comment(@"Most commands, when executed, have a small colored line
next to the response. The color depends whether the command
is completed, errored or in progress (pending)
Color settings below are for the color of those lines.
To get color's hex, you can go here https://htmlcolorcodes.com/
and copy the hex code fo your selected color (marked as #)")]
    public ColorConfig Color { get; set; }

    [Comment("Default bot language. It has to be in the list of supported languages (.langli)")]
    public CultureInfo? DefaultLocale { get; set; }

    [Comment(@"Style in which executed commands will show up in the console.
Allowed values: Simple, Normal, None")]
    public ConsoleOutputType ConsoleOutputType { get; set; }

    [Comment(@"For what kind of updates will the bot check.
    Allowed values: Release, Commit, None")]
    public UpdateCheckType CheckForUpdates { get; set; }

    [Comment(@"How often will the bot check for updates, in hours")]
    public int CheckUpdateInterval { get; set; }

    [Comment("Set which branch to check for updates")]
    public string UpdateBranch { get; set; }

    [Comment(@"Do you want any messages sent by users in Bot's DM to be forwarded to the owner(s)?")]
    public bool ForwardMessages { get; set; }

    [Comment(
        @"Do you want the message to be forwarded only to the first owner specified in the list of owners (in creds.yml),
or all owners? (this might cause the bot to lag if there's a lot of owners specified)")]
    public bool ForwardToAllOwners { get; set; }

    [Comment(@"When a user DMs the bot with a message which is not a command
they will receive this message. Leave empty for no response. The string which will be sent whenever someone DMs the bot.
Supports embeds. How it looks: https://puu.sh/B0BLV.png"), YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string? DmHelpText { get; set; }

    [Comment(@"This is the response for the .h command"), YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string HelpText { get; set; }

    [Comment(@"List of modules and commands completely blocked on the bot")]
    public BlockedConfig? Blocked { get; set; }

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

    [Comment(@"Used for global command logs")]
    public ulong CommandLogChannel { get; set; }

    [Comment("Enable or disable youtube support")]
    public bool YoutubeSupport { get; set; }

    [Comment("ChatGPT API Key")]
    public string ChatGptKey { get; set; }

    [Comment("ChatGPT Channel ID")]
    public ulong ChatGptChannel { get; set; }

    [Comment("ChatGPT Init Prompt. Used to set how chatgpt will act.")]
    public string ChatGptInitPrompt { get; set; }

    [Comment("Max tokens that chatgpt can output")]
    public int ChatGptMaxTokens { get; set; }

    [Comment(@"Used to enable or disable showing the invite button on some commands")]
    public bool ShowInviteButton { get; set; }

    [Comment("ChatGPT Webhook, used if you want to change the appearance of chatgpt messages.")]
    public string ChatGptWebhook { get; set; }

    [Comment("Sets the temperature for ChatGPT")]
    public double ChatGptTemperature { get; set; }

    [Comment("The model to use for chatgpt")]
    public string ChatGptModel { get; set; }

    [Comment(
        @"The authorization redirect url for the auth command. This MUST be added to your valid redirect urls in the discord developer portal.")]
    public string RedirectUrl { get; set; }

    [Comment("Used to set the error emote used across the bot.")]
    public string ErrorEmote { get; set; }

    [Comment("Used to set the success emote used across the bot.")]
    public string SuccessEmote { get; set; }

    [Comment("Used to set the loading emote for the bot.")]
    public string LoadingEmote { get; set; }

    [Comment("Used to set the support server invite on public Mewdeko")]
    public string SupportServer { get; set; }

    [Comment(
        "Notify the owner of the bot when the bot gets quarantined. Only dms first owner if ForwardMessages is enabled.")]
    public bool QuarantineNotification { get; set; }


    public string Prefixed(string text) => Prefix + text;
}

public class BlockedConfig
{
    public BlockedConfig()
    {
        Modules = new HashSet<string?>();
        Commands = new HashSet<string?>();
    }

    public HashSet<string?>? Commands { get; set; }
    public HashSet<string?>? Modules { get; set; }
}

public class ColorConfig
{
    public ColorConfig()
    {
        Ok = SKColor.Parse("00e584");
        Error = SKColor.Parse("ee281f");
        Pending = SKColor.Parse("faa61a");
    }

    [Comment(@"Color used for embed responses when command successfully executes")]
    public SKColor Ok { get; set; }

    [Comment(@"Color used for embed responses when command has an error")]
    public SKColor Error { get; set; }

    [Comment(@"Color used for embed responses while command is doing work or is in progress")]
    public SKColor Pending { get; set; }
}

public enum ConsoleOutputType
{
    Normal = 0,
    Simple = 1,
    None = 2
}

public enum UpdateCheckType
{
    Release = 0,
    Commit = 1,
    None = 2
}
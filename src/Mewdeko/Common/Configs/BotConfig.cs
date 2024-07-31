using System.Globalization;
using Mewdeko.Common.Yml;
using SkiaSharp;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Configs;

/// <summary>
/// Yml configuration for the bot.
/// </summary>
public sealed class BotConfig
{
    /// <summary>
    /// Creates a new instance of <see cref="BotConfig"/>. Sets default values.
    /// </summary>
    public BotConfig()
    {
        Version = 1;
        Color = new ColorConfig();
        DefaultLocale = new CultureInfo("en-US");
        ConsoleOutputType = ConsoleOutputType.Normal;
        ForwardMessages = false;
        ForwardToAllOwners = false;
        DmHelpText = """{"description": "Type `%prefix%h` for help."}""";
        HelpText = "change this in bot.yml";
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
        UpdateBranch = "main";
        CheckForUpdates = UpdateCheckType.None;
    }

    /// <summary>
    /// Gets or sets the version of the bot configuration.
    /// </summary>
    [Comment("DO NOT CHANGE")]
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the color configuration for the bot. Used for embeds. See <see cref="ColorConfig"/>.
    /// </summary>
    [Comment(@"Most commands, when executed, have a small colored line
next to the response. The color depends whether the command
is completed, errored or in progress (pending)
Color settings below are for the color of those lines.
To get color's hex, you can go here https://htmlcolorcodes.com/
and copy the hex code fo your selected color (marked as #)")]
    public ColorConfig Color { get; set; }

    /// <summary>
    /// Gets or sets the default locale for the bot.
    /// </summary>
    [Comment("Default bot language. It has to be in the list of supported languages (.langli)")]
    public CultureInfo? DefaultLocale { get; set; }

    /// <summary>
    /// Gets or sets the style in which executed commands will show up in the console.
    /// </summary>
    [Comment(@"Style in which executed commands will show up in the console.
Allowed values: Simple, Normal, None")]
    public ConsoleOutputType ConsoleOutputType { get; set; }

    /// <summary>
    /// Gets or sets the type of updates the bot will check for.
    /// </summary>
    [Comment(@"For what kind of updates will the bot check.
    Allowed values: Release, Commit, None")]
    public UpdateCheckType CheckForUpdates { get; set; }

    /// <summary>
    /// Gets or sets the interval at which the bot will check for updates, in hours.
    /// </summary>
    [Comment("How often will the bot check for updates, in hours")]
    public int CheckUpdateInterval { get; set; }

    /// <summary>
    /// Gets or sets the branch to check for updates. Default is "main".
    /// </summary>
    [Comment("Set which branch to check for updates")]
    public string UpdateBranch { get; set; }

    /// <summary>
    /// Gets or sets whether messages sent by users in the bot's DM will be forwarded to the owner(s).
    /// </summary>
    [Comment("Do you want any messages sent by users in Bot's DM to be forwarded to the owner(s)?")]
    public bool ForwardMessages { get; set; }

    /// <summary>
    /// Gets or sets whether messages sent by users in the bot's DM will be forwarded to all owners.
    /// </summary>
    [Comment(
        @"Do you want the message to be forwarded only to the first owner specified in the list of owners (in creds.yml),
or all owners? (this might cause the bot to lag if there's a lot of owners specified)")]
    public bool ForwardToAllOwners { get; set; }

    /// <summary>
    /// Gets or sets the message to send to users who DM the bot with a message that is not a command.
    /// </summary>
    [Comment(@"When a user DMs the bot with a message which is not a command
they will receive this message. Leave empty for no response. The string which will be sent whenever someone DMs the bot.
Supports embeds. How it looks: https://puu.sh/B0BLV.png"), YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string? DmHelpText { get; set; }

    /// <summary>
    /// Gets or sets the help text for the .h command. Uses embed code.
    /// </summary>
    [Comment("This is the response for the .h command"), YamlMember(ScalarStyle = ScalarStyle.Literal)]
    public string HelpText { get; set; }

    /// <summary>
    /// Gets or sets the list of modules and commands that are completely blocked on the bot. See <see cref="BlockedConfig"/>.
    /// </summary>
    [Comment("List of modules and commands completely blocked on the bot")]
    public BlockedConfig? Blocked { get; set; }

    /// <summary>
    /// Gets or sets the default prefix for the bot.
    /// </summary>
    [Comment("Which string will be used to recognize the commands")]
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets whether the bot will group greet/bye messages into a single message every 5 seconds.
    /// </summary>
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

    /// <summary>
    /// Gets or sets whether the bot will rotate through all specified statuses.
    /// </summary>
    [Comment(@"Whether the bot will rotate through all specified statuses.
This setting can be changed via .rots command.
See RotatingStatuses submodule in Administration.")]
    public bool RotateStatuses { get; set; }

    /// <summary>
    /// Gets or sets the channel ID for the gloal command log channel.
    /// </summary>
    [Comment("Used for global command logs")]
    public ulong CommandLogChannel { get; set; }

    /// <summary>
    /// Gets or sets whether the bot will support youtube links. (Or hides that it supports them lol)
    /// </summary>
    [Comment("Enable or disable youtube support")]
    public bool YoutubeSupport { get; set; }

    /// <summary>
    /// Gets or sets the ChatGPT API key.
    /// </summary>
    [Comment("ChatGPT API Key")]
    public string ChatGptKey { get; set; }

    /// <summary>
    /// Gets or sets the ChatGPT Channel ID.
    /// </summary>
    [Comment("ChatGPT Channel ID")]
    public ulong ChatGptChannel { get; set; }

    /// <summary>
    /// Gets or sets the ChatGPT Initial prompt when starting a conversation with ChatGPT.
    /// </summary>
    [Comment("ChatGPT Init Prompt. Used to set how chatgpt will act.")]
    public string ChatGptInitPrompt { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of tokens that ChatGPT can output.
    /// </summary>
    [Comment("Max tokens that chatgpt can output")]
    public int ChatGptMaxTokens { get; set; }

    /// <summary>
    /// Gets or sets whether the invite and donation button will be shown on some commands.
    /// </summary>
    [Comment("Used to enable or disable showing the invite button on some commands")]
    public bool ShowInviteButton { get; set; }

    /// <summary>
    /// Gets or sets the webhook for ChatGPT.
    /// </summary>
    [Comment("ChatGPT Webhook, used if you want to change the appearance of chatgpt messages.")]
    public string ChatGptWebhook { get; set; }

    /// <summary>
    /// Gets or sets the temperature for ChatGPT.
    /// </summary>
    [Comment("Sets the temperature for ChatGPT")]
    public double ChatGptTemperature { get; set; }

    /// <summary>
    /// Gets or sets the model to use for ChatGPT.
    /// </summary>
    [Comment("The model to use for chatgpt")]
    public string ChatGptModel { get; set; }

    /// <summary>
    /// Gets or sets the redirect url for the auth command.
    /// </summary>
    [Comment(
        "The authorization redirect url for the auth command. This MUST be added to your valid redirect urls in the discord developer portal.")]
    public string RedirectUrl { get; set; }

    /// <summary>
    /// Gets or sets the error emote used across the bot.
    /// </summary>
    [Comment("Used to set the error emote used across the bot.")]
    public string ErrorEmote { get; set; }

    /// <summary>
    /// Gets or sets the success emote used across the bot.
    /// </summary>
    [Comment("Used to set the success emote used across the bot.")]
    public string SuccessEmote { get; set; }

    /// <summary>
    /// Gets or sets the loading emote used across the bot.
    /// </summary>
    [Comment("Used to set the loading emote for the bot.")]
    public string LoadingEmote { get; set; }

    /// <summary>
    /// Gets or sets the support server invite on Mewdeko.
    /// </summary>
    [Comment("Used to set the support server invite on Mewdeko")]
    public string SupportServer { get; set; }

    /// <summary>
    /// Gets or sets whether the bot will notify the owner when the bot gets quarantined.
    /// </summary>
    [Comment(
        "Notify the owner of the bot when the bot gets quarantined. Only dms first owner if ForwardMessages is enabled.")]
    public bool QuarantineNotification { get; set; }
}

/// <summary>
/// Configuration for blocked modules and commands.
/// </summary>
public class BlockedConfig
{
    /// <summary>
    /// Creates a new instance of <see cref="BlockedConfig"/>. Sets default values.
    /// </summary>
    public BlockedConfig()
    {
        Modules = [];
        Commands = [];
    }

    /// <summary>
    /// Gets or sets the list of blocked commands.
    /// </summary>
    public HashSet<string?>? Commands { get; set; }

    /// <summary>
    /// Gets or sets the list of blocked modules.
    /// </summary>
    public HashSet<string?>? Modules { get; set; }
}

/// <summary>
/// Configuration for colors used in the bot.
/// </summary>
public class ColorConfig
{
    /// <summary>
    /// Creates a new instance of <see cref="ColorConfig"/>. Sets default values.
    /// </summary>
    public ColorConfig()
    {
        Ok = SKColor.Parse("00e584");
        Error = SKColor.Parse("ee281f");
        Pending = SKColor.Parse("faa61a");
    }

    /// <summary>
    /// Gets or sets the color used for embed responses when command successfully executes.
    /// </summary>
    [Comment("Color used for embed responses when command successfully executes")]
    public SKColor Ok { get; set; }

    /// <summary>
    /// Gets or sets the color used for embed responses when command has an error.
    /// </summary>
    [Comment("Color used for embed responses when command has an error")]
    public SKColor Error { get; set; }

    /// <summary>
    /// Gets or sets the color used for embed responses while command is doing work or is in progress.
    /// </summary>
    [Comment("Color used for embed responses while command is doing work or is in progress")]
    public SKColor Pending { get; set; }
}

/// <summary>
/// Configuration for the bot's console output.
/// </summary>
public enum ConsoleOutputType
{
    /// <summary>
    /// Normal console output.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Simple console output.
    /// </summary>
    Simple = 1,

    /// <summary>
    /// No console output.
    /// </summary>
    None = 2
}

/// <summary>
/// Type of update check to perform.
/// </summary>
public enum UpdateCheckType
{
    /// <summary>
    /// Check for release updates.
    /// </summary>
    Release = 0,

    /// <summary>
    /// Check for commit updates.
    /// </summary>
    Commit = 1,

    /// <summary>
    /// Do not check for updates.
    /// </summary>
    None = 2
}
using System;

namespace Discord
{
    // just a copy paste from discord.net in order to rename it, for compatibility iwth v3 which is gonna use custom lib


    // Summary:
    //     Defines the available permissions for a channel.
    [Flags]
    public enum GuildPerm : ulong
    {
        //
        // Summary:
        //     Allows creation of instant invites.
        CreateInstantInvite = 1,
        //
        // Summary:
        //     Allows kicking members.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        KickMembers = 2,
        //
        // Summary:
        //     Allows banning members.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        BanMembers = 4,
        //
        // Summary:
        //     Allows all permissions and bypasses channel permission overwrites.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        Administrator = 8,
        //
        // Summary:
        //     Allows management and editing of channels.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        ManageChannels = 16,
        //
        // Summary:
        //     Allows management and editing of the guild.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        ManageGuild = 32,
        //
        // Summary:
        //     Allows for the addition of reactions to messages.
        AddReactions = 64,
        //
        // Summary:
        //     Allows for viewing of audit logs.
        ViewAuditLog = 128,
        PrioritySpeaker = 256,
        ReadMessages = 1024,
        ViewChannel = 1024,
        SendMessages = 2048,
        //
        // Summary:
        //     Allows for sending of text-to-speech messages.
        SendTTSMessages = 4096,
        //
        // Summary:
        //     Allows for deletion of other users messages.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        ManageMessages = 8192,
        //
        // Summary:
        //     Allows links sent by users with this permission will be auto-embedded.
        EmbedLinks = 16384,
        //
        // Summary:
        //     Allows for uploading images and files.
        AttachFiles = 32768,
        //
        // Summary:
        //     Allows for reading of message history.
        ReadMessageHistory = 65536,
        //
        // Summary:
        //     Allows for using the @everyone tag to notify all users in a channel, and the
        //     @here tag to notify all online users in a channel.
        MentionEveryone = 131072,
        //
        // Summary:
        //     Allows the usage of custom emojis from other servers.
        UseExternalEmojis = 262144,
        //
        // Summary:
        //     Allows for joining of a voice channel.
        Connect = 1048576,
        //
        // Summary:
        //     Allows for speaking in a voice channel.
        Speak = 2097152,
        //
        // Summary:
        //     Allows for muting members in a voice channel.
        MuteMembers = 4194304,
        //
        // Summary:
        //     Allows for deafening of members in a voice channel.
        DeafenMembers = 8388608,
        //
        // Summary:
        //     Allows for moving of members between voice channels.
        MoveMembers = 16777216,
        //
        // Summary:
        //     Allows for using voice-activity-detection in a voice channel.
        UseVAD = 33554432,
        //
        // Summary:
        //     Allows for modification of own nickname.
        ChangeNickname = 67108864,
        //
        // Summary:
        //     Allows for modification of other users nicknames.
        ManageNicknames = 134217728,
        //
        // Summary:
        //     Allows management and editing of roles.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        ManageRoles = 268435456,
        //
        // Summary:
        //     Allows management and editing of webhooks.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        ManageWebhooks = 536870912,
        //
        // Summary:
        //     Allows management and editing of emojis.
        //
        // Remarks:
        //     This permission requires the owner account to use two-factor authentication when
        //     used on a guild that has server-wide 2FA enabled.
        ManageEmojis = 1073741824
    }

    //
    // Summary:
    //     Defines the available permissions for a channel.
    [Flags]
    public enum ChannelPerm : ulong
    {
        //
        // Summary:
        //     Allows creation of instant invites.
        CreateInstantInvite = 1,
        //
        // Summary:
        //     Allows management and editing of channels.
        ManageChannel = 16,
        //
        // Summary:
        //     Allows for the addition of reactions to messages.
        AddReactions = 64,
        PrioritySpeaker = 256,
        //
        // Summary:
        //     Allows for reading of messages. This flag is obsolete, use Discord.ChannelPermission.ViewChannel
        //     instead.
        ReadMessages = 1024,
        //
        // Summary:
        //     Allows guild members to view a channel, which includes reading messages in text
        //     channels.
        ViewChannel = 1024,
        //
        // Summary:
        //     Allows for sending messages in a channel.
        SendMessages = 2048,
        //
        // Summary:
        //     Allows for sending of text-to-speech messages.
        SendTTSMessages = 4096,
        //
        // Summary:
        //     Allows for deletion of other users messages.
        ManageMessages = 8192,
        //
        // Summary:
        //     Allows links sent by users with this permission will be auto-embedded.
        EmbedLinks = 16384,
        //
        // Summary:
        //     Allows for uploading images and files.
        AttachFiles = 32768,
        //
        // Summary:
        //     Allows for reading of message history.
        ReadMessageHistory = 65536,
        //
        // Summary:
        //     Allows for using the @everyone tag to notify all users in a channel, and the
        //     @here tag to notify all online users in a channel.
        MentionEveryone = 131072,
        //
        // Summary:
        //     Allows the usage of custom emojis from other servers.
        UseExternalEmojis = 262144,
        //
        // Summary:
        //     Allows for joining of a voice channel.
        Connect = 1048576,
        //
        // Summary:
        //     Allows for speaking in a voice channel.
        Speak = 2097152,
        //
        // Summary:
        //     Allows for muting members in a voice channel.
        MuteMembers = 4194304,
        //
        // Summary:
        //     Allows for deafening of members in a voice channel.
        DeafenMembers = 8388608,
        //
        // Summary:
        //     Allows for moving of members between voice channels.
        MoveMembers = 16777216,
        //
        // Summary:
        //     Allows for using voice-activity-detection in a voice channel.
        UseVAD = 33554432,
        //
        // Summary:
        //     Allows management and editing of roles.
        ManageRoles = 268435456,
        //
        // Summary:
        //     Allows management and editing of webhooks.
        ManageWebhooks = 536870912
    }
}

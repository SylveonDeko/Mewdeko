Placeholders are used in Quotes, Custom Reactions, Greet/Bye messages, playing statuses, and a few other places.

They can be used to make the message more user friendly, generate random numbers or pictures, etc...

Some features have their own specific placeholders which are noted in that feature's command help. Some placeholders are not available in certain features because they don't make sense there.

### Here is a list of the usual placeholders:

**If you're using placeholders in embeds, don't use %user.mention% and %bot.mention% in titles, footers and field names. They will not show properly.**

**Bot placeholders**

- `%bot.status%` - Bot's status (Online, Idle, DoNotDisturb, Invisible)
- `%bot.latency%` - Bot latency
- `%bot.name%` - Bot username
- `%bot.mention%` - Bot mention (clickable)
- `%bot.fullname%` - Bot username#discriminator
- `%bot.time%` - Bot time (usually the time of the server it's hosted on)
- `%bot.discrim%` - Bot's discriminator
- `%bot.id%` - Bot's user ID
- `%bot.avatar%` - Bot's avatar url

**Server placeholders**

- `%server.id%` - Server ID
- `%server.name%` - Server name
- `%server.members%` - Member count
- `%server.time%` - Server time (requires `.timezone` to be set)

**Channel placeholders**

- `%channel.mention%` - Channel mention (clickable)
- `%channel.name%` - Channel name
- `%channel.id%` - Channel ID
- `%channel.created%` - Channel creation date
- `%channel.nsfw%` - Returns either `True` or `False`, depending on if the channel is designated as NSFW using discord
- `%channel.topic%` - Channel topic

**User placeholders**

- `%user.mention%` - User mention
- `%user.fullname%` - Username#discriminator
- `%user.name%` - Username
- `%user.discrim%` - Discriminator
- `%user.avatar%` - User's avatar url
- `%user.id%` - User ID
- `%user.created_time%` - Account creation time (local time)
- `%user.created_date%` - Account creation date
- `%user.joined_time%` - Account join time (local time)
- `%user.joined_date%` - Account join date

**Ban message placeholders**  

- `%ban.mod%` - Full name of the moderator who performed the ban  
- `%ban.mod.fullname%` - Full name of the moderator who performed the ban  
- `%ban.mod.mention%` - Moderator's mention  
- `%ban.mod.name%` - Name of the moderator - Admin  
- `%ban.mod.discrim%` - Discriminator of the moderator - 1234  
- `%ban.user%` - Full name of the banned user  
- `%ban.user.fullname%` - Full name of the banned user  
- `%ban.user.name%` - Name of the banned user  
- `%ban.user.discrim%` - Discriminator of the banned user  
- `%ban.reason%` - Reason for the ban, if provided  
- `%ban.duration%` - Duration of the ban in the form Days.Hours:Minutes (6.05:04)  

**Bot stats placeholders**

- `%servers%` - Server count bot has joined
- `%users%` - Combined user count on servers the bot has joined

**Shard stats placeholders**

- `%shard.servercount%` - Server count on current shard
- `%shard.usercount%` - Combined user count on current shard
- `%shard.id%` - Shard ID

**Music placeholders**

*Note: These placeholders will only work in rotating playing statuses.*

- `%music.queued%` - Amount of songs currently queued
- `%music.playing%` - Current song name

**Miscellaneous placeholders**

- `%rngX-Y%` - Returns a random number between X and Y
- `%target%` - Returns anything the user has written after the trigger **(only works on custom reactions)**
- `%img:stuff%` - Returns an `imgur.com` search for "stuff" **(only works on custom reactions)**

![img](https://puu.sh/B7mgI.png)

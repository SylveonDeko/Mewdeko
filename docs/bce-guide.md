
# ⚠️ OBSOLETE
- ⚠️ `.bce` command is removed in newer version.  If you're on version 2.39.0 or later, use [config guide][CONFIG GUIDE]  


# BotConfigEdit Guide

`.bce` allows you to conveniently set many of the bot-wide settings that Nadeko has, such as what the currency looks like, what people get when they use `.help`, and so on.
Below is a list of all the settings that can be set, with a quick instruction on how to use them, and their default value.

## BetflipMultiplier

The reward multiplier for correctly guessing a `.bf` (betflip) bet. Keep in mind you can't change the chance to guess the correct flip. It's always 50%.  
**Default is 1.95 (in other words, if you bet 100 and guess, you will get 195 as a reward)**

## Betroll100Multiplier

The reward multiplier for rolling 100 on `.br`.  
**Default is 10.0**

## Betroll67Multiplier

The reward multiplier for rolling 67 or higher on `.br`.  
**Default is 2.0**

## Betroll91Multiplier

The reward multiplier for rolling 91 or higher on `.br`.  
**Default is 4.0**

## GroupGreets

Toggles whether your bot will group greet/bye messages into a single message every 5 seconds.
1st user who joins will get greeted immediately
If more users join within the next 5 seconds, they will be greeted in groups of 5. This will cause %user.mention% and other placeholders to be replaced with multiple users. Keep in mind this might break some of your embeds - for example if you have %user.avatar% in the thumbnail, it will become invalid, as it will resolve to a list of avatars of grouped users.

## CurrencyGenerationPassword

Either `true` or `false` value on whether the currency spawned with `.gc` command will have a random password associated with it in the top left corner. This helps prevent people who lurk in the chat and just spam `.pick` to gain flowers.  
**Default is `false`**

## CurrencyGenerationChance

A number between 0.0 and 1.0 which represents the chance that a message sent to a channel where `.gc` is enabled will spawn currency. 0 is 0% and 1.0 is 100%.  
**Default is 0.02. (That's 2% chance)**

## CurrencyGenerationCooldown

A number of seconds that the bot is guaranteed not to spawn any flowers again after doing so in a channel where `.gc` is enabled. This is useful if you have a pretty high chance of the flowers spawning in the channel (for whatever stupid reason) and don't want the chat to be flooded with currency spawn messages.  
**Default is 10**

## CurrencyName

Name of your currency. Mostly people aren't creative with this and just call them "Credit" or "Dollar". You can do better :^)  
**Default is NadekoFlower**

## CurrencySign

Emoji of your currency. You can use server emojis, though occasionally it will fail to send it correctly on other servers.  
**Default is 🌸**

## DmHelpString

The string which will be sent whenever someone DMs the bot. Supports embeds. How it looks: [https://puu.sh/B0BLV.png](https://puu.sh/B0BLV.png)  
**Default is "Use `.h` for help"**

## HelpString

The strings which will be sent whenever someone types `.h`. Supports embeds. You can also use {0} placeholder which will be replaced with your bot's client id, and {1} placeholder which will be replaced with your bot's prefix. How it looks: [https://puu.sh/B0BMa.png](https://puu.sh/B0BMa.png)  
**Default is too long to type out (check the screenshot)**

## CurrencyDropAmount

The amount of currency which will drop when `.gc` spawn is triggered. This will be the minimum amount of currency to be spawned if CurrencyDropAmountMax is also specified.  
**Default is 1**

## CurrencyDropAmountMax

Setting this value will make currency generation spawn a random amount of currency between CurrencyDropAmount and CurrencyDropAmountMax, inclusive.  
**Default is 0**

## TriviaCurrencyReward

The amount of currency awarded to the winner of the trivia game.  
**Default is 0**

## XpPerMessage

The amount of XP the user receives when they send a message (which is not too short).  
**Default is 3**

## XpMinutesTimeout

This value represents how often the user can receive XP from sending messages.  
**Default is 5**

## MinWaifuPrice

Minimum price the users can pay to claim a waifu with `.claim`.  
**Default is 50**

## WaifuGiftMultiplier

The multiplier applied to the price of waifu gifts, making them more or less expensive.  
**Default is 1**

## MinimumTriviaWinReq

Users can't start trivia games which have smaller win requirement than specified by this setting.  
**Default is 0**

## MinBet

Minimum amount of currency a user can gamble with in a single gamble. Set 0 to disable.  
**Default is 0**

## MaxBet

Maximum amount of currency a user can gamble with in a single gamble. Set 0 to disable.  
**Default is 0**

## OkColor

Hex of the color which will show on the left side of the bot's response when a command succesfully executes. Example: [https://puu.sh/B0BXd.png](https://puu.sh/B0BXd.png)  
**Default is 00e584**

## ErrorColor

Hex of the color which will show on the left side of the bot's response when a command either errors, or you can't perform some action. Example: [https://puu.sh/B0BXs.png](https://puu.sh/B0BXs.png)  
**Default is ee281f**

## ConsoleOutputType

2 values, either 'Simple' or 'Normal'. Normal is the usual amount, and the simple one shows only basic info about the executed commands in the console. Here is a comparison: [https://puu.sh/B0Chn.png](https://puu.sh/B0Chn.png)  
**Default is 'Normal'**

## DailyCurrencyDecay

The percentage of currency all users will lose every 24 hours. The value goes between 0 and 1.0 (0 being 0% to 1.0 being 100%). This is a useful tool to control the inflation :)  
**Default is 0**

## CheckForUpdates

Whether the bot will see if there are updates available. The patch notes will be sent to Bot Owner's DM. The bot checks for updates once every 8 hours. There are 3 available values:  

- None: The bot will not check for updates
- Commit: This is useful for linux self-hosters - the bot will check for any new commit on the NadekoBot repository.
- Release: This is useful for windows self-hosters - the bot will check for any new releases published on the NadekoBot repository. This setting is also useful for linux self-hosters who only want to update when it's pretty safe to do so :)

**Default is Release**

## PatreonCurrencyPerCent

You need this only if you have a patreon page, and you've specified the PatreonCampaignId and PatreonAccessToken in credentials.json. This value is the amount of currency the users will get with `.clparew` for each cent they've pledged. Also make sure your patreon is set to charge upfront, otherwise people will be able to pledge, claim reward and unpledge without getting charged.  
**Default is 1**

## VoiceXpPerMinute
The average amount of xp added every minute to a user connected to a voice channel.  
**Default is 3**

## MaxXpMinutes
The maximum amount of time, in minutes, a user can earn xp in a voice channel. This exists mainly to clear entries out of Redis.  
**Default is 720**

[CONFIG GUIDE]: config-guide.md
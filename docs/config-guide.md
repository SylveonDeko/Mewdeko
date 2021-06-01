# Config  
`.config` is the new `.bce`, it gives you a fast and easy way to edit most bot settings/values. Use `.h .config` for explanation.  

Use `.config` to see the list of editable config files  
Use `.config <config-name>` to see the list of settable properties on that config  
Use `.config <config-name> <setting>` to see the current value and description   
Use `.config <config-name> <setting> value` to set a new value  

All settings are only available if you edit `data/[config-name].yml` files manually.  
If you edit the files manually, you can reload configuration with `.configreload <config-name>`  

The list below is not complete. Use commands above to see up-to-date list for your version.  

## XP  

`txt.cooldown` - Sets a timeout value in which a user cannot gain any more xp from sent messages. ( Value is in minutes )  
`txt.per_msg` - Sets a value for the amount of xp a user will receive from sending a message.  
`voice.per_minute` - Sets how much xp a user will receive from being active in a voice channel.   
`voice.max_minutes` - Restricts a users xp gain to a certain amount of time spent in a voice channel.  

*more settings may be available in `data/xp.yml` file*

## Games  

`trivia.min_win_req` - Restricts a user's ability to make a trivia game with a win requirement less than the set value.   
`trivia.currency_reward` - Sets the amount of currency a user will win if they place first in a completed trivia game.  

*more settings may be available in `data/games.yml` file*

## Bot  

`color.ok` - Sets a hex color that will be shown on the side bar of a successful command.  
`color.error` - Sets a hex color that will be shown on the side bar of an unsuccessful command.  
`color.pending` - Sets a hex color that will be shown on the side bar of a command that is currently in progress.  
`help.text` - The text a user is DM'd when they invoke the `.h` command.  
`help.dmtext` - The text a user will receive when they DM the bot directly.  
`console.type` - Sets the style in which commands will show up in your console, values: `Simple`, `Normal`, `None`.  
`locale` - Sets your native bot language, run the `.langli` command in a Discord channel for a full list of language options.  
`prefix` - Sets default prefix for your bot.  

*more settings may be available in `data/bot.yml` file*

## Gambling  

`currency.name` - Sets the name for your bot's currency.  
`currency.sign` - Sets the icon for your currency.  
`minbet` - Minimum amount users can bet  
`maxbet` - Maximum amount users can bet. Set 0 for unlimited  
`gen.min` - Sets the minimum amount that can be spawned with `.gc` active.   
`gen.max` - Sets the maximum amount that can be spawned with `.gc` active.   
`gen.cd` - Sets a cooldown on how often a flower can spawn with `.gc` active ( Value is in seconds ).  
`gen.chance` - Sets the likelihood that flowers will spawn with `.gc`. Value: ( 0.02 = 2% | 1 + 100% ).  
`gen.has_pw` - Toggles wether the generated flowers will have a password at the top left of the image. Value: `true` or `false`   
`bf.multi` - Sets the amount fo currency a user will win off of a winning a bet flip.  
`waifu.min_price` - Sets the minimum price a user must pay to claim a user as their waifu.  
`waifu.multi.reset` - Sets a multiplier for the `.waifureset` command.  
`waifu.multi.crush_claim` - Sets a discount for a user that is claiming another user that has their affinity set to them.  
`waifu.multi.normal_claim` - Amount a user would have to spend to claim a waifu with no affinity set.  
`waifu.multi.divorce_value` - Sets how much a user would get if they divorce a waifu.  
`waifu.multi.all_gifts` - Sets how much of a gifts value will be added to the value of the gifted waifu.  
`waifu.multi.gift_effect` - Sets a bonus amount that a waifu will receive if they have their affinity set to the gifter.  
`decay.percent` - Sets the percentage to decay all users currency daily.  
`decay.maxdecay` - Sets the maximum a amount that a user's currency can decay in a day.  
`decay.threshold` - Sets the minimum amount that a user must have to be eligible to receive a decay.  
  
*more settings may be available in `data/gambling.yml` file*
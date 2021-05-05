## Custom Reactions / Expressions

### Important

- For modifying **global** custom reactions, the ones which will work across all the servers your bot is connected to, you **must** be a Bot Owner.  
  You must also use the commands for adding, deleting and listing these reactions in a direct message with the bot.
- For modifying **local** custom reactions, the ones which will only work on the server that they are added on, it is required to have the **Administrator** permission.  
  You must also use the commands for adding, deleting and listing these reactions in the server you want the custom reactions to work on.

### Commands and Their Use

| Command Name | Description                                                                                                                                                                                                                                                                                | Example                          |
| :----------: | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------- |
|    `.acr`    | Add a custom reaction with a trigger and a response. Running this command in a server requries the Administrator permission. Running this command in DM is Bot Owner only, and adds a new global custom reaction. | `.acr "hello" Hi there, %user%!` |
|    `.lcr`    | Lists a page of global or server custom reactions (15 reactions per page). Running this command in a DM will list the global custom reactions, while running it in a server will list that server's custom reactions.                                                                      | `.lcr 1`                         |
|    `.dcr`    | Deletes a custom reaction based on the provided index. Running this command in a server requires the Administrator permission. Running this command in DM is Bot Owner only, and will delete a global custom reaction.                                                                     | `.dcr 5`                         |

#### Now that we know the commands let's take a look at an example of adding a command with `.acr`,

`.acr "Nice Weather" It sure is, %user%!`

This command can be split into two different arguments:

- The trigger, `"Nice Weather"`
- And the response, `It sure is, %user%!`

An important thing to note about the triger is that, to be more than one word, we had to wrap it with quotation marks, `"Like this"` otherwise, only the first word would have been recognised as the trigger, and the second word would have been recognised as part of the response.

There's no special requirement for the formatting of the response, so we could just write it in exactly the same way we want it to respond, albeit with a placeholder - which will be explained in this next section.

Now, if that command was ran in a server, anyone on that server can make the bot mention them, saying `It sure is, @Username` anytime they say "Nice Weather". If the command is ran in a direct message with the bot, then the custom reaction can be used on every server the bot is connected to.

### Block global Custom Reactions

If you want to disable a global custom reaction which you do not like, and you do not want to remove it, or you are not the bot owner, you can do so by adding a new Custom Reaction with the same trigger on your server, and set the response to `-`.

For example:
`.acr /o/ -`

Now if you try to trigger `/o/`, it won't print anything even if there is a global custom reaction with the same name.

### Placeholders!

To learn about placeholders, go [here](placeholders.md)

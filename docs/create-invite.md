#### Creating the Discord Bot application

This document aims to guide you through the process of creating a Discord account for your bot (the Discord Bot application), and inviting that account into your Discord server.

![img2](https://i.imgur.com/Vxxeh2n.gif)

- Go to [the Discord developer application page][DiscordApp].
- Log in with your Discord account.
- Create an application.
- On the **General Information** tab, fill out the `Name` field (it's your app's name)
- Upload an image if you want and add an app description. **(Optional)**
- Go to the **Bot** tab on the left sidebar.
- Click on the `Add a Bot` button and confirm that you do want to add a bot to this app.
- Scroll down to the `Privileged Gateway Intents` section and enable both intents. 
These are required for a number of features to function properly, and should both be on.

![img3](https://i.imgur.com/iuq2901.gif)

#### Inviting your bot to your server

![img4](https://i.imgur.com/6beUSa5.gif)

- On the **General Information** tab, copy your `Client ID` from your [applications page][DiscordApp].
- Replace the **`12345678`** in this link:
  `https://discordapp.com/oauth2/authorize?client_id=`**`12345678`**`&scope=bot&permissions=66186303` with your `Client ID`.
- The link should now look like this:
  `https://discordapp.com/oauth2/authorize?client_id=`**`YOUR_CLIENT_ID_HERE`**`&scope=bot&permissions=66186303`
- Access that newly created link, pick your Discord server, click `Authorize` and confirm with the captcha at the end.
- The bot should have been added to your server.

[Google Console]: https://console.developers.google.com
[DiscordApp]: https://discordapp.com/developers/applications/me
[Invite Guide]: https://tukimoop.pw/s/guide.html
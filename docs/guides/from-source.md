## Setting up NadekoBot From Source

| Table of Contents                                                                                                                       |
| :-------------------------------------------------------------------------------------------------------------------------------------- |
| [Installing with the CLI Installer](#installing-with-the-cli-installer) |
| [Setup (CLI)](#setup-cli)                                               |
| [Launching Nadeko (CLI)](#launching-nadeko-cli)                         |
| [Installing Nadeko Manually](#installing-nadeko-manually)               |
| [Setup](#setup)                                                         |
| [Launching Nadeko](#launching-nadeko)                                   |
| [Updating Nadeko](#updating-nadeko)                                     |

### Installing with the CLI Installer

This is the recommended way of installing Nadeko from source. If you don't want to use the installer, skip to [Installing Nadeko Manually](#installing-nadeko-manually).

#### Prerequisites (CLI)

- Windows 7 or later
    - If you are on Windows 7, you must update [PowerShell].
- [.net5 SDK][.net] (restart Windows after installation)
- [Git] (select [this option](https://i.imgur.com/zlWVTsi.png) during the installation process)
- Redis
    - Windows 64 bit: Download and install the [latest msi][Redis]. Don't forget to [add it to the path environment variable](https://i.imgur.com/uUby6Xw.png) during the installation process.
    - Windows 32 bit: Skip this step
- [Create a Discord Bot application](../../create-invite/#creating-discord-bot-application) and [invite the bot to your server](../../create-invite/#inviting-your-bot-to-your-server).

**Optional**

If you want Nadeko to play music, do the following:

- Install [Visual C++ 2010 (x86)] and [Visual C++ 2017] (both are required - restart Windows after installation)
- [youtube-dl] - Click on `Windows.exe` (on the top left corner) and download the file. Then move it to **`C:\youtube-dl`**. If the folder `youtube-dl` doesn't exist, create one.

#### Setup (CLI)

- Download the [CLI installer](https://cdn.discordapp.com/attachments/287982972664020994/416782797420888074/NadekoInstaller.zip). Move it to where you want Nadeko's files to be.
- Right click the file and extract it.
- Right click the **`NadekoInstaller.bat`** file and open it as Administrator.
- After the admin check, you should see main menu with the options below:

```
1. Download Latest Build
2. Run NadekoBot (normally)
3. Run NadekoBot with Auto Restart (check "if" nadeko is working properly, before using this)
4. Setup credentials.json
5. Install ffmpeg (for music)
6. Redis Installation (Opens Website) (64bit)
7. Run Redis (if its not running) (64bit)
8. Install Youtube-dl. (Opens Website)
9. Add Youtube-dl to PATH.
10. Add Redis to PATH. (Advanced Users Only) ("Run Redis" is enough for Normal Users.) (64bit)
11. Install .NET Core SDK (Opens Website)
12. Install Git. (Opens Website)
13. Copy libsodium and opus dll files for 32bit users. (Required for 32bit, Music)
14. Download and run redis-server for 32bit users. (32bit)
15. [NEW] NadekoBot Extensions
16. To exit
```

- Run Option `1` to download Nadeko (type 1 and press Enter). Once it's done, it should take you back to the main menu.
- Run Option `4` to [set up your credentials](../../jsons-explained). Paste the info as requested.
    - **If your Windows is 32-bit**, run Option `14` now. Otherwise, ignore this step.
- Nadeko should be ready to launch. Run Option `2` to test it out. If everything goes well, Nadeko should appear as online on your Discord server and respond to commands. Once you're done with testing, type **`.die`** to shut it down and return to the installer's main menu.

If you don't want the music features, you can launch Nadeko with Option `3` and have fun with your newly created bot. Otherwise, follow the steps below.

- If you haven't downloaded **`youtube-dl.exe`** and moved it to **`C:\youtube-dl`** yet, then do it now.
    - **If your Windows is 32-bit**, run Option `13`. Otherwise, ignore this step.
- Run Option `5` to download **`ffmpeg`**.
- Run Option `9` to add **`youtube-dl.exe`** to your system's path environment variable.
- That's it. You're done. Launch Nadeko with Option `3` and have fun sharing music with your friends.

#### Launching Nadeko (CLI)

- Just open the CLI installer and run Option `2` or `3`. Easy as that.

---

### Installing Nadeko Manually

This is the *"hard"* way of installing Nadeko. If you're here, we are assuming you know what the hell you're doing.

#### Prerequisites

- Windows 7 or later
- [.net5 SDK][.net] (restart Windows after installation)
- [Git] (select [this option](https://i.imgur.com/zlWVTsi.png) during the installation process)
- Redis
    - Windows 64 bit: Download and install the [latest msi][Redis]. Don't forget to [add it to the path environment variable](https://i.imgur.com/uUby6Xw.png) during the installation process.
    - Windows 32 bit: Download [redis-server.exe](https://github.com/MaybeGoogle/NadekoFiles/blob/master/x86%20Prereqs/redis-server.exe?raw=true) and store it somewhere accessible.
- [Create a Discord Bot application](../../jsons-explained/#creating-discord-bot-application) and [invite the bot to your server](../../jsons-explained/#inviting-your-bot-to-your-server).

**Optional**
If you want Nadeko to play music, do the following:

- [Notepad++] (makes it easier to edit your credentials)
- Install [Visual C++ 2010 (x86)] and [Visual C++ 2017] (both are required - restart Windows after installation)
- [youtube-dl] - Click on `Windows.exe` (on the top left corner) and download the file. Store it somewhere accessible.
- [ffmpeg-32bit] / [ffmpeg-64bit] - Download the version for your architecture. Extract it, then find and copy the `ffmpeg.exe` file to somewhere accessible.
- **For 32-bit Windows**, download [libsodium](https://github.com/MaybeGoogle/NadekoFiles/blob/master/x86%20Prereqs/NadekoBot_Music/libsodium.dll?raw=true) and (lib)[opus](https://github.com/MaybeGoogle/NadekoFiles/blob/master/x86%20Prereqs/NadekoBot_Music/opus.dll?raw=true) and store them somewhere accessible.

#### Setup

- Open command prompt (`cmd.exe`) and run the following command to download the source:
- `git clone -b 1.9 https://gitlab.com/Kwoth/NadekoBot`
- On Windows Explorer, go to `NadekoBot/src/NadekoBot` and edit the `credentials.json` file according to this [guide](../../jsons-explained/#setting-up-credentialsjson-file).
- Add these 2 arguments to your credentials file:

```js
    "ShardRunCommand": "dotnet",
    "ShardRunArguments": "run -c Release --no-build -- {0} {1}",
```

- Move **`youtube-dl.exe`** and **`ffmpeg.exe`** into `NadekoBot/src/NadekoBot` (or add them to your PATH environment variable, if you know how)
- **For 32-bit Windows**, replace **`libsodium.dll`** and **`opus.dll`** in `NadekoBot/src/NadekoBot` with the ones you've downloaded.

#### Launching Nadeko

- **For 32-bit Windows**, run the **`redis-server.exe`** you have downloaded. You **must** have this window open while using NadekoBot.
- On command prompt, move to the correct directory:
- `cd NadekoBot/src/NadekoBot`
- Build and run Nadeko:
- `dotnet run -c Release`
- If everything goes well, Nadeko should start up and show as online in your Discord server.

---

### Updating Nadeko

**If you have not made custom edits to the source code.**

- If you're using the CLI installer, shut your bot down and run Option `1`. That's it.
- If you've installed manually, open command prompt (`cmd.exe`)
- Move to Nadeko's root folder:
- `cd NadekoBot`
- Update Nadeko:
- `git pull`

**If you have made custom edits to the source code.**

- Open command prompt (`cmd.exe`)
- Move to Nadeko's root folder:
- `cd NadekoBot`
- Stash your changes:
- `git stash save "give me a nice name dd-mm-yyyy"` or just `git stash`
- Update Nadeko:
- `git pull`
- Apply your stash:
- `git stash apply` or `git stash apply stash@{n}` (where `n` is the ID of the stash)

Other useful commands:

- `git status` to check the changes you've made
- `git stash list` to see the list of saved stashes and their corresponding ID
- `git stash drop stash@{n}` to delete a specific stash
- `git stash pop stash@{n}` to apply and delete a specific stash

[Notepad++]: https://notepad-plus-plus.org/
[PowerShell]: https://www.microsoft.com/en-us/download/details.aspx?id=54616
[.net]: https://dotnet.microsoft.com/download/dotnet/5.0
[Redis]: https://github.com/MicrosoftArchive/redis/releases/tag/win-3.0.504
[Git]: https://git-scm.com/downloads
[Visual C++ 2010 (x86)]: https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe
[Visual C++ 2017]: https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads
[ffmpeg-32bit]: https://cdn.nadeko.bot/dl/ffmpeg-32.zip
[ffmpeg-64bit]: https://cdn.nadeko.bot/dl/ffmpeg-64.zip
[youtube-dl]: https://rg3.github.io/youtube-dl/download.html

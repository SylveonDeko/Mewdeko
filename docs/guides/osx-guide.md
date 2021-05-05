## Setting Up NadekoBot on OSX (macOS)

| Table of Contents                                       |
| :------------------------------------------------------ |
| [Prerequisites]                                         |
| [Installing Homebrew]                                   |
| [Downloading and Running Nadeko]                        |
| [Running Nadeko with the Terminal closed]      |
| [Using Nadeko with pm2 (easiest method)]                |
| [Using Nadeko with tmux (if you don't want to use pm2)] |
| [Doing a clean reinstall]                               |

#### Prerequisites

- OSX 10.12 (Sierra) or higher (needed for .NET Core 2.x).
- [Homebrew](http://brew.sh/). 
- [Create a Discord Bot application](../../create-invite/#creating-discord-bot-application) and [invite the bot to your server](../../create-invite/#inviting-your-bot-to-your-server).  
  
#### Installing Homebrew

- Open Terminal (if you don't know how to, click on the magnifying glass on the top right corner of your screen and type **Terminal** on the window that pops up).  
- Copy and paste this command, then press Enter:  
`/usr/bin/ruby -e "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install)"`


#### Installing the prerequisites

Run this command in Terminal (copy-paste the entire block):  

``` bash
brew install wget git ffmpeg openssl opus opus-tools opusfile libffi libsodium tmux python youtube-dl redis npm

brew services start redis

npm install pm2@3.1.3 -g 
```

**Installing dotNET Core SDK**

- Download [dotNET Core SDK 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1)
- Open the `.pkg` file you've downloaded and install it.
- Run this command in Terminal. There won't be any output. (copy-paste the entire block):
``` bash
ln -s /usr/local/share/dotnet/dotnet /usr/local/bin

ln -s /usr/local/opt/openssl/lib/libcrypto.1.0.0.dylib /usr/local/lib/

ln -s /usr/local/opt/openssl/lib/libssl.1.0.0.dylib /usr/local/lib/
```


#### Downloading and Running Nadeko

- Use the following command to download and run the installer. (PS: **Do not** rename the **`linuxAIO.sh`** file)  
`cd ~ && wget -N https://github.com/Kwoth/NadekoBot-BashScript/raw/1.9/linuxAIO.sh && bash linuxAIO.sh`  
- Choose Option `1` to download Nadeko. Once installation is completed you should see the options again.  
- Choose Option `5` to set up your credentials according to this [guide](../../jsons-explained/#setting-up-credentialsjson-file), or find and edit the `credentials.json` file manually.  
- Choose Option `2` to **Run Nadeko (Normally)**.  
- Check in your Discord server if your new bot is working properly. Once you're done testing, type `.die` on Discord to shut it down. The Terminal should automatically return to the main menu.


#### Running NadekoBot with the terminal closed

If you run Nadeko through option 2 or 3 and close the terminal, Nadeko will also close. To avoid this, you'll need to use a process manager that will keep it open and running in the background. This section of the guide instructs on how to achieve this with pm2 and tmux. Whether you pick one or the other, please **do not** simultaneously, or you're going to get double responses to every command.


#### Using Nadeko with pm2 (easiest method)

- pm2 will not only allow Nadeko to run in the background, but will also automatically launch Nadeko upon system reboots.
- Open the installer, if you haven't already:  
`cd ~ && wget -N https://github.com/Kwoth/NadekoBot-BashScript/raw/1.9/linuxAIO.sh && bash linuxAIO.sh`  
- Run Option `7` (ignore Option `6`, that's for Linux only).  
  - Pick whether you want to run it with or without auto-restart and auto-update.
- Once it's done, run Option `8` to exit.  
- That's it. Your bot should be running in the background. Feel free to close the Terminal window.  
  
**Managing Nadeko with pm2**  
  
- Use the following commands on Terminal to check your Nadeko setup:  
- `pm2 status` to see all pm2 processes  
- `pm2 info Nadeko` to see information about Nadeko  
- `pm2 logs Nadeko` to view real-time logs of Nadeko, or  
- `pm2 logs Nadeko --lines number` (**number** = how many lines you want to output) to see a specific amount of lines of the log. The logfile is also stored and presented at the top of these commands  
  
**Updating Nadeko with pm2**  
  
- If you have set up Nadeko with auto-update, simply run `.die` on your Discord server. That's it.  
- If you have set up Nadeko with no auto-update:  
  - Shut your bot down with `pm2 stop Nadeko`  
  - Open the installer with `bash linuxAIO.sh` and choose Option `1`  
  - Once it's done, exit the installer with Option `8` and run `pm2 restart Nadeko`  
    - You can watch your bot going online with `pm2 logs Nadeko`  
  

#### Using Nadeko with tmux (if you don't want to use pm2)

- On the Terminal, create a new session:  
- `tmux new -s nadeko`

The above command will create a new session named **nadeko** *(you can replace “nadeko” with anything you prefer, it's your session name)*.  

- Run the installer: `bash linuxAIO.sh`  
- Choose `2` to **Run NadekoBot normally**.  
    - **NOTE**: With this option, if you use `.die` on Discord, the bot will shut down and stay offline until you manually run it again.  
- Choose `3` to **Run NadekoBot with Auto Restart**.  
    - **NOTE**: With this option, the bot will auto run if you use `.die`, making it function as a restart.  

If you pick Option `3`, you will be shown the following options:  

```
1. Run Auto Restart normally without Updating.
2. Run Auto Restart and update NadekoBot.
3. Exit
```

- With Option `1`, the bot will restart on `.die` command and will not download the latest build available.  
- With Option `2`, the bot will restart and download the latest build available everytime the `.die` command is used.  

Now check your Discord server, the bot should be online.  

- To move the bot to the background, press **Control+B**, release the keys then hit **D**. That will detach the session, allowing you to finally close the terminal window and not worry about having your bot shut down in the process.  

**Updating Nadeko with tmux**  

- If you're running Nadeko with auto-update, just type `.die` in your Discord server. That's it!  
- If you're running Nadeko with **no** auto-update:  
    - Kill your previous session.  
        - Check the session name with `tmux ls`  
        - Kill with `tmux kill-session -t nadeko` (don't forget to replace "nadeko" with whatever you named your bot's session).  
    - Create a new session: `tmux new -s nadeko`  
    - Run this command: `cd ~ && wget -N https://github.com/Kwoth/NadekoBot-BashScript/raw/1.9/linuxAIO.sh && bash linuxAIO.sh`  
    - Choose Option `1` to download the most up to date version of Nadeko.  
    - Once it's done, choose Option `2` or `3` and detach the session by pressing **Control+B**, release then **D**.  

**Additional Information**  

- If you want to **see the active sessions**, run `tmux ls`. That will give you the list of the currently running sessions.  
- If you want to **switch to/see a specific session**, type `tmux a -t nadeko` (*nadeko* is the name of the session we created before so, replace it with the session name you have created).  
    - If you want to go through the log, press **Control+B**, release the keys then hit **Page Up** or **Page Down** to navigate.  
    - Don't forget to always detach from the session by pressing **Control+B** then **D** once you're done.  
- If you want **create** a new session, run `tmux new -s nadeko`. If you want to **kill it**, run `tmux kill-session -t nadeko`  
  

#### Doing a clean reinstall

- Make a backup of your credentials (`~/NadekoBot/src/NadekoBot/credentials.json`)
- Make a backup of the database (`~/NadekoBot/src/NadekoBot/bin/Release/netcoreapp2.0/data/NadekoBot.db`)
- Make a backup of the images (`~/NadekoBot/src/NadekoBot/data/images.json`)
- Delete the NadekoBot folder
- Install the bot from scratch, replace the files you backed up and run.

#### Help! My music isn't working!

Make sure you have the [Google API Key](../../jsons-explained/#setting-up-your-api-keys) in your `credentials.json`
If music still isn't working, try reinstalling ffmpeg:

- `brew update && brew upgrade` (Update formulae and Homebrew itself && Install newer versions of outdated packages)
- `brew prune` (Remove dead symlinks from Homebrewâ€™s prefix)
- `brew doctor` (Check your Homebrew installation for common issues)
- Then try `brew install ffmpeg` again.
 

[Prerequisites]: #prerequisites
[Installing Homebrew]: #installing-homebrew
[Downloading and Running Nadeko]: #downloading-and-running-nadeko
[Running Nadeko with the Terminal closed]: #running-nadekobot-with-the-terminal-closed
[Using Nadeko with pm2 (easiest method)]: #using-nadeko-with-pm2-easiest-method
[Using Nadeko with tmux (if you don't want to use pm2)]: #using-nadeko-with-tmux-if-you-dont-want-to-use-pm2
[Doing a clean reinstall]: #doing-a-clean-reinstall
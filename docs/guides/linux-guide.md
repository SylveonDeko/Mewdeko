## Setting up NadekoBot on Linux

| Table of Contents                                   |
| :-------------------------------------------------- |
| [Getting Started]                                   |
| [Downloading and Installing the Prerequisites]      |
| [Installing Nadeko]                                 |
| [Setting up, Running and Updating Nadeko with pm2]  |
| [Running Nadeko on tmux]                            |
| [Making Nadeko persist upon system restarts (tmux)] |
| [Setting up Nadeko on a VPS (Digital Ocean)]        |

#### Operating System Compatibility

It is recommended that you use **Ubuntu 16.04**, as there have been nearly no problems with it. Music features are currently not working on Debian and CentOS. Also, **32-bit systems are incompatible**.

##### Compatible operating systems:

- Ubuntu: 16.04, 18.04, 20.04
- Mint: 17, 18
- Debian: 9, 10
- CentOS: 7, 8

#### Getting Started

- Use the following command to get and run the **`linuxAIO.sh`** installer
    - (PS: **Do Not** rename the **`linuxAIO.sh`** file)

`cd ~ && wget -N https://github.com/Kwoth/NadekoBot-BashScript/raw/1.9/linuxAIO.sh && bash linuxAIO.sh`

You should see the main menu with the following options:

```
1. Download NadekoBot
2. Run Nadeko (Normally)
3. Run Nadeko with Auto Restart (Run Nadeko normally before using this.)
4. Auto-Install Prerequisites (For Ubuntu, Debian and CentOS)
5. Set up credentials.json (If you have downloaded NadekoBot already)
6. Set up pm2 for NadekoBot (see README)
7. Start Nadeko in pm2 (complete option 6 first)
8. Exit
```

#### Downloading and Installing the Prerequisites

- **If** you are running NadekoBot for the first time on your system and never had any *prerequisites* installed, press `4` and `enter` key, then `y` once you see the following:

```
Welcome to NadekoBot Auto Prerequisites Installer.
Would you like to continue?
```

- This will install all prerequisites your system needs in order to run NadekoBot.
    - (Optional) **If** you prefer to install them manually, you can try finding them [here](https://github.com/Kwoth/NadekoBot-BashScript/blob/1.9/nadekoautoinstaller.sh).

Once it finishes, the installer should automatically take you back to the main menu.

#### Installing Nadeko

- Choose Option `1` to get the **most updated build of NadekoBot**. When the installation is complete, you will see the options again.
- If you haven't [set up your Discord bot application](../../create-invite/#creating-discord-bot-application) and [invited the bot to your server](../../create-invite/#inviting-your-bot-to-your-server) yet, do it now.
    - Only the ClientID, Bot Token and OwnerID are required. Everything else is optional.
    - The Google API Key is required if you want Nadeko to play music.
- Once you have acquired them, choose Option `5` to set up your credentials.
    - You will be asked to enter your credentials. Just follow the on-screen instructions and enter them as requested. (*i.e.* If you are asked to insert the **Bot's Token**, then just copy and paste the **Bot's Token** and hit `Enter`. Rinse and repeat until it's over.)
    - If you want to skip any optional information, just press `Enter` without typing/pasting anything.

Once you're done with the credentials, you should be taken back to the main menu.

##### Checking if Nadeko is working

- Choose Option `2` to **Run Nadeko (Normally)**.
- Check in your Discord server if your new bot is working properly. Once you're done testing, type `.die` to shut it down and return to the main menu.

You can now choose Option `3` and have Nadeko run with auto restart. It will work just fine, however it's strongly advised that you use Nadeko with a process manager like pm2 or tmux, as they will keep Nadeko running in the background, freeing up your terminal for other tasks.

---

#### Setting up, Running and Updating Nadeko with [pm2](https://github.com/Unitech/pm2/blob/master/README.md) [strongly recommended]

Nadeko can be run using [pm2](https://github.com/Unitech/pm2), a process manager that seamlessly handles keeping your bot up. Besides, it handles disconnections and shutdowns gracefully, ensuring any leftover processes are properly killed. It also persists on server restart, so you can restart your server or computer and pm2 will manage the startup of your bot. Lastly, there is proper error logging and overall logging. These are just a few features of pm2, and it is a great way to run Nadeko with stability.

##### Setting up pm2/NodeJS for Nadeko

**Before proceeding, make sure your bot is not running by either running `.die` in your Discord server or exiting the process with `Ctrl+C`.**

You may be presented with the installer main menu once you shut your bot down. If not, simply run `bash linuxAIO.sh`.

- Run Option `6` to install NodeJS and pm2.
    - If you already have NodeJS and pm2 installed on your system, you can skip this step (which is a one-time thing).
- There is an automated script built in the installer so installation and startup is a breeze. Just select Option `7` to bring you to a menu of choices. These are the normal choices you have for running Nadeko.

```
[1] Start with auto-restart with .die and no auto-update.
[2] Start with auto-restart with .die and auto-update on restart as well.
[3] Run normally without any auto-restart or auto-update functionality.
```

- Simply choose one of these and Nadeko will start in pm2! If you did everything correctly, you can run the following to check your Nadeko setup:

`sudo pm2 status` to see all pm2 processes

`sudo pm2 info Nadeko` information about Nadeko

`sudo pm2 logs Nadeko` to view real-time logs of Nadeko, or

`sudo pm2 logs Nadeko --lines number` (**number** = how many lines you wish to output) to see a specific amount of lines of the log. The logfile is also stored and presented at the top of these commands

##### Updating Nadeko with pm2

- If you have set up Nadeko with auto-update, simply run `.die` on your Discord server. That's it!
- If you have set up Nadeko with **no** auto-update:
    - Shut your bot down with `sudo pm2 stop Nadeko`
    - Open the installer with `bash linuxAIO.sh` and choose Option `1`
    - Once it's done, exit the installer with Option `8` and run `sudo pm2 restart Nadeko`
        - You can watch your bot going online with `sudo pm2 logs Nadeko`

---

#### Running Nadeko on tmux [if you don't want to use pm2]

**Before proceeding, make sure your bot is not running by either running `.die` in your Discord server or exiting the process with `Ctrl+C`.**
If you are presented with the installer main menu, exit it by choosing Option `8`.

- Create a new session: `tmux new -s nadeko`

The above command will create a new session named **nadeko** *(you can replace “nadeko” with anything you prefer, it's your session name)*.

- Run the installer: `bash linuxAIO.sh`
- Choose `2` to **Run NadekoBot normally.**
    - **NOTE**: With this option, if you use `.die` in Discord, the bot will shut down and stay offline until you manually run it again.
- Choose `3` to **Run NadekoBot with Auto Restart.**
    - **NOTE**: With this option, the bot will auto run if you use `.die`, making it to function as restart.

You will be shown the following options:

```
1. Run Auto Restart normally without Updating.
2. Run Auto Restart and update NadekoBot.
3. Exit
```

- With option `1. Run Auto Restart normally without Updating`, the bot will restart on `.die` command and will not download the latest build available.
- With option `2. Run Auto Restart and update NadekoBot`, the bot will restart and download the latest build available everytime the `.die` command is used.

**Now check your Discord server, the bot should be online**

- To move the bot to the background, press **Ctrl+B**, release the keys then hit **D**. That will detach the session, allowing you to finally close the terminal window and not worry about having your bot shut down in the process.

#### Updating Nadeko

- If you're running Nadeko with auto-update, just type `.die` in your Discord server. That's it!
- If you're running Nadeko with **no** auto-update:
    - Kill your previous session.
        - Check the session name with `tmux ls`
        - Kill with `tmux kill-session -t nadeko` (don't forget to replace "nadeko" with whatever you named your bot's session).
    - Create a new session: `tmux new -s nadeko`
    - Run this command: `cd ~ && wget -N https://github.com/Kwoth/NadekoBot-BashScript/raw/1.9/linuxAIO.sh && bash linuxAIO.sh`
    - Choose Option `1` to download the most up to date version of Nadeko.
    - Once it's done, choose Option `2` or `3` and detach the session by pressing **Ctrl+B**, release then **D**.

#### Additional Information

- If you want to **see the active sessions**, run `tmux ls`. That will give you the list of the currently running sessions.
- If you want to **switch to/see a specific session**, type `tmux a -t nadeko` (**nadeko** is the name of the session we created before so, replace **“nadeko”** with the session name you have created).
    - If you want to go through the log, press **Ctrl+B**, release the keys then hit **Page Up** or **Page Down** to navigate.
    - Don't forget to always detach from the session by pressing **Ctrl+B** then **D** once you're done.
- If you want **create** a new session, run `tmux new -s nadeko`. If you want to **kill it**, run `tmux kill-session -t nadeko`

#### Making Nadeko persist upon system restarts (tmux - For Advanced Users)

This procedure is completely optional. We'll be using [*systemd*](https://en.wikipedia.org/wiki/Systemd) to handle Nadeko during system shutdowns and reboots.

**1.** Start off by downloading the necessary scripts:

- `cd ~ && wget https://raw.githubusercontent.com/Kaoticz/NadekoBot-BashScript/1.9k/nadeko.service`
- `cd ~ && wget https://raw.githubusercontent.com/Kwoth/NadekoBot-BashScript/1.9/NadekoARN.sh`
- `cd ~ && wget https://raw.githubusercontent.com/Kwoth/NadekoBot-BashScript/1.9/NadekoARU_Latest.sh`

**2.** If you **are** logged in as `root` and **don't want** Nadeko to auto-update, ignore the procedures below and go straight to step 3.

---

- Let's edit the script *systemd* is going to use to start Nadeko: `nano nadeko.service`
    - You should see the following:

```css
[Unit]
Description=NadekoBot

[Service]
WorkingDirectory=/root
User=root
Type=forking
ExecStart=/usr/bin/tmux new-session -s Nadeko -d '/bin/sh NadekoARN.sh'
ExecStop=/bin/sleep 2

[Install]
WantedBy=multi-user.target
```

- Change `/root` from *"WorkingDirectory"* to the directory that contains your NadekoBot folder.
    - For example, if your bot is located in `/home/username/NadekoBot`, you should change `/root` to `/home/username`.
- Change `root` from *"User"* to whatever username you're using.
- **Optional:** If you want Nadeko to auto-update upon restarts, change `NadekoARN.sh` to `NadekoARU_Latest.sh`.
- Once you're done, press `Ctrl+X` to exit nano, type `y` to confirm the changes and `Enter` to go back to the terminal.

---

**3.** Now the script needs to be moved to where *systemd* stores their services. On Ubuntu, it's usually in `/etc/systemd/system`. If you are not using Ubuntu and are unsure about where *systemd* stores stuff, [Google is your best friend](https://www.google.com/ "MaybeGoogle :^)").

- To do that, run this command: `sudo mv nadeko.service /etc/systemd/system/nadeko.service`

**4.** Now it's time to reload *systemd*, so it loads our new script up: `sudo systemctl daemon-reload`

**5.** Set the script to run upon system restarts: `sudo systemctl enable nadeko`

**6.** Start Nadeko on the current session: `sudo systemctl start nadeko`

And that's it. Every time your system restarts, *systemd* should automatically startup your bot with tmux. If everything has gone well, you should be able to see Nadeko on the list of processes being handled by tmux by running the `tmux ls` command.

#### Managing Nadeko on tmux with systemd

Here is a list of useful commands if you intend on managing Nadeko with *systemd*.

- `tmux ls` - lists all processes managed by tmux.
- `tmux a -t Nadeko` - shows Nadeko's log (press `Ctrl+B` then `D` to exit).
- `sudo systemctl start nadeko` - starts Nadeko, if it has been stoped.
- `sudo systemctl restart nadeko` - restarts Nadeko. Can be used while the bot is being run.
- `sudo systemctl stop nadeko` - completely shuts Nadeko down.
- `sudo systemctl enable nadeko` - makes Nadeko start automatically upon system reboots.
- `sudo systemctl disable nadeko` - stops Nadeko from starting automatically upon system reboots.
- `sudo systemctl status nadeko` - shows some information about your bot (press `Ctrl+C` to exit).

---

### Setting up Nadeko on a Linux VPS (Digital Ocean Droplet)

If you want Nadeko to play music for you 24/7 without having to hosting it on your PC and want to keep it cheap, reliable and convenient as possible, you can try Nadeko on Linux Digital Ocean Droplet using the link [DigitalOcean](http://m.do.co/c/46b4d3d44795/) (by using this link, you will get **$10 credit** and also support Nadeko)

**Setting up NadekoBot**
Assuming you have followed the link above to setup an account and a Droplet with a 64-bit operational system on Digital Ocean and got the `IP address and root password (in your e-mail)` to login, it's time to get started.

**This section is only relevant to those who want to host Nadeko on DigitalOcean. Go through this whole section before setting the bot up.**

#### Prerequisites

- Download [PuTTY](http://www.chiark.greenend.org.uk/~sgtatham/putty/download.html)
- Download [WinSCP](https://winscp.net/eng/download.php) *(optional)*
- [Create and invite the bot](../../jsons-explained/#creating-discord-bot-application).

#### Starting up

- **Open PuTTY** and paste or enter your `IP address` and then click **Open**.  
  If you entered your Droplets IP address correctly, it should show **login as:** in a newly opened window.
- Now for **login as:**, type `root` and press enter.
- It should then ask for a password. Type the `root password` you have received in your e-mail address, then press Enter.

If you are running your droplet for the first time, it will most likely ask you to change your root password. To do that, copy the **password you've received by e-mail** and paste it on PuTTY.

- To paste, just right-click the window (it won't show any changes on the screen), then press Enter.
- Type a **new password** somewhere, copy and paste it on PuTTY. Press Enter then paste it again.

**Save the new password somewhere safe.**

After that, your droplet should be ready for use. [Follow the guide from the beginning](#getting-started) to set Nadeko up on your newly created VPS.


[Getting Started]: #getting-started
[Downloading and Installing the Prerequisites]: #downloading-and-installing-the-prerequisites
[Installing Nadeko]: #installing-nadeko
[Setting up, Running and Updating Nadeko with pm2]: #setting-up-running-and-updating-nadeko-with-pm2-strongly-recommended
[Running Nadeko on tmux]: #running-nadeko-on-tmux-if-you-dont-want-to-use-pm2
[Making Nadeko persist upon system restarts (tmux)]: #making-nadeko-persist-upon-system-restarts-tmux-for-advanced-users
[Setting up Nadeko on a VPS (Digital Ocean)]: #setting-up-nadeko-on-a-linux-vps-digital-ocean-droplet

# Prerequisites 
- A Linux operating system or virtualized environment.
- PostgreSQL
- Dotnet
- Git
- Ability to properly use a terminal.

# Creating the Bot
**1. Go to [Discord Developer Portal](https://discord.dev) and login.**

**2. Click on `new application`.**

**3. Name the application and click create.**

**4. Navigate to the `Bot` tab on the siderbar.**

**5. Click reset token input 2FA code and copy the token for later.**
# Download
**1. Run this following command in a terminal (Debian/Ubuntu)**
```
sudo apt update && sudo apt install postgres postgres-contrib dotnet-sdk-8.0 git
```
**2. Download Mewdeko via `git clone -b psqldeko https://github.com/sylveondeko/mewdeko.git`.**
# Database setup
**1. Switch to the postgres user**
   ```
   sudo -i -u postgres
   ```
**2. Create a new PostgreSQL role**
   ```
   createuser --interactive
   ```
**3. Create a new database**
   ```
   createdb <dbName>
   ```
**4. Set password for the newly created role.**
   ```
   psql
     ALTER USER <username> WITH ENCRYPTED PASSWORD '<password>';
     \q
   ```
**5. Edit PostgreSQL configuration to allow password authenication.**
   - Open the configuration file.
    ```
    sudo nano /etc/postgresql/<yourVersion>/main/pg_hba.conf
    ```
   - Find the lines that look like this and change `peer` to `md5`:
   ```
   local   all             postgres                                peer
     local   all             all                                     peer
     host    all             all             127.0.0.1/32            md5
     host    all             all             ::1/128                 md5
   ```
   - Restart PostgreSQL
   ```
   sudo systemctl restart postgresql
   ```
**6. Set up the PostgreSQL connection string in `credentials.json`:**
   Format: `"PsqlConnectionString": "Server=ServerIp;Database=DatabaseName;Port=PsqlPort;UID=PsqlUser;Password=UserPassword"`
# Setup `credentials.json`.
**Follow the [credentials guide](https://mewdeko.tech/credguide)**
# Final Setup
**1. To run the bot do the command `dotnet run -c release` in terminal while inside the directory where the .csproj file (e.g., `/srv/Mewdeko/src/Mewdeko`) to shutdown the bot do .die or Ctrl+C in the terminal window optionally you can make it into a systemd service (example shown below).**

**2. Go to [Discord Developer Portal](https://discord.dev) and login.**

**3. Find the application you just created.**

**4. Navigate to the `OAuth2` tab.**

**5. Go to the `OAuth2 URL Generator` and select the scopes `bot` and `application.commands`. The URL should look like this: `https://discord.com/oauth2/authorize?client_id=<clientID>&permissions=0&integration_type=0&scope=applications.commands+bot`.**

**5. Copy the URL and paste it into the browser and invite the bot to your server.**

# (optional) Systemd service setup.
**1. Run `dotnet publish -c release` in the directory where Mewdeko.csproj is located.**
**2. Run `sudo nano /etc/systemd/system/mewdeko.service` and paste the service file shown below.**
**3. To start Mewdeko, run `sudo systemctl start mewdeko`, to restart run `sudo systemctl restart mewdeko`, and to stop run `sudo systemctl stop mewdeko`>**


```
[Unit]
Description=Mewdeko
After=network.target

[Service]
WorkingDirectory=/path/to/mewdeko
ExecStart=/usr/bin/dotnet /path/to/Mewdeko.dll 
ExecStop=/bin/kill $MAINPID
Killsignal=SIGINT
TimeoutStopSec=10
User=<yourUsername>
Group=<yourGroup>

[Install]
WantedBy=multi-user.target
```

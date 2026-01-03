# Self-Hosting Mewdeko on Linux

This guide covers how to set up and run your own instance of Mewdeko on Linux.

## Automated Installation

For a streamlined installation experience, use the community-maintained bash script:

**[Mewdeko-BashScript](https://github.com/StrangeRanger/Mewdeko-BashScript)**

This script handles dependency installation, building, and service configuration automatically.

## Manual Installation

If you prefer to install manually or the script does not support your distribution, follow the steps below.

### Prerequisites

#### Required Software

1. **.NET 10 SDK**

   **Debian/Ubuntu:**
   ```bash
   wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 10.0
   ```

   Add to your PATH in `~/.bashrc` or `~/.zshrc`:
   ```bash
   export DOTNET_ROOT=$HOME/.dotnet
   export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
   ```

   **Fedora:**
   ```bash
   sudo dnf install dotnet-sdk-10.0
   ```

   **Arch Linux:**
   ```bash
   sudo pacman -S dotnet-sdk
   ```

2. **PostgreSQL**

   **Debian/Ubuntu:**
   ```bash
   sudo apt update
   sudo apt install postgresql postgresql-contrib
   sudo systemctl start postgresql
   sudo systemctl enable postgresql
   ```

   **Fedora:**
   ```bash
   sudo dnf install postgresql-server postgresql-contrib
   sudo postgresql-setup --initdb
   sudo systemctl start postgresql
   sudo systemctl enable postgresql
   ```

   **Arch Linux:**
   ```bash
   sudo pacman -S postgresql
   sudo -u postgres initdb -D /var/lib/postgres/data
   sudo systemctl start postgresql
   sudo systemctl enable postgresql
   ```

3. **Redis**

   **Debian/Ubuntu:**
   ```bash
   sudo apt install redis-server
   sudo systemctl start redis-server
   sudo systemctl enable redis-server
   ```

   **Fedora:**
   ```bash
   sudo dnf install redis
   sudo systemctl start redis
   sudo systemctl enable redis
   ```

   **Arch Linux:**
   ```bash
   sudo pacman -S redis
   sudo systemctl start redis
   sudo systemctl enable redis
   ```

4. **Git**

   **Debian/Ubuntu:**
   ```bash
   sudo apt install git
   ```

   **Fedora:**
   ```bash
   sudo dnf install git
   ```

   **Arch Linux:**
   ```bash
   sudo pacman -S git
   ```

5. **Build Dependencies**

   **Debian/Ubuntu:**
   ```bash
   sudo apt install build-essential libopus-dev libsodium-dev ffmpeg
   ```

   **Fedora:**
   ```bash
   sudo dnf install gcc-c++ opus-devel libsodium-devel ffmpeg
   ```

   **Arch Linux:**
   ```bash
   sudo pacman -S base-devel opus libsodium ffmpeg
   ```

6. **Lavalink** (for music features)

   Requires Java 17 or higher:

   **Debian/Ubuntu:**
   ```bash
   sudo apt install openjdk-17-jre
   ```

   **Fedora:**
   ```bash
   sudo dnf install java-17-openjdk
   ```

   **Arch Linux:**
   ```bash
   sudo pacman -S jre17-openjdk
   ```

   Download Lavalink from [GitHub Releases](https://github.com/lavalink-devs/Lavalink/releases)

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/Sylveon76/Mewdeko.git
   cd Mewdeko
   ```

2. Build the project:
   ```bash
   dotnet build src/Mewdeko/Mewdeko.csproj -c Release
   ```

3. Navigate to the output directory:
   ```bash
   cd src/Mewdeko/bin/Release/net10.0/
   ```

### Database Setup

1. Switch to the postgres user:
   ```bash
   sudo -u postgres psql
   ```

2. Create a database and user:
   ```sql
   CREATE DATABASE mewdeko;
   CREATE USER mewdeko WITH ENCRYPTED PASSWORD 'your_secure_password';
   GRANT ALL PRIVILEGES ON DATABASE mewdeko TO mewdeko;
   \c mewdeko
   GRANT ALL ON SCHEMA public TO mewdeko;
   \q
   ```

3. Note your connection string:
   ```
   Host=localhost;Port=5432;Database=mewdeko;Username=mewdeko;Password=your_secure_password
   ```

### Configuration

1. Copy the example credentials file:
   ```bash
   cp credentials_example.json credentials.json
   ```

2. Edit `credentials.json` with your preferred editor:
   ```bash
   nano credentials.json
   ```

3. Configure the required fields:

   **Required:**
   - `Token`: Your Discord bot token from the [Discord Developer Portal](https://discord.com/developers/applications)
   - `PsqlConnectionString`: Your PostgreSQL connection string
   - `OwnerIds`: Array of Discord user IDs who will have owner-level access

   **Recommended:**
   - `GoogleApiKey`: For YouTube search
   - `SpotifyClientId` and `SpotifyClientSecret`: For Spotify integration
   - `LastFmApiKey`: For Last.fm integration

4. For a detailed credentials guide, see: https://blog.mewdeko.tech/credentials-guide/

### Running the Bot

#### Direct Execution

```bash
dotnet Mewdeko.dll
```

Or from the project root:
```bash
dotnet run --project src/Mewdeko/Mewdeko.csproj -c Release
```

#### Running with systemd (Recommended for Production)

1. Create a systemd service file:
   ```bash
   sudo nano /etc/systemd/system/mewdeko.service
   ```

2. Add the following content (adjust paths as needed):
   ```ini
   [Unit]
   Description=Mewdeko Discord Bot
   After=network.target postgresql.service redis.service

   [Service]
   Type=simple
   User=your_username
   WorkingDirectory=/home/your_username/Mewdeko/src/Mewdeko/bin/Release/net10.0
   ExecStart=/usr/bin/dotnet Mewdeko.dll
   Restart=always
   RestartSec=10

   [Install]
   WantedBy=multi-user.target
   ```

3. Enable and start the service:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable mewdeko
   sudo systemctl start mewdeko
   ```

4. Check status:
   ```bash
   sudo systemctl status mewdeko
   ```

5. View logs:
   ```bash
   journalctl -u mewdeko -f
   ```

### Lavalink Setup (Optional, for Music)

1. Download Lavalink.jar to a directory of your choice

2. Create `application.yml` in the same directory (see [Lavalink documentation](https://lavalink.dev/configuration/))

3. Create a systemd service for Lavalink:
   ```bash
   sudo nano /etc/systemd/system/lavalink.service
   ```

   ```ini
   [Unit]
   Description=Lavalink Audio Server
   After=network.target

   [Service]
   Type=simple
   User=your_username
   WorkingDirectory=/path/to/lavalink
   ExecStart=/usr/bin/java -jar Lavalink.jar
   Restart=always
   RestartSec=10

   [Install]
   WantedBy=multi-user.target
   ```

4. Enable and start:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable lavalink
   sudo systemctl start lavalink
   ```

## Updating

1. Stop the bot:
   ```bash
   sudo systemctl stop mewdeko
   ```

2. Pull the latest changes:
   ```bash
   cd ~/Mewdeko
   git pull
   ```

3. Rebuild:
   ```bash
   dotnet build src/Mewdeko/Mewdeko.csproj -c Release
   ```

4. Start the bot:
   ```bash
   sudo systemctl start mewdeko
   ```

Database migrations are handled automatically on startup.

## Troubleshooting

### Common Issues

**Permission denied errors:**
- Ensure the user running the bot has read/write access to the working directory
- Check PostgreSQL user permissions

**Database connection errors:**
- Verify PostgreSQL is running: `sudo systemctl status postgresql`
- Check pg_hba.conf allows local connections
- Verify the connection string in credentials.json

**Redis connection errors:**
- Verify Redis is running: `sudo systemctl status redis`
- Check Redis is listening on the configured address

**Music not working:**
- Verify Lavalink is running: `sudo systemctl status lavalink`
- Check Java is installed: `java -version`
- Verify LavalinkUrl in credentials.json

### Getting Help

- Join the [Discord Server](https://discord.gg/mewdeko) for community support
- Open an issue on [GitHub](https://github.com/Sylveon76/Mewdeko/issues) for bugs

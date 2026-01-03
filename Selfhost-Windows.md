# Self-Hosting Mewdeko on Windows

This guide covers how to set up and run your own instance of Mewdeko on Windows.

## Prerequisites

### Required Software

1. **.NET 10 SDK**
   - Download from [Microsoft .NET Downloads](https://dotnet.microsoft.com/download/dotnet/10.0)
   - Verify installation by running `dotnet --version` in Command Prompt

2. **PostgreSQL**
   - Download from [PostgreSQL Downloads](https://www.postgresql.org/download/windows/)
   - During installation, note down the password you set for the postgres user
   - Default port is 5432

3. **Redis**
   - Download from [Memurai](https://www.memurai.com/) (Redis-compatible for Windows) or use WSL with Redis
   - Alternative: [Redis Windows Archive](https://github.com/microsoftarchive/redis/releases) (older, but functional)

4. **Lavalink** (for music features)
   - Requires Java 17 or higher
   - Download Lavalink from the [official repository](https://github.com/lavalink-devs/Lavalink/releases)

5. **Git** (optional, for cloning)
   - Download from [Git for Windows](https://git-scm.com/download/win)

### Optional Software

- **Visual Studio 2022** or **VS Code** for development
- **pgAdmin** for database management
- **Notepad++** or another text editor for editing configuration files

## Installation Methods

### Method 1: Using Pre-built Releases (Recommended)

1. Download the latest Windows release from [GitHub Releases](https://github.com/Sylveon76/Mewdeko/releases)

2. Extract the ZIP file to your desired location (e.g., `C:\Mewdeko`)

3. Navigate to the extracted folder

4. Continue to the [Configuration](#configuration) section

### Method 2: Building from Source

1. Clone the repository:
   ```
   git clone https://github.com/Sylveon76/Mewdeko.git
   cd Mewdeko
   ```

2. Build the project:
   ```
   dotnet build src/Mewdeko/Mewdeko.csproj -c Release
   ```

3. The built files will be in `src/Mewdeko/bin/Release/net10.0/`

## Configuration

### Database Setup

1. Open pgAdmin or connect to PostgreSQL via command line

2. Create a new database for Mewdeko:
   ```sql
   CREATE DATABASE mewdeko;
   ```

3. Note your connection string format:
   ```
   Host=localhost;Port=5432;Database=mewdeko;Username=postgres;Password=YOUR_PASSWORD
   ```

### Credentials Setup

1. Navigate to the Mewdeko folder

2. Copy `credentials_example.json` to `credentials.json`

3. Edit `credentials.json` with your configuration:

   **Required fields:**
   - `Token`: Your Discord bot token from the [Discord Developer Portal](https://discord.com/developers/applications)
   - `PsqlConnectionString`: Your PostgreSQL connection string
   - `OwnerIds`: Array of Discord user IDs who will have owner-level access to the bot

   **Recommended fields:**
   - `GoogleApiKey`: For YouTube search functionality
   - `SpotifyClientId` and `SpotifyClientSecret`: For Spotify integration
   - `LastFmApiKey`: For Last.fm integration
   - `TwitchClientId` and `TwitchClientSecret`: For Twitch stream notifications

4. For a detailed credentials guide, see: https://blog.mewdeko.tech/credentials-guide/

### Redis Setup

1. Start Redis/Memurai service

2. Verify Redis is running on `127.0.0.1:6379` (default)

3. If using a different address, update `RedisConnections` in `credentials.json`

### Lavalink Setup (Optional, for Music)

1. Download Lavalink.jar from the releases page

2. Create an `application.yml` configuration file (see Lavalink documentation)

3. Start Lavalink:
   ```
   java -jar Lavalink.jar
   ```

4. Update `LavalinkUrl` in `credentials.json` if not using default (`http://localhost:2334`)

## Running the Bot

### Direct Execution

1. Navigate to the Mewdeko folder

2. Run:
   ```
   Mewdeko.exe
   ```

   Or if built from source:
   ```
   dotnet run --project src/Mewdeko/Mewdeko.csproj
   ```

### Running as a Windows Service (Advanced)

For persistent operation, you can set up Mewdeko as a Windows service using tools like NSSM (Non-Sucking Service Manager).

## Troubleshooting

### Common Issues

**Bot won't start:**
- Verify your Discord token is correct
- Check that PostgreSQL is running
- Check that Redis is running
- Review the console output for error messages

**Database connection errors:**
- Verify PostgreSQL is running
- Check your connection string format
- Ensure the database exists

**Music not working:**
- Verify Lavalink is running
- Check the LavalinkUrl in credentials.json
- Ensure Java is installed for Lavalink

### Getting Help

- Join the [Discord Server](https://discord.gg/mewdeko) for community support
- Open an issue on [GitHub](https://github.com/Sylveon76/Mewdeko/issues) for bugs

## Updating

1. Download the latest release or pull the latest changes if using git

2. Stop the currently running bot

3. Replace the old files with the new ones (keep your `credentials.json`)

4. Start the bot again

Database migrations are handled automatically on startup.

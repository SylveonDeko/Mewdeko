# Mewdeko

A feature-rich, customizable Discord bot built with .NET and Discord.Net.

[![Discord Server](https://discordapp.com/api/guilds/843489716674494475/widget.png)](https://discord.gg/mewdeko)
[![License: AGPL v3](https://img.shields.io/badge/license-AGPLv3-pink)](https://opensource.org/licenses/AGPL-3.0)
[![Add to Discord](https://img.shields.io/badge/discord-add%20mewdeko-pink)](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands)
[![top.gg](https://img.shields.io/badge/top.gg-mewdeko-pink)](https://top.gg/bot/752236274261426212)

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/B0B03QN1K)

## Overview

Mewdeko is an open-source Discord bot designed to provide server management, moderation, entertainment, and community engagement tools. It offers extensive customization options, allowing server administrators to tailor the bot's behavior to their specific needs.

The bot is built on .NET 10 and uses PostgreSQL for data persistence, Redis for caching, and Lavalink for music playback.

## Features

### Moderation
- Warning systems with configurable actions and thresholds
- Raid protection with configurable triggers (clustered joins, new accounts)
- Message cleanup and purge commands
- User banning, kicking, muting, and timeout management
- Global ban list support for cross-server moderation

### Server Management
- Role management with bulk operations
- Channel creation and permission management
- Sticky messages that persist at the bottom of channels
- Repeating scheduled messages
- Emote management and stealing from other servers

### Greetings and Welcomes
- MultiGreet system supporting up to 30 different greeting configurations
- Multiple greetings per channel (up to 5)
- Webhook support for custom greeting avatars
- Auto-delete options for greeting messages
- Role-based greeting triggers

### Community Features
- Starboard for highlighting popular messages
- Suggestions system with voting and status tracking
- Confession system for anonymous posts
- Ticket system for support channels
- Reputation system for user recognition
- XP and leveling system with customizable rewards

### Entertainment
- Music playback with support for YouTube, Spotify, SoundCloud, and more
- Games including trivia, hangman, tic-tac-toe, and connect four
- Anime and manga lookup via AniList and MyAnimeList
- Image search and manipulation
- Roleplay action commands

### Utility
- AFK system with customizable messages and return notifications
- Message sniping for deleted and edited messages
- Reminders and to-do lists
- User profiles with customizable information
- Stream notifications for Twitch integration
- RSS feed subscriptions
- Weather and timezone lookups

### Customization
- Chat triggers for custom responses based on patterns
- Fine-grained permission system for all commands
- Custom currency system
- Birthday tracking and announcements
- Highlights for keyword notifications

## Getting Started

### Using the Hosted Instance

The easiest way to use Mewdeko is to invite the hosted instance to your server:

[Invite Mewdeko](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands)

### Self-Hosting

For those who want to run their own instance:

#### Requirements
- .NET 10 SDK
- PostgreSQL database
- Redis server
- Lavalink server (for music features)

#### Installation

**Linux:**
Follow the bash script installer: [Mewdeko-BashScript](https://github.com/StrangeRanger/Mewdeko-BashScript)

**Windows:**
See the [Windows self-host guide](Selfhost-Windows.md)

#### Configuration

1. Copy `credentials_example.json` to `credentials.json`
2. Add your Discord bot token and other required API keys
3. Configure your database connection string
4. Start the bot

## Documentation

- [Commands List](https://mewdeko.tech/commands)
- [Self-Host Guide (Windows)](Selfhost-Windows.md)
- [Self-Host Guide (Linux)](Selfhost-Linux.md)

## Contributing

Contributions are welcome. Please read the [Contributing Guide](CONTRIBUTING.md) and [Style Guide](STYLE_GUIDE.md) before submitting pull requests.

All contributions are licensed under the AGPLv3. By contributing, you agree that your code will be available under this license.

## Support

- [Discord Server](https://discord.gg/mewdeko)
- [GitHub Issues](https://github.com/Sylveon76/Mewdeko/issues)

## License

Mewdeko is licensed under the [GNU Affero General Public License v3.0](LICENSE.md).

This means:
- You must provide source code (or a link to your fork) for any modified version you deploy
- Modifications must also be licensed under AGPLv3
- If you run a modified Mewdeko instance, you must make the source available to users

## Code of Conduct

Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

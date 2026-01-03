# Contributing to Mewdeko

Thank you for your interest in contributing to Mewdeko. This document outlines the process for contributing to the project.

## Ways to Contribute

- Reporting bugs
- Suggesting features
- Submitting code fixes
- Improving documentation
- Reviewing pull requests

## Development Workflow

### Getting Started

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Mewdeko.git
   cd Mewdeko
   ```
3. Add the upstream repository:
   ```bash
   git remote add upstream https://github.com/Sylveon76/Mewdeko.git
   ```
4. Create a new branch for your changes:
   ```bash
   git checkout -b feature/your-feature-name
   ```

### Making Changes

1. Make your changes in your feature branch
2. Follow the [code style guidelines](STYLE_GUIDE.md)
3. Test your changes locally
4. Commit your changes with clear, descriptive commit messages

### Submitting a Pull Request

1. Push your branch to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```
2. Open a pull request against the `main` branch of the upstream repository
3. Fill in the pull request template with relevant information
4. Wait for review and address any feedback

### Pull Request Guidelines

- Keep pull requests focused on a single change
- Update documentation if your changes affect user-facing behavior
- Ensure the build passes before requesting review
- Allow maintainers to make edits to your pull request

## Reporting Bugs

Report bugs by [opening a new issue](https://github.com/Sylveon76/Mewdeko/issues/new).

A good bug report includes:

- A clear, descriptive title
- Steps to reproduce the issue
- Expected behavior
- Actual behavior
- Environment details (OS, .NET version, etc.)
- Relevant logs or screenshots
- Any error messages

Please redact sensitive information (tokens, passwords, personal data) from logs and screenshots before submitting.

## Suggesting Features

Feature requests are welcome. Open an issue describing:

- The problem you are trying to solve
- Your proposed solution
- Alternative solutions you have considered
- Any additional context

## Code Review Process

All submissions require review. Maintainers will:

- Review your code for correctness and style
- Suggest improvements or request changes
- Merge approved changes

## Setting Up the Development Environment

### Prerequisites

- .NET 10 SDK
- PostgreSQL
- Redis
- Your preferred IDE (Visual Studio, VS Code, Rider)

### Building

```bash
dotnet build src/Mewdeko/Mewdeko.csproj
```

### Running Tests

```bash
dotnet test
```

### Code Formatting

The project uses EditorConfig for consistent formatting. Most IDEs will automatically apply these settings. You can also format code manually:

```bash
dotnet format
```

## Project Structure

```
Mewdeko/
  src/
    Mewdeko/           # Main bot project
      Modules/         # Command modules organized by feature
      Services/        # Background services and utilities
      Database/        # Database context and entities
      Extensions/      # Extension methods
    MewdekoSourceGen/  # Source generators
```

## License

By contributing, you agree that your contributions will be licensed under the [GNU Affero General Public License v3.0](LICENSE.md).

### What This Means

- Your code will be open source under AGPLv3
- Anyone running a modified version must make their source code available
- You retain copyright of your contributions

### AGPL Compliance for Deployments

If you deploy a modified version of Mewdeko:

- You must provide a link to your source code repository
- The link should be accessible to users of your bot (e.g., in a command or server channel)
- Configuration files, credentials, and databases are excluded from this requirement

## Community

- [Discord Server](https://discord.gg/mewdeko) for discussion and support
- [GitHub Issues](https://github.com/Sylveon76/Mewdeko/issues) for bug reports and feature requests

## Questions

If you have questions about contributing, feel free to ask in the Discord server or open a discussion on GitHub.

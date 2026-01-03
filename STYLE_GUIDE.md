# Code Style Guide

This document describes the coding standards and conventions used in the Mewdeko project. The project uses EditorConfig to enforce most of these rules automatically.

## General Formatting

### Indentation and Whitespace

- Use **spaces** for indentation, not tabs
- Use **4 spaces** for C# files
- Use **2 spaces** for JSON, XML, YAML, and web files (HTML, CSS, JS)
- Trim trailing whitespace
- Use CRLF line endings for most files, LF for shell scripts

### File Encoding

- Use UTF-8 encoding for all files

## C# Conventions

### Type Keywords

Use language keywords instead of framework type names:

```csharp
// Good
int count = 0;
string name = "test";

// Avoid
Int32 count = 0;
String name = "test";
```

### Implicit Typing

Use `var` when the type is apparent:

```csharp
// Good
var users = new List<User>();
var name = "test";
var count = GetCount();

// Also acceptable when type is not immediately obvious
User user = GetUser();
```

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes, Structs, Enums | PascalCase | `UserService`, `GuildConfig` |
| Interfaces | PascalCase with I prefix | `IUserService`, `IGuildService` |
| Methods | PascalCase | `GetUser()`, `SendMessage()` |
| Properties | PascalCase | `UserId`, `GuildName` |
| Public/Internal Fields | PascalCase | `MaxRetries`, `DefaultTimeout` |
| Private/Protected Fields | camelCase | `userId`, `guildName` |
| Private Readonly Fields | camelCase | `userService`, `logger` |
| Constants | PascalCase | `MaxUsers`, `DefaultPrefix` |
| Parameters | camelCase | `userId`, `guildId` |
| Local Variables | camelCase | `userCount`, `result` |

### Braces

Opening braces go on a new line (Allman style):

```csharp
// Good
public void DoSomething()
{
    if (condition)
    {
        // code
    }
}

// Avoid
public void DoSomething() {
    if (condition) {
        // code
    }
}
```

Single-line statements may omit braces:

```csharp
// Acceptable
if (condition)
    return;

// Also acceptable
if (condition)
{
    return;
}
```

### Modifiers

Order modifiers as follows:

```csharp
public static readonly string DefaultValue = "test";
private async Task DoSomethingAsync() { }
protected internal virtual void ProcessData() { }
```

Order: `public`, `private`, `protected`, `internal`, `static`, `extern`, `new`, `virtual`, `abstract`, `sealed`, `override`, `readonly`, `unsafe`, `volatile`, `async`

### Using Directives

- Sort `System` namespaces first
- Place using directives at the top of the file

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Services;
```

### Expression-Bodied Members

Expression-bodied members are permitted but not required:

```csharp
// Both are acceptable
public string Name => _name;

public string Name
{
    get { return _name; }
}
```

### Null Handling

Use null-conditional and null-coalescing operators:

```csharp
// Good
var name = user?.Name ?? "Unknown";
var length = items?.Count ?? 0;

// Avoid
var name = user != null ? user.Name : "Unknown";
```

### Pattern Matching

Use pattern matching where appropriate:

```csharp
// Good
if (obj is string text)
{
    Console.WriteLine(text);
}

// Avoid
if (obj is string)
{
    var text = (string)obj;
    Console.WriteLine(text);
}
```

## Code Organization

### File Structure

Each file should generally contain one type. Related types may be grouped in the same file if they are small and closely related.

### Region Usage

Avoid using `#region` directives. If a class is large enough to need regions, consider splitting it into smaller classes.

### Method Length

Keep methods focused and reasonably sized. If a method exceeds 50 lines, consider refactoring into smaller methods.

## Async/Await

### Naming

Async methods should be suffixed with `Async`:

```csharp
public async Task<User> GetUserAsync(ulong userId)
{
    // implementation
}
```

### ConfigureAwait

In library code, use `ConfigureAwait(false)` unless you need to capture the synchronization context:

```csharp
var result = await SomeOperationAsync().ConfigureAwait(false);
```

## Error Handling

### Exceptions

- Throw specific exceptions, not generic `Exception`
- Include meaningful error messages
- Avoid catching generic `Exception` unless re-throwing

```csharp
// Good
if (user == null)
    throw new ArgumentNullException(nameof(user));

// Avoid
if (user == null)
    throw new Exception("User is null");
```

## Documentation

### XML Documentation

Public APIs should have XML documentation:

```csharp
/// <summary>
/// Gets a user by their Discord ID.
/// </summary>
/// <param name="userId">The Discord user ID.</param>
/// <returns>The user if found; otherwise, null.</returns>
public async Task<User?> GetUserAsync(ulong userId)
{
    // implementation
}
```

### Comments

- Write comments for complex logic that is not immediately obvious
- Avoid comments that simply restate what the code does
- Keep comments up to date with code changes

## Project-Specific Conventions

### Discord.Net

- Use `ulong` for Discord IDs (user, guild, channel, message, etc.)
- Prefer `IUser`, `IGuild`, `IChannel` interfaces over concrete types when possible

### Database

- Entity classes go in the `Database/Models` directory
- Use meaningful names for database entities and properties

### Modules

- Command modules go in `Modules/{FeatureName}/`
- Group related commands in the same module
- Use separate files for slash commands (prefix with `Slash`)

## Applying These Standards

### EditorConfig

The project includes an `.editorconfig` file that enforces many of these rules. Most IDEs (Visual Studio, VS Code, Rider) will automatically apply these settings.

### Code Formatting

Run the .NET formatter to fix formatting issues:

```bash
dotnet format
```

### IDE Settings

Configure your IDE to:
- Apply EditorConfig settings automatically
- Format on save (optional but recommended)
- Show whitespace characters when helpful

using System.Text.RegularExpressions;
using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders.Interactions;

/// <summary>
/// Class that converts a string to an array of IUser objects.
/// </summary>
public partial class UserArrayConverter : TypeConverter<IUser[]>
{
    /// <summary>
    /// Returns the Discord type of the option.
    /// </summary>
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    /// <summary>
    /// Converts the given string to an array of IUser objects.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="input">The string to convert.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the conversion result.</returns>
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context,
        IApplicationCommandInteractionDataOption input, IServiceProvider services)
    {
        var option = input.Value as string;
        var userStrings = option.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var guild = context.Guild as SocketGuild;
        var users = new List<IUser>();
        var userRegex = UserRegex();

        foreach (var userString in userStrings)
        {
            IUser user;

            // Match user mention or ID
            if (userRegex.Match(userString) is { Success: true } match &&
                ulong.TryParse(match.Groups[1].Value, out var userId) || ulong.TryParse(userString, out userId))
                user = guild.GetUser(userId);
            // Match user name
            else
                user = guild.Users.FirstOrDefault(u =>
                    string.Equals(u.Username, userString, StringComparison.OrdinalIgnoreCase));

            if (user != null)
                users.Add(user);
            else
                return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ParseFailed,
                    $"User {userString} not found."));
        }

        return Task.FromResult(TypeConverterResult.FromSuccess(users.ToArray()));
    }

    /// <summary>
    /// Writes the properties of the option.
    /// </summary>
    /// <param name="properties">The properties of the option.</param>
    /// <param name="parameter">The parameter information.</param>
    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Description = "Mention name and ID can be used. Seperate users with a space.";
    }

    /// <summary>
    /// Generates a regular expression for matching user mentions or IDs.
    /// </summary>
    /// <returns>A compiled regular expression.</returns>
    [GeneratedRegex("<@!?([0-9]+)>")]
    private static partial Regex UserRegex();
}
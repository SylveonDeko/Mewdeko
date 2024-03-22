using System.Text.RegularExpressions;
using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders.Interactions;

/// <summary>
/// Class that converts a string to an array of IRole objects.
/// </summary>
public partial class RoleArrayConverter : TypeConverter<IRole[]>
{
    /// <summary>
    /// Returns the Discord type of the option.
    /// </summary>
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    /// <summary>
    /// Converts the given string to an array of IRole objects.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="input">The string to convert.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the conversion result.</returns>
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context,
        IApplicationCommandInteractionDataOption input, IServiceProvider services)
    {
        var option = input.Value as string;
        var roleStrings = option.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var guildUser = context.User as SocketGuildUser;
        var roles = new List<IRole>();
        var roleRegex = RoleRegex();

        foreach (var roleString in roleStrings)
        {
            IRole role;

            // Match role mention or ID
            if (roleRegex.Match(roleString) is { Success: true } match &&
                ulong.TryParse(match.Groups[1].Value, out var roleId) || ulong.TryParse(roleString, out roleId))
                role = guildUser.Guild.GetRole(roleId);
            // Match role name
            else
                role = guildUser.Guild.Roles.FirstOrDefault(r =>
                    string.Equals(r.Name, roleString, StringComparison.OrdinalIgnoreCase));

            if (role != null)
                roles.Add(role);
            else
                return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ParseFailed,
                    $"Role {roleString} not found."));
        }

        return Task.FromResult(TypeConverterResult.FromSuccess(roles.ToArray()));
    }

    /// <summary>
    /// Writes the properties of the option.
    /// </summary>
    /// <param name="properties">The properties of the option.</param>
    /// <param name="parameter">The parameter information.</param>
    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Description = "Mention name and ID can be used. Seperate roles with a space.";
    }

    /// <summary>
    /// Generates a regular expression for matching role mentions or IDs.
    /// </summary>
    /// <returns>A compiled regular expression.</returns>
    [GeneratedRegex("<@&([0-9]+)>")]
    private static partial Regex RoleRegex();
}
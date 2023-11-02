using System.Text.RegularExpressions;
using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders.Interactions;

public partial class RoleArrayConverter : TypeConverter<IRole[]>
{
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;


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

    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Description = "Mention name and ID can be used. Seperate roles with a space.";
    }

    [GeneratedRegex("<@&([0-9]+)>")]
    private static partial Regex RoleRegex();
}
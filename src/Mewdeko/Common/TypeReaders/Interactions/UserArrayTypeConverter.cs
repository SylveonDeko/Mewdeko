using System.Text.RegularExpressions;
using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders.Interactions;

public partial class UserArrayConverter : TypeConverter<IUser[]>
{
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

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

    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Description = "Mention name and ID can be used. Seperate users with a space.";
    }

    [GeneratedRegex("<@!?([0-9]+)>")]
    private static partial Regex UserRegex();
}
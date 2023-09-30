using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders.Interactions;

public class StatusRolesTypeConverter : TypeConverter<StatusRolesTable>
{
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;


    public override async Task<TypeConverterResult> ReadAsync(IInteractionContext context,
        IApplicationCommandInteractionDataOption input, IServiceProvider services)
    {
        var option = input.Value as string;
        var cache = services.GetService(typeof(IDataCache)) as IDataCache;
        var statusRoles = await cache.GetStatusRoleCache();

        if (statusRoles == null)
            return TypeConverterResult.FromError(InteractionCommandError.ParseFailed, "StatusRoles cache is null.");

        var statusRole = statusRoles.FirstOrDefault(x => x.GuildId == context.Guild.Id && x.Status == option);

        return statusRole != null
            ? TypeConverterResult.FromSuccess(statusRole)
            : TypeConverterResult.FromError(InteractionCommandError.ParseFailed, $"StatusRole {option} not found.");
    }

    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Description = "The statusrole to look at/modify.";
    }
}
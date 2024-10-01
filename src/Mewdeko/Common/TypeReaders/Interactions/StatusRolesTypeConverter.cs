using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders.Interactions;

/// <summary>
///     Class that converts a string to a StatusRolesTable object.
/// </summary>
public class StatusRolesTypeConverter : TypeConverter<StatusRolesTable>
{
    /// <summary>
    ///     Returns the Discord type of the option.
    /// </summary>
    public override ApplicationCommandOptionType GetDiscordType()
    {
        return ApplicationCommandOptionType.String;
    }

    /// <summary>
    ///     Converts the given string to a StatusRolesTable object.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="input">The string to convert.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the conversion result.</returns>
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

    /// <summary>
    ///     Writes the properties of the option.
    /// </summary>
    /// <param name="properties">The properties of the option.</param>
    /// <param name="parameter">The parameter information.</param>
    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Description = "The statusrole to look at/modify.";
    }
}
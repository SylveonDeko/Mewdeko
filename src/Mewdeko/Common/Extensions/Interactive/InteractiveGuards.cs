using Discord;

namespace Mewdeko.Common.Extensions.Interactive
{
    internal static class InteractiveGuards
    {
        public static void NotNull<T>(T obj, string argumentName) where T : class
        {
            if (obj is null) throw new ArgumentNullException(argumentName);
        }

        public static void MessageFromCurrentUser(IDiscordClient client, IUserMessage message)
        {
            if (message is null) return;

            if (message.Author.Id != client.CurrentUser.Id)
                throw new ArgumentException("Message author must be the current user.", nameof(message));
        }

        public static void DeleteAndDisableInputNotSet(ActionOnStop action, string parameterName)
        {
            if (action.HasFlag(ActionOnStop.DeleteMessage)) return;

            if (action.HasFlag(ActionOnStop.DeleteInput | ActionOnStop.DisableInput))
                throw new ArgumentException(
                    $"{ActionOnStop.DeleteInput} and {ActionOnStop.DisableInput} are mutually exclusive.",
                    parameterName);
        }

#if !DNETLABS
        public static void CanUseComponents<TOption>(IInteractiveElement<TOption> element)
        {
            if (element.InputType == InputType.Buttons || element.InputType == InputType.SelectMenus)
            {
                throw new NotSupportedException("Discord.Net does not support components (yet). Use Discord.Net Labs.");
            }
        }
#endif
    }
}
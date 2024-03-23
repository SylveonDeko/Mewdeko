namespace Mewdeko.Modules.Chat_Triggers.Common
{
    /// <summary>
    /// Represents exported chat triggers.
    /// </summary>
    public class ExportedTriggers
    {
        /// <summary>
        /// Gets or sets the reactions associated with the trigger.
        /// </summary>
        public string[]? React;

        /// <summary>
        /// Gets or sets the roles to be added by the trigger.
        /// </summary>
        public List<ulong> ARole = new();

        /// <summary>
        /// Gets or sets the roles to be removed by the trigger.
        /// </summary>
        public List<ulong> RRole = new();

        // Properties for backwards compatibility with NadekoBot

        /// <summary>
        /// Gets or sets the ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the response.
        /// </summary>
        public string Res { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether auto-delete is enabled for the trigger.
        /// </summary>
        public bool Ad { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the trigger allows targeting.
        /// </summary>
        public bool At { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the trigger contains anywhere.
        /// </summary>
        public bool Ca { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the trigger has a direct message response.
        /// </summary>
        public bool Dm { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the trigger is a regular expression.
        /// </summary>
        public bool Rgx { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the trigger reacts to the trigger.
        /// </summary>
        public bool Rtt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the trigger has no response.
        /// </summary>
        public bool Nr { get; set; }

        /// <summary>
        /// Gets or sets the type of role grant for the trigger.
        /// </summary>
        public CtRoleGrantType Rgt { get; set; }

        /// <summary>
        /// Gets or sets the valid trigger types for the trigger.
        /// </summary>
        public ChatTriggerType VTypes { get; set; } = ChatTriggerType.Message;

        /// <summary>
        /// Gets or sets the application command name for the trigger.
        /// </summary>
        public string AcName { get; set; } = "";

        /// <summary>
        /// Gets or sets the application command description for the trigger.
        /// </summary>
        public string AcDesc { get; set; } = "";

        /// <summary>
        /// Gets or sets the application command type for the trigger.
        /// </summary>
        public CtApplicationCommandType Act { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the response is ephemeral.
        /// </summary>
        public bool Eph { get; set; }

        /// <summary>
        /// Converts a <see cref="Database.Models.ChatTriggers"/> object to an <see cref="ExportedTriggers"/> object.
        /// </summary>
        /// <param name="ct">The <see cref="Database.Models.ChatTriggers"/> object.</param>
        /// <returns>The converted <see cref="ExportedTriggers"/> object.</returns>
        public static ExportedTriggers FromModel(Database.Models.ChatTriggers ct) =>
            new()
            {
                Id = "",
                Res = ct.Response,
                Ad = ct.AutoDeleteTrigger == 1,
                At = ct.AllowTarget == 1,
                Ca = ct.ContainsAnywhere == 1,
                Dm = ct.DmResponse == 1,
                Rgx = ct.IsRegex == 1,
                React = string.IsNullOrWhiteSpace(ct.Reactions)
                    ? null
                    : ct.GetReactions(),
                Rtt = ct.ReactToTrigger == 1,
                Nr = ct.NoRespond == 1,
                RRole = ct.GetRemovedRoles(),
                ARole = ct.GetGrantedRoles(),
                Rgt = ct.RoleGrantType,
                VTypes = ct.ValidTriggerTypes,
                AcName = ct.ApplicationCommandName,
                AcDesc = ct.ApplicationCommandDescription,
                Act = ct.ApplicationCommandType,
                Eph = ct.EphemeralResponse == 1
            };
    }
}
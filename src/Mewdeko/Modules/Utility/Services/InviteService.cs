using CommandLine;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for managing invites.
///     Implements the INService interface.
/// </summary>
public class InviteService : INService
{
    /// <summary>
    ///     Options for the InviteService commands.
    ///     Implements the IMewdekoCommandOptions interface.
    /// </summary>
    public class Options : IMewdekoCommandOptions
    {
        /// <summary>
        ///     Gets or sets the maximum number of times the invite can be used.
        /// </summary>
        [Option('m', "max-uses", Required = false, Default = 0,
            HelpText = "Maximum number of times the invite can be used. Default 0 (never).")]
        public int MaxUses { get; set; }

        /// <summary>
        ///     Gets or sets the flag for unique invites.
        /// </summary>
        [Option('u', "unique", Required = false, Default = false,
            HelpText =
                "Not setting this flag will result in bot getting the existing invite with the same settings if it exists, instead of creating a new one.")]
        public bool Unique { get; set; } = false;

        /// <summary>
        ///     Gets or sets the flag for temporary invites.
        /// </summary>
        [Option('t', "temporary", Required = false, Default = false,
            HelpText = "If this flag is set, the user will be kicked from the guild once they close their client.")]
        public bool Temporary { get; set; } = false;

        /// <summary>
        ///     Gets or sets the time for the invite to expire.
        /// </summary>
        [Option('e', "expire", Required = false, Default = 0,
            HelpText = "Time in seconds to expire the invite. Default 0 (no expiry).")]
        public int Expire { get; set; }

        /// <summary>
        ///     Normalizes the options for the invite.
        /// </summary>
        public void NormalizeOptions()
        {
            if (MaxUses < 0)
                MaxUses = 0;

            if (Expire < 0)
                Expire = 0;
        }
    }
}
using Discord.Interactions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

/// <summary>
/// Partial class for server administration slash commands.
/// </summary>
[Group("administration", "Server administration stuffs")]
public partial class SlashAdministration : MewdekoSlashModuleBase<AdministrationService>;
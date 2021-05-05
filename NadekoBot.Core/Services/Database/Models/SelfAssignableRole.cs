namespace NadekoBot.Core.Services.Database.Models
{
    public class SelfAssignedRole : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong RoleId { get; set; }
        
        public int Group { get; set; }
        public int LevelRequirement { get; set; }
    }
}

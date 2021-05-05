namespace NadekoBot.Core.Services.Database.Models
{
    public class SlowmodeIgnoredRole : DbEntity
    {
        public ulong RoleId { get; set; }

        // override object.Equals
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return ((SlowmodeIgnoredRole)obj).RoleId == RoleId;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return RoleId.GetHashCode();
        }
    }
}

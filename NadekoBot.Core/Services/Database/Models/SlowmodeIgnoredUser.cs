namespace NadekoBot.Core.Services.Database.Models
{
    public class SlowmodeIgnoredUser : DbEntity
    {
        public ulong UserId { get; set; }

        // override object.Equals
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return ((SlowmodeIgnoredUser)obj).UserId == UserId;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }
    }
}

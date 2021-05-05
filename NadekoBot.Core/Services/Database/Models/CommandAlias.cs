namespace NadekoBot.Core.Services.Database.Models
{
    public class CommandAlias : DbEntity
    {
        public string Trigger { get; set; }
        public string Mapping { get; set; }

        //// override object.Equals
        //public override bool Equals(object obj)
        //{
        //    if (obj == null || GetType() != obj.GetType())
        //    {
        //        return false;
        //    }

        //    return ((CommandAlias)obj).Trigger.Trim().ToLowerInvariant() == Trigger.Trim().ToLowerInvariant();
        //}

        //// override object.GetHashCode
        //public override int GetHashCode()
        //{
        //    return Trigger.Trim().ToLowerInvariant().GetHashCode();
        //}
    }
}

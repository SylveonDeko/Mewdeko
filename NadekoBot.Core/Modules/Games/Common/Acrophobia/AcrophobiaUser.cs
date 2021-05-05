namespace NadekoBot.Modules.Games.Common.Acrophobia
{
    public class AcrophobiaUser
    {
        public string UserName { get; }
        public ulong UserId { get; }
        public string Input { get; }

        public AcrophobiaUser(ulong userId, string userName, string input)
        {
            this.UserName = userName;
            this.UserId = userId;
            this.Input = input;
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is AcrophobiaUser x
                ? x.UserId == this.UserId
                : false;
        }
    }
}

namespace Mewdeko.Modules.Games.Common.Acrophobia;

public class AcrophobiaUser
{
    public AcrophobiaUser(ulong userId, string userName, string input)
    {
        UserName = userName;
        UserId = userId;
        Input = input;
    }

    public string UserName { get; }
    public ulong UserId { get; }
    public string Input { get; }

    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is AcrophobiaUser x
            ? x.UserId == UserId
            : false;
    }
}
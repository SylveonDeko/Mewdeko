namespace Mewdeko.Modules.Utility.Common.Patreon;

public class Attributes
{
    public int AmountCents { get; set; }
    public string CreatedAt { get; set; }
    public object DeclinedSince { get; set; }
    public bool IsTwitchPledge { get; set; }
    public bool PatronPaysFees { get; set; }
    public int? PledgeCapCents { get; set; }
}

public class Address
{
    public object Data { get; set; }
}

public class Data
{
    public string Id { get; set; }
    public string Type { get; set; }
}

public class Links
{
    public string Related { get; set; }
}

public class Creator
{
    public Data Data { get; set; }
    public Links Links { get; set; }
}

public class Patron
{
    public Data Data { get; set; }
    public Links Links { get; set; }
}

public class Reward
{
    public Data Data { get; set; }
    public Links Links { get; set; }
}

public class Relationships
{
    public Address Address { get; set; }
    public Creator Creator { get; set; }
    public Patron Patron { get; set; }
    public Reward Reward { get; set; }
}

public class PatreonPledge
{
    public Attributes Attributes { get; set; }
    public string Id { get; set; }
    public Relationships Relationships { get; set; }
    public string Type { get; set; }
}
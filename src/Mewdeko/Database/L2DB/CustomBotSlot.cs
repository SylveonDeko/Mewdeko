using LinqToDB.Mapping;
using System;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel
{
	[Table("CustomBotSlots")]
	public class CustomBotSlot
	{
		[Column("Id"                , IsPrimaryKey = true , IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id                { get; set; } // integer
		[Column("UserId"                                                                                                )] public ulong?    UserId            { get; set; } // numeric(20,0)
		[Column("KofiEmail"                                                                                             )] public string?   KofiEmail         { get; set; } // text
		[Column("Source"            , CanBeNull = false                                                                 )] public string    Source            { get; set; } = "kofi"; // text
		[Column("KofiTransactionId"                                                                                     )] public string?   KofiTransactionId { get; set; } // text
		[Column("ExpiresAt"                                                                                             )] public DateTime? ExpiresAt         { get; set; } // timestamp (6) without time zone
		[Column("InstanceId"                                                                                            )] public int?      InstanceId        { get; set; } // integer
		[Column("StoppedAt"                                                                                             )] public DateTime? StoppedAt         { get; set; } // timestamp (6) without time zone
		[Column("DateAdded"         , SkipOnInsert = true, SkipOnUpdate = true                                          )] public DateTime  DateAdded         { get; set; } // timestamp (6) without time zone
	}

	[Table("CustomBotSecrets")]
	public class CustomBotSecret
	{
		[Column("InstanceId"    , IsPrimaryKey = true                                  )] public int      InstanceId     { get; set; } // integer
		[Column("EncryptedToken", CanBeNull = false                                    )] public string   EncryptedToken { get; set; } = null!; // text
		[Column("ApiPort"                                                              )] public int      ApiPort        { get; set; } // integer
		[Column("ContainerName" , CanBeNull = false                                    )] public string   ContainerName  { get; set; } = null!; // text
		[Column("DateAdded"     , SkipOnInsert = true, SkipOnUpdate = true             )] public DateTime DateAdded      { get; set; } // timestamp (6) without time zone
	}

	[Table("KofiPayments")]
	public class KofiPayment
	{
		[Column("Id"                        , IsPrimaryKey = true , IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int      Id                         { get; set; } // integer
		[Column("KofiTransactionId"         , CanBeNull = false                                                               )] public string   KofiTransactionId          { get; set; } = null!; // text
		[Column("Email"                     , CanBeNull = false                                                               )] public string   Email                      { get; set; } = null!; // text
		[Column("Type"                      , CanBeNull = false                                                               )] public string   Type                       { get; set; } = null!; // text
		[Column("Amount"                                                                                                      )] public string?  Amount                     { get; set; } // text
		[Column("IsSubscriptionPayment"                                                                                       )] public bool     IsSubscriptionPayment      { get; set; } // boolean
		[Column("IsFirstSubscriptionPayment"                                                                                  )] public bool     IsFirstSubscriptionPayment { get; set; } // boolean
		[Column("SlotId"                                                                                                      )] public int?     SlotId                     { get; set; } // integer
		[Column("PaidAt"                                                                                                      )] public DateTime PaidAt                     { get; set; } // timestamp (6) without time zone
		[Column("DateAdded"                 , SkipOnInsert = true, SkipOnUpdate = true                                        )] public DateTime DateAdded                  { get; set; } // timestamp (6) without time zone
	}
}

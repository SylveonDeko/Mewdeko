using LinqToDB.Mapping;
using System;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel
{
	[Table("GuildOwnership")]
	public class GuildOwnership
	{
		[Column("Id"        , IsPrimaryKey = true , IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int      Id         { get; set; } // integer
		[Column("GuildId"                                                                                      )] public ulong    GuildId    { get; set; } // numeric(20,0)
		[Column("InstanceId"                                                                                   )] public int      InstanceId { get; set; } // integer
		[Column("DateAdded" , SkipOnInsert = true, SkipOnUpdate = true                                         )] public DateTime DateAdded  { get; set; } // timestamp (6) without time zone
	}
}

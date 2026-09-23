using LinqToDB.Mapping;
using System;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel
{
	[Table("WordOfTheDayConfigs")]
	public class WordOfTheDayConfig
	{
		[Column("Id"             , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id              { get; set; } // integer
		[Column("GuildId"                                                                                          )] public ulong     GuildId         { get; set; } // numeric(20,0)
		[Column("ChannelId"                                                                                        )] public ulong?    ChannelId       { get; set; } // numeric(20,0)
		[Column("Enabled"                                                                                          )] public bool      Enabled         { get; set; } // boolean
		[Column("PostHour"                                                                                         )] public int       PostHour        { get; set; } // integer
		[Column("Timezone"                                                                                         )] public string    Timezone        { get; set; } = "UTC"; // text
		[Column("PingRoleId"                                                                                       )] public ulong?    PingRoleId      { get; set; } // numeric(20,0)
		[Column("MessageTemplate"                                                                                  )] public string?   MessageTemplate { get; set; } // text
		[Column("Topic"                                                                                            )] public string?   Topic           { get; set; } // text
		[Column("PartOfSpeech"                                                                                     )] public int       PartOfSpeech    { get; set; } // integer
		[Column("Difficulty"                                                                                       )] public int       Difficulty      { get; set; } // integer
		[Column("SourceMode"                                                                                       )] public int       SourceMode      { get; set; } // integer
		[Column("LastPostedDate"                                                                                   )] public DateTime? LastPostedDate  { get; set; } // date
		[Column("DateAdded"                                                                                        )] public DateTime  DateAdded       { get; set; } // timestamp (6) without time zone
		[Column("DateModified"                                                                                     )] public DateTime  DateModified    { get; set; } // timestamp (6) without time zone
	}
}

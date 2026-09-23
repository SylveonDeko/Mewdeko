using LinqToDB.Mapping;
using System;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel
{
	[Table("WordOfTheDaySchedules")]
	public class WordOfTheDaySchedule
	{
		[Column("Id"          , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int      Id           { get; set; } // integer
		[Column("GuildId"                                                                                       )] public ulong    GuildId      { get; set; } // numeric(20,0)
		[Column("RuleType"                                                                                      )] public int      RuleType     { get; set; } // integer
		[Column("RuleKey"                                                                                       )] public int      RuleKey      { get; set; } // integer
		[Column("Topic"                                                                                         )] public string?  Topic        { get; set; } // text
		[Column("PartOfSpeech"                                                                                  )] public int?     PartOfSpeech { get; set; } // integer
		[Column("Difficulty"                                                                                    )] public int?     Difficulty   { get; set; } // integer
		[Column("DateAdded"                                                                                     )] public DateTime DateAdded    { get; set; } // timestamp (6) without time zone
	}
}

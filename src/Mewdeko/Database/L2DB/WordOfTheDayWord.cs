using LinqToDB.Mapping;
using System;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel
{
	[Table("WordOfTheDayWords")]
	public class WordOfTheDayWord
	{
		[Column("Id"          , IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)] public int       Id           { get; set; } // integer
		[Column("GuildId"                                                                                       )] public ulong     GuildId      { get; set; } // numeric(20,0)
		[Column("Word"                                                                                          )] public string    Word         { get; set; } = null!; // text
		[Column("PartOfSpeech"                                                                                  )] public string?   PartOfSpeech { get; set; } // text
		[Column("Definition"                                                                                    )] public string?   Definition   { get; set; } // text
		[Column("Example"                                                                                       )] public string?   Example      { get; set; } // text
		[Column("AddedBy"                                                                                       )] public ulong     AddedBy      { get; set; } // numeric(20,0)
		[Column("TimesUsed"                                                                                     )] public int       TimesUsed    { get; set; } // integer
		[Column("LastUsed"                                                                                      )] public DateTime? LastUsed     { get; set; } // timestamp (6) without time zone
		[Column("DateAdded"                                                                                     )] public DateTime  DateAdded    { get; set; } // timestamp (6) without time zone
	}
}

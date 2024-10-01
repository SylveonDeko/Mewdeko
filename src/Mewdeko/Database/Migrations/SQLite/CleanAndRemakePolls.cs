﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class CleanAndRemakePolls : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "PRAGMA foreign_keys=off; delete from PollAnswer; delete from Poll; DELETE from PollVote; PRAGMA foreign_keys=on;");
        migrationBuilder.AddColumn<int>("PollType", "Poll");
    }
}
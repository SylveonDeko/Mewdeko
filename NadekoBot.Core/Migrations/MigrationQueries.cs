namespace NadekoBot.Migrations
{
    internal class MigrationQueries
    {
        public static string UserClub { get; } = @"
CREATE TABLE DiscordUser_tmp(
    Id INTEGER PRIMARY KEY,
    AvatarId TEXT,
    Discriminator TEXT,
    UserId INTEGER UNIQUE NOT NULL,
    DateAdded TEXT,
    Username TEXT
);

INSERT INTO DiscordUser_tmp
    SELECT Id, AvatarId, Discriminator, UserId, DateAdded, Username
    FROM DiscordUser;

DROP TABLE DiscordUser;

CREATE TABLE DiscordUser(
    Id INTEGER PRIMARY KEY,
    AvatarId TEXT,
    Discriminator TEXT,
    UserId INTEGER UNIQUE NOT NULL,
    DateAdded TEXT,
    Username TEXT,
    ClubId INTEGER,
    CONSTRAINT FK_DiscordUser_Clubs_ClubId FOREIGN KEY(ClubId) REFERENCES Clubs(Id) ON DELETE RESTRICT
);

INSERT INTO DiscordUser
    SELECT Id, AvatarId, Discriminator, UserId, DateAdded, Username, NULL
    FROM DiscordUser_tmp;

DROP TABLE DiscordUser_tmp;";
        public static string TotalXp { get; } =
@"UPDATE DiscordUser
SET TotalXp = ifnull((SELECT SUM(Xp) FROM UserXpStats WHERE UserId = DiscordUser.UserId), 0)";

    }
}
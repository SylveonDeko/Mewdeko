using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Games.Services;

public sealed class GamesConfigService : ConfigServiceBase<GamesConfig>
{
    private new const string FilePath = "data/games.yml";
    private static readonly TypedKey<GamesConfig> changeKey = new("config.games.updated");

    public GamesConfigService(IConfigSeria serializer, IPubSub pubSub)
        : base(FilePath, serializer, pubSub, changeKey)
    {
        AddParsedProp("trivia.min_win_req", gs => gs.Trivia.MinimumWinReq, int.TryParse,
            ConfigPrinters.ToString, val => val > 0);
        AddParsedProp("trivia.currency_reward", gs => gs.Trivia.CurrencyReward, long.TryParse,
            ConfigPrinters.ToString, val => val >= 0);
    }

    public override string Name { get; } = "games";
}

// public sealed class GamesConfigMigrator : IConfigMigrator
// {
//         //     private readonly DbService _db;
//     private readonly GamesConfigService _gss;
//
//     public GamesConfigMigrator(DbService dbService, GamesConfigService gss)
//     {
//         _log = LogManager.GetCurrentClassLogger();
//         _db = dbService;
//         _gss = gss;
//     }
//
//     public void EnsureMigrated()
//     {
//         using var uow = _db.GetDbContext();
//         using var conn = uow._context.Database.GetDbConnection();
//         MigrateRaceAnimals(conn);
//         MigrateEightBall(conn);
//     }
//     
//     private void MigrateTrivia(DbConnection conn)
//     {
//         using (var checkTableCommand = conn.CreateCommand())
//         {
//             // make sure table still exists
//             checkTableCommand.CommandText =
//                 "SELECT name FROM sqlite_master WHERE type='table' AND name='BotConfig';";
//             var checkReader = checkTableCommand.ExecuteReader();
//             if (!checkReader.HasRows)
//                 return;
//         }
//
//         Log.Information("Migrating trivia...");
//
//         using var com = conn.CreateCommand();
//         com.CommandText = $@"SELECT MinimumTriviaWinReq, TriviaCurrencyReward FROM BotConfig";
//         using var reader = com.ExecuteReader();
//
//         if (!reader.Read())
//             return;
//         
//         _gss.ModifyConfig(ModifyTriviaAction(reader));
//
//         Log.Information("Trivia config migrated to data/games.yml");
//     }
//     
//     private static Action<GamesConfig> ModifyTriviaAction(DbDataReader reader)
//         => realConfig =>
//         {
//             var val = (int) (long) reader["MinimumTriviaWinReq"];
//             realConfig.Trivia.MinimumWinReq = val <= 0 ? 1 : val;
//             realConfig.Trivia.CurrencyReward = (long) reader["TriviaCurrencyReward"];
//         };
//
//     private void MigrateEightBall(DbConnection conn)
//     {
//         using (var checkTableCommand = conn.CreateCommand())
//         {
//             // make sure table still exists
//             checkTableCommand.CommandText =
//                 "SELECT name FROM sqlite_master WHERE type='table' AND name='EightBallResponses';";
//             var checkReader = checkTableCommand.ExecuteReader();
//             if (!checkReader.HasRows)
//                 return;
//         }
//
//         try
//         {
//             using (var com = conn.CreateCommand())
//             {
//                 com.CommandText = $@"SELECT Text FROM EightBallResponses";
//                 using var reader = com.ExecuteReader();
//
//                 if (!reader.Read())
//                     return;
//
//                 Log.Information("Migrating eightball...");
//                 _gss.ModifyConfig(Modify8ballAction(reader));
//             }
//
//             Log.Information("Eightball migrated to data/games.yml");
//             MigrateTrivia(conn);
//         }
//         finally
//         {
//
//             using var deleteEightBallCommand = conn.CreateCommand();
//             deleteEightBallCommand.CommandText = "DROP TABLE IF EXISTS EightBallResponses";
//             deleteEightBallCommand.ExecuteNonQuery();
//         }
//
//     }
//
//     private static Action<GamesConfig> Modify8ballAction(DbDataReader reader)
//         => realConfig =>
//         {
//             realConfig.EightBallResponses.Clear();
//             do
//             {
//                 realConfig.EightBallResponses.Add((string) reader["Text"]);
//             } while (reader.Read());
//         };
//
//     private void MigrateRaceAnimals(DbConnection conn)
//     {
//         using (var checkTableCommand = conn.CreateCommand())
//         {
//             // make sure table still exists
//             checkTableCommand.CommandText =
//                 "SELECT name FROM sqlite_master WHERE type='table' AND name='RaceAnimals';";
//             var checkReader = checkTableCommand.ExecuteReader();
//             if (!checkReader.HasRows)
//                 return;
//         }
//
//         using (var com = conn.CreateCommand())
//         {
//             com.CommandText = $@"SELECT Name, Icon FROM RaceAnimals";
//             using var reader = com.ExecuteReader();
//             
//             if (!reader.Read())
//                 return;
//             
//             Log.Information("Migrating race animals...");
//             _gss.ModifyConfig(ModifyRaceAnimalsAction(reader));
//         }
//
//         Log.Information("Race animals migrated to data/games.yml");
//
//         using var deleteRaceAnimalsCommand = conn.CreateCommand();
//         deleteRaceAnimalsCommand.CommandText = "DROP TABLE IF EXISTS RaceAnimals";
//         deleteRaceAnimalsCommand.ExecuteNonQuery();
//     }
//
//     private static Action<GamesConfig> ModifyRaceAnimalsAction(DbDataReader reader)
//         => realConfig =>
//         {
//             realConfig.RaceAnimals.Clear();
//
//             do
//             {
//                 realConfig.RaceAnimals.Add(new RaceAnimal()
//                 {
//                     Icon = (string) reader["Icon"],
//                     Name = (string) reader["Name"]
//                 });
//             } while (reader.Read());
//         };
// }
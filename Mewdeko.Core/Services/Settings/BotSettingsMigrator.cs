using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using Mewdeko.Core.Common.Configs;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Fluent;
using SixLabors.ImageSharp.PixelFormats;

namespace Mewdeko.Core.Services
{
    public class BotSettingsMigrator
    {
        private readonly BotSettingsService _bss;
        private readonly DbService _db;
        private readonly Logger _log;

        public BotSettingsMigrator(DbService dbService, BotSettingsService bss)
        {
            _log = LogManager.GetCurrentClassLogger();
            _db = dbService;
            _bss = bss;
        }

        public void EnsureMigrated()
        {
            using (var uow = _db.GetDbContext())
            {
                var conn = uow._context.Database.GetDbConnection();
                MigrateBotConfig(conn);
            }
        }

        private void MigrateBotConfig(DbConnection conn)
        {
            using var checkTableCommand = conn.CreateCommand();

            // make sure table still exists
            checkTableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='BotConfig';";
            var checkReader = checkTableCommand.ExecuteReader();
            if (!checkReader.HasRows)
                return;

            using var checkMigratedCommand = conn.CreateCommand();
            checkMigratedCommand.CommandText =
                "UPDATE BotConfig SET HasMigratedBotSettings = 1 WHERE HasMigratedBotSettings = 0;";
            var changedRows = checkMigratedCommand.ExecuteNonQuery();
            if (changedRows == 0)
                return;

            _log.Info("Migrating bot settings...");

            var blockedCommands = new HashSet<string>();
            using var cmdCom = conn.CreateCommand();
            cmdCom.CommandText = "SELECT Name from BlockedCmdOrMdl WHERE BotConfigId != NULL";
            var cmdReader = cmdCom.ExecuteReader();
            while (cmdReader.Read())
                blockedCommands.Add(cmdReader.GetString(0));

            var blockedModules = new HashSet<string>();
            using var mdlCom = conn.CreateCommand();
            mdlCom.CommandText = "SELECT Name from BlockedCmdOrMdl WHERE BotConfigId is NULL";
            var mdlReader = mdlCom.ExecuteReader();
            while (mdlReader.Read())
                blockedModules.Add(mdlReader.GetString(0));

            using var com = conn.CreateCommand();
            com.CommandText = @"SELECT DefaultPrefix, ForwardMessages, ForwardToAllOwners,
OkColor, ErrorColor, ConsoleOutputType, DMHelpString, HelpString, RotatingStatuses, Locale, GroupGreets
FROM BotConfig";

            using var reader = com.ExecuteReader();
            if (!reader.Read())
                return;

            _bss.ModifyConfig(x =>
            {
                x.Prefix = reader.GetString(0);
                x.ForwardMessages = reader.GetBoolean(1);
                x.ForwardToAllOwners = reader.GetBoolean(2);
                x.Color = new ColorConfig
                {
                    Ok = Rgba32.TryParseHex(reader.GetString(3), out var okColor)
                        ? okColor
                        : Rgba32.ParseHex("00e584"),
                    Error = Rgba32.TryParseHex(reader.GetString(4), out var errorColor)
                        ? errorColor
                        : Rgba32.ParseHex("ee281f")
                };
                x.ConsoleOutputType = (ConsoleOutputType) reader.GetInt32(5);
                x.DmHelpText = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                x.HelpText = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                x.RotateStatuses = reader.GetBoolean(8);
                try
                {
                    x.DefaultLocale = new CultureInfo(reader.GetString(9));
                }
                catch
                {
                    x.DefaultLocale = new CultureInfo("en-US");
                }

                x.GroupGreets = reader.GetBoolean(10);
                x.Blocked.Commands = blockedCommands;
                x.Blocked.Modules = blockedModules;
            });

            Log.Info("Data written to data/bot.yml");
        }
    }
}
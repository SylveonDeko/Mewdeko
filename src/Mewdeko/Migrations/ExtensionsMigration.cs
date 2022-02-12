// using Microsoft.EntityFrameworkCore.Migrations;
//
// namespace Mewdeko.Migrations;
//
// public partial class ExtensionsMigration : Migration
// {
//     protected override void Up(MigrationBuilder migrationBuilder)
//     {
//         migrationBuilder.CreateTable(
//             name: "WaifuInfo",
//             columns: table => new
//             {
//                 Id = table.Column<int>(type: "INTEGER", nullable: false)
//                           .Annotation("Sqlite:Autoincrement", true),
//                 WaifuId = table.Column<int>(type: "INTEGER", nullable: false),
//                 ClaimerId = table.Column<int>(type: "INTEGER", nullable: true),
//                 AffinityId = table.Column<int>(type: "INTEGER", nullable: true),
//                 Price = table.Column<int>(type: "INTEGER", nullable: false),
//                 DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true)
//             },
//             constraints: table =>
//             {
//                 table.PrimaryKey("PK_WaifuInfo", x => x.Id);
//                 table.ForeignKey(
//                     name: "FK_WaifuInfo_DiscordUser_AffinityId",
//                     column: x => x.AffinityId,
//                     principalTable: "DiscordUser",
//                     principalColumn: "Id",
//                     onDelete: ReferentialAction.Restrict);
//                 table.ForeignKey(
//                     name: "FK_WaifuInfo_DiscordUser_ClaimerId",
//                     column: x => x.ClaimerId,
//                     principalTable: "DiscordUser",
//                     principalColumn: "Id",
//                     onDelete: ReferentialAction.Restrict);
//                 table.ForeignKey(
//                     name: "FK_WaifuInfo_DiscordUser_WaifuId",
//                     column: x => x.WaifuId,
//                     principalTable: "DiscordUser",
//                     principalColumn: "Id",
//                     onDelete: ReferentialAction.Cascade);
//             });
//         
//          migrationBuilder.CreateTable(
//                 name: "WaifuUpdates",
//                 columns: table => new
//                 {
//                     Id = table.Column<int>(type: "INTEGER", nullable: false)
//                         .Annotation("Sqlite:Autoincrement", true),
//                     UserId = table.Column<int>(type: "INTEGER", nullable: false),
//                     UpdateType = table.Column<int>(type: "INTEGER", nullable: false),
//                     OldId = table.Column<int>(type: "INTEGER", nullable: true),
//                     NewId = table.Column<int>(type: "INTEGER", nullable: true),
//                     DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true)
//                 },
//                 constraints: table =>
//                 {
//                     table.PrimaryKey("PK_WaifuUpdates", x => x.Id);
//                     table.ForeignKey(
//                         name: "FK_WaifuUpdates_DiscordUser_NewId",
//                         column: x => x.NewId,
//                         principalTable: "DiscordUser",
//                         principalColumn: "Id",
//                         onDelete: ReferentialAction.Restrict);
//                     table.ForeignKey(
//                         name: "FK_WaifuUpdates_DiscordUser_OldId",
//                         column: x => x.OldId,
//                         principalTable: "DiscordUser",
//                         principalColumn: "Id",
//                         onDelete: ReferentialAction.Restrict);
//                     table.ForeignKey(
//                         name: "FK_WaifuUpdates_DiscordUser_UserId",
//                         column: x => x.UserId,
//                         principalTable: "DiscordUser",
//                         principalColumn: "Id",
//                         onDelete: ReferentialAction.Cascade);
//                 });
//
//             migrationBuilder.CreateTable(
//                 name: "WaifuItem",
//                 columns: table => new
//                 {
//                     Id = table.Column<int>(type: "INTEGER", nullable: false)
//                         .Annotation("Sqlite:Autoincrement", true),
//                     WaifuInfoId = table.Column<int>(type: "INTEGER", nullable: true),
//                     ItemEmoji = table.Column<string>(type: "TEXT", nullable: true),
//                     Name = table.Column<string>(type: "TEXT", nullable: true),
//                     Price = table.Column<int>(type: "INTEGER", nullable: false),
//                     Item = table.Column<int>(type: "INTEGER", nullable: false),
//                     DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true)
//                 },
//                 constraints: table =>
//                 {
//                     table.PrimaryKey("PK_WaifuItem", x => x.Id);
//                     table.ForeignKey(
//                         name: "FK_WaifuItem_WaifuInfo_WaifuInfoId",
//                         column: x => x.WaifuInfoId,
//                         principalTable: "WaifuInfo",
//                         principalColumn: "Id",
//                         onDelete: ReferentialAction.Restrict);
//                 });
//     }
//
//     protected override void Down(MigrationBuilder migrationBuilder)
//     {
//         migrationBuilder.DropTable(
//             name: "WaifuItem");
//
//         migrationBuilder.DropTable(
//             name: "WaifuUpdates");
//     }
// }
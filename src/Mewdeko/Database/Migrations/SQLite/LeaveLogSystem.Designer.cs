using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

[DbContext(typeof(MewdekoSqLiteContext))]
[Migration("AddLogSystemThingies")]
partial class CreateJoinLeaveLogsSystemThingie
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // just a template
    }

}
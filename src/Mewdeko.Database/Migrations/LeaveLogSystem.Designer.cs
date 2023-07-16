using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

[DbContext(typeof(MewdekoContext))]
[Migration("AddLogSystemThingies")]
partial class CreateJoinLeaveLogsSystemThingie
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // just a template
    }

}
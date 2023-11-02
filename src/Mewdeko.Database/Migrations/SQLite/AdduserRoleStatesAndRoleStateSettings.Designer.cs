using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

[DbContext(typeof(MewdekoSqLiteContext))]
[Migration("AddUserRoleStatesAndRoleStateSettings")]
partial class AddUserRoleStatesAndRoleStateSettings

{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // just a template
    }

}
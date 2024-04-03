using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.PostgreSql;

[DbContext(typeof(MewdekoPostgresContext))]
[Migration("AddAutoBanRole")]
partial class AddAutoBanRole
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // required for reasons
    }
}
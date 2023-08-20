using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

[DbContext(typeof(MewdekoContext))]
[Migration("AddGatoBanner")]
partial class AddGatoBanner

{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // just a template
    }

}
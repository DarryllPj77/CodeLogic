using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeLogic.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CodeLogicDbContext))]
[Migration("20261106050000_IncreaseSourceTextLimit")]
public partial class IncreaseSourceTextLimit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite stores both limits as TEXT and has no ALTER COLUMN operation for
        // MaxLength metadata. Application validation enforces the shared limit.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No physical SQLite schema change was made in Up.
    }
}

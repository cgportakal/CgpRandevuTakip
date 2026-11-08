using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgpRandevuTakip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_OutcomeAndLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClosedByUserId",
                table: "Appointments",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "Appointments");
        }
    }
}

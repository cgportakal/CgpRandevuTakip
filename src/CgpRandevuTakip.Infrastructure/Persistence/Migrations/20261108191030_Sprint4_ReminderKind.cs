using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgpRandevuTakip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_ReminderKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ReminderDispatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ReminderDispatches");
        }
    }
}

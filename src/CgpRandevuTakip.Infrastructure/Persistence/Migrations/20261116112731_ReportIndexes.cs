using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgpRandevuTakip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CategoryId_StartsAtUtc",
                table: "Appointments",
                columns: new[] { "CategoryId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status_StartsAtUtc",
                table: "Appointments",
                columns: new[] { "Status", "StartsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_CategoryId_StartsAtUtc",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Status_StartsAtUtc",
                table: "Appointments");
        }
    }
}

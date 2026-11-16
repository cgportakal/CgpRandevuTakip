using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgpRandevuTakip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeriesCount",
                table: "Appointments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeriesId",
                table: "Appointments",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeriesIndex",
                table: "Appointments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_SeriesId",
                table: "Appointments",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_SeriesId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SeriesCount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SeriesIndex",
                table: "Appointments");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgpRandevuTakip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeetingRoomsTitlesGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TitleId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomId",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppointmentGuests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AppointmentId = table.Column<string>(type: "TEXT", nullable: false),
                    PersonId = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Attendance = table.Column<int>(type: "INTEGER", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    PostNotes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    WillRepeat = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentGuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentGuests_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentGuests_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentHosts",
                columns: table => new
                {
                    AppointmentId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentHosts", x => new { x.AppointmentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AppointmentHosts_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentHosts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HostTitles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false, collation: "NOCASE"),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostTitles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingRooms",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false, collation: "NOCASE"),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    HostUserId = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoNamed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingRooms_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TitleId",
                table: "Users",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RoomId",
                table: "Appointments",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RoomId_StartsAtUtc",
                table: "Appointments",
                columns: new[] { "RoomId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentGuests_AppointmentId_IsPrimary",
                table: "AppointmentGuests",
                columns: new[] { "AppointmentId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentGuests_AppointmentId_PersonId",
                table: "AppointmentGuests",
                columns: new[] { "AppointmentId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentGuests_PersonId_AppointmentId",
                table: "AppointmentGuests",
                columns: new[] { "PersonId", "AppointmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHosts_AppointmentId_IsPrimary",
                table: "AppointmentHosts",
                columns: new[] { "AppointmentId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHosts_UserId_AppointmentId",
                table: "AppointmentHosts",
                columns: new[] { "UserId", "AppointmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_HostTitles_IsActive_SortOrder",
                table: "HostTitles",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HostTitles_Name",
                table: "HostTitles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRooms_HostUserId",
                table: "MeetingRooms",
                column: "HostUserId",
                unique: true,
                filter: "\"Kind\" = 0 AND \"HostUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRooms_Kind_IsActive_SortOrder",
                table: "MeetingRooms",
                columns: new[] { "Kind", "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_MeetingRooms_RoomId",
                table: "Appointments",
                column: "RoomId",
                principalTable: "MeetingRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_HostTitles_TitleId",
                table: "Users",
                column: "TitleId",
                principalTable: "HostTitles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_MeetingRooms_RoomId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_HostTitles_TitleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AppointmentGuests");

            migrationBuilder.DropTable(
                name: "AppointmentHosts");

            migrationBuilder.DropTable(
                name: "HostTitles");

            migrationBuilder.DropTable(
                name: "MeetingRooms");

            migrationBuilder.DropIndex(
                name: "IX_Users_TitleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_RoomId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_RoomId_StartsAtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "TitleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Appointments");
        }
    }
}

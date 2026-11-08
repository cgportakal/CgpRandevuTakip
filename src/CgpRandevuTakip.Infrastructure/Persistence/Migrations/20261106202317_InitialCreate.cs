using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgpRandevuTakip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PhoneE164 = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Organization = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recipients",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PhoneE164 = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Tag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLoginEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppShareLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AppointmentId = table.Column<string>(type: "TEXT", nullable: true),
                    ShareDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecipientIdsJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Result = table.Column<int>(type: "INTEGER", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppShareLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PersonId = table.Column<string>(type: "TEXT", nullable: false),
                    HostUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    StudentFirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StudentLastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentOutcomes",
                columns: table => new
                {
                    AppointmentId = table.Column<string>(type: "TEXT", nullable: false),
                    Attendance = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    WillRepeat = table.Column<bool>(type: "INTEGER", nullable: false),
                    PostNotes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentOutcomes", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_AppointmentOutcomes_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReminderDispatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AppointmentId = table.Column<string>(type: "TEXT", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAction = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SnoozeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WindowsNotifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PushNotifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActionAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActionByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    DedupKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderDispatches_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CategoryId",
                table: "Appointments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_HostUserId_StartsAtUtc",
                table: "Appointments",
                columns: new[] { "HostUserId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PersonId",
                table: "Appointments",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StartsAtUtc",
                table: "Appointments",
                column: "StartsAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAtUtc",
                table: "AuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_CreatedAtUtc",
                table: "BackupRecords",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_LastName_FirstName",
                table: "Persons",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Persons_PhoneE164",
                table: "Persons",
                column: "PhoneE164");

            migrationBuilder.CreateIndex(
                name: "IX_Recipients_IsActive",
                table: "Recipients",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDispatches_AppointmentId",
                table: "ReminderDispatches",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDispatches_DedupKey",
                table: "ReminderDispatches",
                column: "DedupKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDispatches_State_DueAtUtc",
                table: "ReminderDispatches",
                columns: new[] { "State", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppShareLogs_AppointmentId",
                table: "WhatsAppShareLogs",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppShareLogs_ShareDate",
                table: "WhatsAppShareLogs",
                column: "ShareDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentOutcomes");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BackupRecords");

            migrationBuilder.DropTable(
                name: "Recipients");

            migrationBuilder.DropTable(
                name: "ReminderDispatches");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "WhatsAppShareLogs");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Persons");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}

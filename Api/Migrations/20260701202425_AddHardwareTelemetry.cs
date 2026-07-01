using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHardwareTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PairedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BatteryPercent = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_AspNetUsers_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InsoleReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    AthleteId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Foot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PressureMapJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cadence = table.Column<double>(type: "float", nullable: true),
                    GroundContactMs = table.Column<double>(type: "float", nullable: true),
                    FootStrike = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StrideAsymmetryPct = table.Column<double>(type: "float", nullable: true),
                    ImpactForce = table.Column<double>(type: "float", nullable: true),
                    BalanceScore = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsoleReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InsoleReadings_AspNetUsers_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InsoleReadings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyncSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PacketCount = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WearableReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    AthleteId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HeartRate = table.Column<int>(type: "int", nullable: true),
                    RecoveryScore = table.Column<double>(type: "float", nullable: true),
                    StressLevel = table.Column<double>(type: "float", nullable: true),
                    SpO2 = table.Column<double>(type: "float", nullable: true),
                    HydrationPct = table.Column<double>(type: "float", nullable: true),
                    StaminaPct = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WearableReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WearableReadings_AspNetUsers_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WearableReadings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_AthleteId",
                table: "Devices",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InsoleReadings_AthleteId_Timestamp",
                table: "InsoleReadings",
                columns: new[] { "AthleteId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_InsoleReadings_DeviceId",
                table: "InsoleReadings",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncSessions_DeviceId",
                table: "SyncSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_WearableReadings_AthleteId_Timestamp",
                table: "WearableReadings",
                columns: new[] { "AthleteId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WearableReadings_DeviceId",
                table: "WearableReadings",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InsoleReadings");

            migrationBuilder.DropTable(
                name: "SyncSessions");

            migrationBuilder.DropTable(
                name: "WearableReadings");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}

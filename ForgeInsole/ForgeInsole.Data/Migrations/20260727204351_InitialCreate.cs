using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ForgeInsole.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Insoles",
                columns: table => new
                {
                    InsoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Firmware = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insoles", x => x.InsoleId);
                });

            migrationBuilder.CreateTable(
                name: "SimulationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InsoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PressureHeel = table.Column<double>(type: "float", nullable: false),
                    PressureMidfoot = table.Column<double>(type: "float", nullable: false),
                    PressureForefoot = table.Column<double>(type: "float", nullable: false),
                    AccelX = table.Column<double>(type: "float", nullable: false),
                    AccelY = table.Column<double>(type: "float", nullable: false),
                    AccelZ = table.Column<double>(type: "float", nullable: false),
                    GyroX = table.Column<double>(type: "float", nullable: false),
                    GyroY = table.Column<double>(type: "float", nullable: false),
                    GyroZ = table.Column<double>(type: "float", nullable: false),
                    Cadence = table.Column<double>(type: "float", nullable: false),
                    GaitBalance = table.Column<double>(type: "float", nullable: false),
                    ContactTimeMs = table.Column<double>(type: "float", nullable: false),
                    FootStrike = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StrideAsymmetryPct = table.Column<double>(type: "float", nullable: false),
                    ImpactForce = table.Column<double>(type: "float", nullable: false),
                    SimulationSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryReadings_Insoles_InsoleId",
                        column: x => x.InsoleId,
                        principalTable: "Insoles",
                        principalColumn: "InsoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Insoles",
                columns: new[] { "InsoleId", "CreatedAt", "Firmware", "Label", "Side", "Status" },
                values: new object[,]
                {
                    { "forge-insole-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1.4.0", "Forge Insole 01", "L", "Active" },
                    { "forge-insole-02", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1.4.0", "Forge Insole 02", "R", "Active" },
                    { "forge-insole-03", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1.4.0", "Forge Insole 03", "L", "Active" },
                    { "forge-insole-04", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1.4.0", "Forge Insole 04", "R", "Active" },
                    { "forge-insole-05", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1.3.2", "Forge Insole 05", "L", "Active" },
                    { "forge-insole-06", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1.3.2", "Forge Insole 06", "R", "Active" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SimulationSessions_StartedAt",
                table: "SimulationSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryReadings_InsoleId_Timestamp",
                table: "TelemetryReadings",
                columns: new[] { "InsoleId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimulationSessions");

            migrationBuilder.DropTable(
                name: "TelemetryReadings");

            migrationBuilder.DropTable(
                name: "Insoles");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeInsole.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceSourceAndSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue is "Simulated", not the scaffolder's "": Source is persisted via
            // HasConversion<string>() over the InsoleSource enum, and an empty string would
            // fail to parse back on read. Every row that predates this migration was produced
            // by InsoleTelemetryHostedService, so "Simulated" is also the accurate backfill.
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "TelemetryReadings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Simulated");

            migrationBuilder.AddColumn<int>(
                name: "Steps",
                table: "TelemetryReadings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Insoles",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Simulated");

            migrationBuilder.UpdateData(
                table: "Insoles",
                keyColumn: "InsoleId",
                keyValue: "forge-insole-01",
                column: "Source",
                value: "Simulated");

            migrationBuilder.UpdateData(
                table: "Insoles",
                keyColumn: "InsoleId",
                keyValue: "forge-insole-02",
                column: "Source",
                value: "Simulated");

            migrationBuilder.UpdateData(
                table: "Insoles",
                keyColumn: "InsoleId",
                keyValue: "forge-insole-03",
                column: "Source",
                value: "Simulated");

            migrationBuilder.UpdateData(
                table: "Insoles",
                keyColumn: "InsoleId",
                keyValue: "forge-insole-04",
                column: "Source",
                value: "Simulated");

            migrationBuilder.UpdateData(
                table: "Insoles",
                keyColumn: "InsoleId",
                keyValue: "forge-insole-05",
                column: "Source",
                value: "Simulated");

            migrationBuilder.UpdateData(
                table: "Insoles",
                keyColumn: "InsoleId",
                keyValue: "forge-insole-06",
                column: "Source",
                value: "Simulated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "TelemetryReadings");

            migrationBuilder.DropColumn(
                name: "Steps",
                table: "TelemetryReadings");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Insoles");
        }
    }
}

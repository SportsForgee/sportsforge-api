using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInsoleImu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AccelX",
                table: "InsoleReadings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AccelY",
                table: "InsoleReadings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AccelZ",
                table: "InsoleReadings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GyroX",
                table: "InsoleReadings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GyroY",
                table: "InsoleReadings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GyroZ",
                table: "InsoleReadings",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccelX",
                table: "InsoleReadings");

            migrationBuilder.DropColumn(
                name: "AccelY",
                table: "InsoleReadings");

            migrationBuilder.DropColumn(
                name: "AccelZ",
                table: "InsoleReadings");

            migrationBuilder.DropColumn(
                name: "GyroX",
                table: "InsoleReadings");

            migrationBuilder.DropColumn(
                name: "GyroY",
                table: "InsoleReadings");

            migrationBuilder.DropColumn(
                name: "GyroZ",
                table: "InsoleReadings");
        }
    }
}

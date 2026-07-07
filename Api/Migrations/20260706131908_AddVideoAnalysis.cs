using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoUploads",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AthleteId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoUploads_AspNetUsers_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoUploads_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VideoAnalysisResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoUploadId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TopSpeedKmh = table.Column<double>(type: "float", nullable: true),
                    GaitBalanceScore = table.Column<double>(type: "float", nullable: true),
                    SymmetryScore = table.Column<double>(type: "float", nullable: true),
                    AnomalyCount = table.Column<int>(type: "int", nullable: false),
                    CalibrationMethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAnalysisResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoAnalysisResults_VideoUploads_VideoUploadId",
                        column: x => x.VideoUploadId,
                        principalTable: "VideoUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalysisResults_VideoUploadId",
                table: "VideoAnalysisResults",
                column: "VideoUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoUploads_AthleteId",
                table: "VideoUploads",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoUploads_UploadedByUserId",
                table: "VideoUploads",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoAnalysisResults");

            migrationBuilder.DropTable(
                name: "VideoUploads");
        }
    }
}

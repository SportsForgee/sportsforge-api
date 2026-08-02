using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CoachId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sets = table.Column<int>(type: "int", nullable: false),
                    Reps = table.Column<int>(type: "int", nullable: false),
                    Duration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Intensity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drills_AspNetUsers_CoachId",
                        column: x => x.CoachId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CoachId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_AspNetUsers_CoachId",
                        column: x => x.CoachId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionDrills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    DrillId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionDrills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionDrills_Drills_DrillId",
                        column: x => x.DrillId,
                        principalTable: "Drills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDrills_TrainingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TrainingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    AthleteId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionParticipants_AspNetUsers_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionParticipants_TrainingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TrainingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrillCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionDrillId = table.Column<int>(type: "int", nullable: false),
                    AthleteId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActualSets = table.Column<int>(type: "int", nullable: false),
                    ActualReps = table.Column<int>(type: "int", nullable: false),
                    EffortRating = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrillCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrillCompletions_AspNetUsers_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrillCompletions_SessionDrills_SessionDrillId",
                        column: x => x.SessionDrillId,
                        principalTable: "SessionDrills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrillCompletions_AthleteId",
                table: "DrillCompletions",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_DrillCompletions_SessionDrillId_AthleteId",
                table: "DrillCompletions",
                columns: new[] { "SessionDrillId", "AthleteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drills_CoachId",
                table: "Drills",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDrills_DrillId",
                table: "SessionDrills",
                column: "DrillId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDrills_SessionId",
                table: "SessionDrills",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_AthleteId",
                table: "SessionParticipants",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_SessionId_AthleteId",
                table: "SessionParticipants",
                columns: new[] { "SessionId", "AthleteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_CoachId",
                table: "TrainingSessions",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_ScheduledAt",
                table: "TrainingSessions",
                column: "ScheduledAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrillCompletions");

            migrationBuilder.DropTable(
                name: "SessionParticipants");

            migrationBuilder.DropTable(
                name: "SessionDrills");

            migrationBuilder.DropTable(
                name: "Drills");

            migrationBuilder.DropTable(
                name: "TrainingSessions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewSchedulingBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Interviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GraphUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvailabilityRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvailabilityRecords_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewParticipants",
                columns: table => new
                {
                    InterviewId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewParticipants", x => new { x.InterviewId, x.ParticipantId });
                    table.ForeignKey(
                        name: "FK_InterviewParticipants_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterviewParticipants_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AvailabilityRecordId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeSlots_AvailabilityRecords_AvailabilityRecordId",
                        column: x => x.AvailabilityRecordId,
                        principalTable: "AvailabilityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityRecords_ParticipantId_Date",
                table: "AvailabilityRecords",
                columns: new[] { "ParticipantId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewParticipants_ParticipantId",
                table: "InterviewParticipants",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_StartTime",
                table: "Interviews",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_Status",
                table: "Interviews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Email",
                table: "Participants",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_GraphUserId",
                table: "Participants",
                column: "GraphUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_AvailabilityRecordId",
                table: "TimeSlots",
                column: "AvailabilityRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_EndTime",
                table: "TimeSlots",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_StartTime",
                table: "TimeSlots",
                column: "StartTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewParticipants");

            migrationBuilder.DropTable(
                name: "TimeSlots");

            migrationBuilder.DropTable(
                name: "Interviews");

            migrationBuilder.DropTable(
                name: "AvailabilityRecords");

            migrationBuilder.DropTable(
                name: "Participants");
        }
    }
}

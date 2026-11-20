using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phoenix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Enrollment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampusNumber = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    SchoolYear = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryGradeLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FullTimeEquivalency = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    ExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExitReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollment_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_CampusNumber_SchoolYear",
                table: "Enrollment",
                columns: new[] { "CampusNumber", "SchoolYear" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_StudentId_CampusNumber_EntryDate",
                table: "Enrollment",
                columns: new[] { "StudentId", "CampusNumber", "EntryDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_StudentId_SchoolYear",
                table: "Enrollment",
                columns: new[] { "StudentId", "SchoolYear" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Enrollment");
        }
    }
}

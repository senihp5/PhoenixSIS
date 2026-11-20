using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phoenix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_ActorId_OccurredUtc",
                table: "AuditEvent",
                columns: new[] { "ActorId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_OccurredUtc",
                table: "AuditEvent",
                column: "OccurredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_StudentId_OccurredUtc",
                table: "AuditEvent",
                columns: new[] { "StudentId", "OccurredUtc" });

            // ADR-004: the audit trail is append-only. The application never
            // updates or deletes audit rows; this trigger makes the database
            // refuse both as well. EF knows about it via HasTrigger.
            migrationBuilder.Sql(@"
CREATE TRIGGER [dbo].[TR_AuditEvent_AppendOnly]
ON [dbo].[AuditEvent]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, 'AuditEvent is append-only (ADR-004): UPDATE and DELETE are not allowed.', 1;
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_AuditEvent_AppendOnly];");

            migrationBuilder.DropTable(
                name: "AuditEvent");
        }
    }
}

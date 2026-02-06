using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class ReviewEmail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentReviewRequestDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    CompanyId = table.Column<int>(type: "integer", nullable: false),

                    AppointmentCompletionId = table.Column<int>(type: "integer", nullable: false),

                    ReviewId = table.Column<int>(type: "integer", nullable: false),

                    CustomerId = table.Column<int>(type: "integer", nullable: false),

                    RecipientEmail = table.Column<string>(type: "text", nullable: false),

                    Status = table.Column<int>(type: "integer", nullable: false),

                    AttemptCount = table.Column<int>(type: "integer", nullable: false),

                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),

                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),

                    LastError = table.Column<string>(type: "text", nullable: true),

                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),

                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentReviewRequestDispatches", x => x.Id);

                    table.ForeignKey(
                        name: "FK_AppointmentReviewRequestDispatches_AppointmentCompletions_AppointmentCompletionId",
                        column: x => x.AppointmentCompletionId,
                        principalTable: "AppointmentCompletions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_AppointmentReviewRequestDispatches_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 1 dispatch por completion (idempotência)
            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReviewRequestDispatches_AppointmentCompletionId",
                table: "AppointmentReviewRequestDispatches",
                column: "AppointmentCompletionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReviewRequestDispatches_ReviewId",
                table: "AppointmentReviewRequestDispatches",
                column: "ReviewId");

            // fila de envio / retry
            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReviewRequestDispatches_Status_SentAtUtc",
                table: "AppointmentReviewRequestDispatches",
                columns: new[] { "Status", "SentAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentReviewRequestDispatches");
        }
    }
}

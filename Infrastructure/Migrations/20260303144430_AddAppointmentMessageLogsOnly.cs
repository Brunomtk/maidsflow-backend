using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddAppointmentMessageLogsOnly : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentMessageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    AppointmentId = table.Column<int>(type: "integer", nullable: false),

                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),

                    ScheduledForUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),

                    Attempt = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),

                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),

                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RecipientPhoneE164 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),

                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    BodyText = table.Column<string>(type: "text", nullable: true),

                    TemplateKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),

                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Twilio"),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderStatus = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),

                    LastError = table.Column<string>(type: "text", nullable: true),
                    LastErrorRaw = table.Column<string>(type: "text", nullable: true),

                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentMessageLogs", x => x.Id);

                    // Se você quiser FK real (recomendado), descomenta isso:
                    // table.ForeignKey(
                    //     name: "FK_AppointmentMessageLogs_Appointments_AppointmentId",
                    //     column: x => x.AppointmentId,
                    //     principalTable: "Appointments",
                    //     principalColumn: "Id",
                    //     onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentMessageLogs_AppointmentId_Kind_Channel_CreatedDate",
                table: "AppointmentMessageLogs",
                columns: new[] { "AppointmentId", "Kind", "Channel", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentMessageLogs_ProviderMessageId",
                table: "AppointmentMessageLogs",
                column: "ProviderMessageId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppointmentMessageLogs");
        }
    }
}
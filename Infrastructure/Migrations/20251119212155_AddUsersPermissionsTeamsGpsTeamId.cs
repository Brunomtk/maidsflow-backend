using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersPermissionsTeamsGpsTeamId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // REMOVIDO: ProfessionalId em Notifications (já existe no banco)
            // migrationBuilder.AddColumn<int>(
            //     name: "ProfessionalId",
            //     table: "Notifications",
            //     type: "integer",
            //     nullable: true);

           // migrationBuilder.AddColumn<int>(
               // name: "UserId",
               // table: "Notifications",
               // type: "integer",
                //nullable: true);
            
            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "GpsTrackings",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Customers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "Customers",
                type: "text",
                nullable: true);

            //migrationBuilder.AddColumn<int>(
               // name: "CompanyId",
               // table: "Checklists",
               // type: "integer",
               // nullable: false,
               // defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    ProfessionalId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    IsLeader = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Professionals_ProfessionalId",
                        column: x => x.ProfessionalId,
                        principalTable: "Professionals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            //migrationBuilder.CreateIndex(
                //name: "IX_Checklists_CompanyId",
               // table: "Checklists",
               // column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_ProfessionalId",
                table: "TeamMembers",
                column: "ProfessionalId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId",
                table: "TeamMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_UserId",
                table: "TeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId",
                table: "UserPermissions",
                column: "UserId");

            //migrationBuilder.AddForeignKey(
                //name: "FK_Checklists_Companies_CompanyId",
                //table: "Checklists",
                //column: "CompanyId",
               // principalTable: "Companies",
               // principalColumn: "Id",
               // onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
               // name: "FK_Checklists_Companies_CompanyId",
               // table: "Checklists");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            //migrationBuilder.DropIndex(
              //  name: "IX_Checklists_CompanyId",
         //       table: "Checklists");

            // REMOVIDO: DropColumn de ProfessionalId (coluna já existia antes dessa migration)
            // migrationBuilder.DropColumn(
            //     name: "ProfessionalId",
            //     table: "Notifications");

            //migrationBuilder.DropColumn(
                //name: "UserId",
                //table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "GpsTrackings");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "Customers");

           // migrationBuilder.DropColumn(
               // name: "CompanyId",
              //  table: "Checklists");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Customers",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}

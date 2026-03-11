using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class Checklist : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChecklistItems_CustomerAreas_CustomerAreaId",
                table: "ChecklistItems");

            migrationBuilder.AddColumn<int>(
                name: "ChecklistTemplateId",
                table: "Checklists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyLabel",
                table: "Checklists",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateNameSnapshot",
                table: "Checklists",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomerAreaId",
                table: "ChecklistItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ChecklistTemplateItemId",
                table: "ChecklistItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ChecklistItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "ChecklistItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPhoto",
                table: "ChecklistItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ChecklistItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SpaceName",
                table: "ChecklistItems",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ChecklistItems",
                type: "character varying(220)",
                maxLength: 220,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TemplateType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsSystemTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistTemplates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTemplateItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChecklistTemplateId = table.Column<int>(type: "integer", nullable: false),
                    SpaceName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresPhoto = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistTemplateItems_ChecklistTemplates_ChecklistTemplateId",
                        column: x => x.ChecklistTemplateId,
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_ChecklistTemplateId",
                table: "Checklists",
                column: "ChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_ChecklistTemplateItemId",
                table: "ChecklistItems",
                column: "ChecklistTemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_CompanyId",
                table: "ChecklistTemplates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplateItems_ChecklistTemplateId",
                table: "ChecklistTemplateItems",
                column: "ChecklistTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChecklistItems_ChecklistTemplateItems_ChecklistTemplateItemId",
                table: "ChecklistItems",
                column: "ChecklistTemplateItemId",
                principalTable: "ChecklistTemplateItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChecklistItems_CustomerAreas_CustomerAreaId",
                table: "ChecklistItems",
                column: "CustomerAreaId",
                principalTable: "CustomerAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Checklists_ChecklistTemplates_ChecklistTemplateId",
                table: "Checklists",
                column: "ChecklistTemplateId",
                principalTable: "ChecklistTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChecklistItems_ChecklistTemplateItems_ChecklistTemplateItemId",
                table: "ChecklistItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ChecklistItems_CustomerAreas_CustomerAreaId",
                table: "ChecklistItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Checklists_ChecklistTemplates_ChecklistTemplateId",
                table: "Checklists");

            migrationBuilder.DropTable(
                name: "ChecklistTemplateItems");

            migrationBuilder.DropTable(
                name: "ChecklistTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Checklists_ChecklistTemplateId",
                table: "Checklists");

            migrationBuilder.DropIndex(
                name: "IX_ChecklistItems_ChecklistTemplateItemId",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "ChecklistTemplateId",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "PropertyLabel",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "TemplateNameSnapshot",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "ChecklistTemplateItemId",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "RequiresPhoto",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "SpaceName",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ChecklistItems");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerAreaId",
                table: "ChecklistItems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChecklistItems_CustomerAreas_CustomerAreaId",
                table: "ChecklistItems",
                column: "CustomerAreaId",
                principalTable: "CustomerAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

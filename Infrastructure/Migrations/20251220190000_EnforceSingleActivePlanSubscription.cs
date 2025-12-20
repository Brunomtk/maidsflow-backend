using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class EnforceSingleActivePlanSubscription : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cleanup: se por algum motivo existir mais de 1 assinatura Active por Company,
            // mantém a mais recente (CreatedDate/Id) como Active e marca o resto como Inactive.
            migrationBuilder.Sql(@"
WITH ranked AS (
  SELECT ""Id"", ""CompanyId"",
         ROW_NUMBER() OVER (PARTITION BY ""CompanyId"" ORDER BY ""CreatedDate"" DESC, ""Id"" DESC) AS rn
  FROM ""PlanSubscriptions""
  WHERE ""Status"" = 0 -- Active
)
UPDATE ""PlanSubscriptions"" ps
SET ""Status"" = 1, ""UpdatedDate"" = NOW()
FROM ranked r
WHERE ps.""Id"" = r.""Id"" AND r.rn > 1;
");

            // Garante 1 Active por Company no banco (Postgres: índice único parcial)
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PlanSubscriptions_Company_Active""
ON ""PlanSubscriptions"" (""CompanyId"")
WHERE ""Status"" = 0;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""UX_PlanSubscriptions_Company_Active"";");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ServiceExtension
{
    /// <summary>
    /// Applies pending EF Core migrations automatically on startup.
    /// Logs every step (current applied count, pending names, success/failure) so the
    /// container logs make it obvious whether the DB schema is current.
    /// </summary>
    public static class MigrationManager
    {
        public static IHost MigrateDatabase(this IHost host)
        {
            using var scope = host.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbContextClass>>();
            using var ctx = scope.ServiceProvider.GetRequiredService<DbContextClass>();

            try
            {
                var pending = ctx.Database.GetPendingMigrations().ToList();
                var applied = ctx.Database.GetAppliedMigrations().ToList();

                if (pending.Count == 0)
                {
                    logger.LogInformation(
                        "Database schema is up to date. {AppliedCount} migrations already applied.",
                        applied.Count);
                    return host;
                }

                logger.LogInformation(
                    "Applying {PendingCount} pending migrations on boot: {PendingList}",
                    pending.Count, string.Join(", ", pending));

                var sw = System.Diagnostics.Stopwatch.StartNew();
                ctx.Database.Migrate();
                sw.Stop();

                logger.LogInformation(
                    "Database migrations applied successfully in {ElapsedMs}ms ({Total} now applied).",
                    sw.ElapsedMilliseconds, applied.Count + pending.Count);
            }
            catch (System.Exception ex)
            {
                logger.LogCritical(ex,
                    "FATAL: Database migration failed on startup. App will not serve traffic until fixed.");
                throw;
            }
            return host;
        }
    }
}

// Infrastructure/ServiceExtension/ServiceExtension.cs
using Infrastructure;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.ServiceExtension
{
    public static class ServiceExtension
    {
        public static IServiceCollection AddDIServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<DbContextClass>(options =>
                options.UseNpgsql(connectionString)
            );

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<IPlanRepository, PlanRepository>();
            services.AddScoped<IPlanSubscriptionRepository, PlanSubscriptionRepository>();
            services.AddScoped<IProfessionalRepository, ProfessionalRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();
            services.AddScoped<ILeaderRepository, LeaderRepository>();
            services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            services.AddScoped<IAppointmentCompletionRepository, AppointmentCompletionRepository>();
            services.AddScoped<IServiceTypeRepository, ServiceTypeRepository>();
            services.AddScoped<IPayrollRuleRepository, PayrollRuleRepository>();
            services.AddScoped<IPayrollRunRepository, PayrollRunRepository>();
            services.AddScoped<IPayrollItemRepository, PayrollItemRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
            services.AddScoped<ICheckRecordRepository, CheckRecordRepository>();
            services.AddScoped<IRecurrenceRepository, RecurrenceRepository>();
            services.AddScoped<IGpsTrackingRepository, GpsTrackingRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.AddScoped<IInternalFeedbackRepository, InternalFeedbackRepository>();
            services.AddScoped<ICancellationRepository, CancellationRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
            
            

            services.AddScoped<Infrastructure.Repositories.IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}

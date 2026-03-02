using ControlApi.Middleware;
using Infrastructure.Authenticate;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Services;
using Services.Storage;
using Services.Integrations.Twilio;
using Services.Integrations.SendGrid;
using Services.Integrations.Stripe;
using Services.Integrations.Guesty;
using Services.Email;
using Core.Options;
using Services.Integrations.GoogleMaps;
using System.Text.Json.Serialization;
using System.Net;
using System.Text;
using ControlApi.BackgroundJobs;



namespace ControlApi
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);
            
            // -----------------------------
            // DI: Repositórios/Serviços + Db
            // -----------------------------
            builder.Services.AddDIServices(builder.Configuration);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<Services.Security.ICurrentUser, Services.Security.CurrentUser>();
            builder.Services.AddScoped<Services.Security.IScopeGuard, Services.Security.ScopeGuard>();

            
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<ICompanyService, CompanyService>();
            builder.Services.AddScoped<IPlanService, PlanService>();
            builder.Services.AddScoped<IPlanSubscriptionService, PlanSubscriptionService>();
            builder.Services.AddScoped<IProfessionalService, ProfessionalService>();
            builder.Services.AddScoped<ITeamService, TeamService>();
            builder.Services.AddScoped<ILeaderService, LeaderService>();
            builder.Services.AddScoped<IAppointmentService, AppointmentService>();
            builder.Services.AddScoped<IServiceTypeService, ServiceTypeService>();
            builder.Services.AddScoped<IPayrollRuleService, PayrollRuleService>();
            builder.Services.AddScoped<IPayrollPreviewService, PayrollPreviewService>();
            builder.Services.AddScoped<IPayrollRunService, PayrollRunService>();
builder.Services.AddScoped<IAppointmentCompletionService, AppointmentCompletionService>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<ICustomerAddressService, CustomerAddressService>();
            builder.Services.AddScoped<ICheckRecordService, CheckRecordService>();
            builder.Services.AddScoped<IRecurrenceService, RecurrenceService>();
            builder.Services.AddScoped<IGpsTrackingService, GpsTrackingService>();
            builder.Services.AddScoped<IRoutePlanningService, RoutePlanningService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
            builder.Services.AddScoped<IInternalFeedbackService, InternalFeedbackService>();
            builder.Services.AddScoped<ICancellationService, CancellationService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
            builder.Services.AddScoped<IPushNotificationSender, WebPushNotificationSender>();

            builder.Services.AddScoped<IStripeBillingService, StripeBillingService>();

// -----------------------------
// Guesty (Open API) - token is configured per company (Profile > Integrations)
// -----------------------------
builder.Services.Configure<GuestyOptions>(builder.Configuration.GetSection(GuestyOptions.SectionName));
builder.Services.AddSingleton<IGuestyRateLimiter, GuestyRateLimiter>();
builder.Services.AddHttpClient<IGuestyOpenApiClient, GuestyOpenApiClient>();
builder.Services.AddHttpClient<IGuestyAuthClient, GuestyAuthClient>();
builder.Services.AddScoped<IGuestyIntegrationService, GuestyIntegrationService>();
builder.Services.AddScoped<IGuestyScheduleService, GuestyScheduleService>();
builder.Services.AddScoped<IGuestyCustomerAddressSyncService, GuestyCustomerAddressSyncService>();


            // -----------------------------
            // S3 (Checklist Photos)
            // -----------------------------
            builder.Services.Configure<S3Options>(builder.Configuration.GetSection("S3"));
            builder.Services.AddSingleton<IS3StorageService, S3StorageService>();

            // -----------------------------
            // Twilio (SMS)
            // -----------------------------
            builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection("Twilio"));
            builder.Services.AddHttpClient<ITwilioSmsSender, TwilioSmsSender>();

            // -----------------------------
            // SendGrid (Email)
            // -----------------------------
            builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));

            // -----------------------------
            // Google Maps (Reverse Geocoding - optional)
            // -----------------------------
            builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection(GoogleMapsOptions.SectionName));
            builder.Services.AddHttpClient<IReverseGeocodingService, GoogleReverseGeocodingService>();
            builder.Services.AddHttpClient<IDirectionsService, GoogleDirectionsService>();
            builder.Services.AddHttpClient<IGeocodingService, GoogleGeocodingService>();

            // -----------------------------
            // GPS Tracking options
            // -----------------------------
            builder.Services.Configure<GpsTrackingOptions>(builder.Configuration.GetSection(GpsTrackingOptions.SectionName));

            builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
            builder.Services.AddHttpClient<ISendGridEmailSender, SendGridEmailSender>();
            builder.Services.AddScoped<ICredentialsEmailService, CredentialsEmailService>();
            builder.Services.AddScoped<IPasswordResetEmailService, PasswordResetEmailService>();
            builder.Services.AddScoped<IPlanPaymentEmailService, PlanPaymentEmailService>();
            builder.Services.AddScoped<Services.Email.IReviewRequestEmailService, Services.Email.ReviewRequestEmailService>();

            // -----------------------------
            // Background Jobs
            // -----------------------------
            builder.Services.AddHostedService<AppointmentReminderHostedService>();
builder.Services.AddHostedService<CheckoutReminderHostedService>();
            builder.Services.AddHostedService<ReviewRequestHostedService>();
            builder.Services.AddHostedService<NotificationCleanupHostedService>();
            builder.Services.AddHostedService<GpsTrackingRetentionHostedService>();
            
            // -----------------------------
            // JWT
            // -----------------------------
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
                        ),
                        ClockSkew = TimeSpan.Zero
                    };
                });
            
            builder.Services.AddSingleton<IJWTManager, JWTManager>();
            
            // -----------------------------
            // Controllers / JSON
            // -----------------------------
            builder.Services.AddControllers()
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });
            
            // -----------------------------
            // Swagger (com JWT)
            // -----------------------------
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Control.API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization headers usando o esquema Bearer. Ex: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });
            
            // -----------------------------
            // Serilog
            // -----------------------------
            builder.Host.UseSerilog((context, loggerConfig) =>
            {
                loggerConfig
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithEnvironmentName()
                    .Enrich.WithMachineName()
                    .Enrich.WithExceptionDetails()
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
            });
            
            // -----------------------------
            // CORS — origens explícitas (http e https)
            // -----------------------------
            var allowedOrigins = new[]
            {
                "http://localhost:3000",
                "https://localhost:3000",
            
                "http://maidsflow.com",
                "https://maidsflow.com",
                "http://www.maidsflow.com",
                "https://www.maidsflow.com",
            
                "http://138.197.119.101",
                "https://138.197.119.101"
            };
            
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    // Se precisar enviar cookies/credenciais cross-site, habilite:
                    // .AllowCredentials();
                    // e GARANTA que o Nginx não injeta Access-Control-Allow-* (deixe o backend cuidar)
                });
            });
            
            // -----------------------------
            // Forwarded Headers (proxy Nginx / LB)
            // -----------------------------
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            
                // Se o Nginx proxyia local -> Kestrel, use loopback:
                options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
            
                options.RequireHeaderSymmetry = false;
                // options.ForwardLimit = 2; // ajuste se houver mais proxies
            });
            
            builder.Services.AddScoped<ICustomerAreaRepository, CustomerAreaRepository>();
            builder.Services.AddScoped<IChecklistRepository, ChecklistRepository>();
            builder.Services.AddScoped<IChecklistItemRepository, ChecklistItemRepository>();
            builder.Services.AddScoped<IChecklistItemPhotoRepository, ChecklistItemPhotoRepository>();
            builder.Services.AddScoped<ICustomerAreaService, CustomerAreaService>();
            builder.Services.AddScoped<IChecklistService, ChecklistService>();
            var app = builder.Build();
            
            // -----------------------------
            // Pipeline
            // -----------------------------
            if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            
            // Se usa Npgsql em algum ponto legado
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            
            // Deve vir BEM no começo do pipeline, antes de redirecionamentos
            app.UseForwardedHeaders();
            
            app.UseRouting();
            
            // CORS precisa vir após Routing e antes de Auth
            app.UseCors("CorsPolicy");
            
            // Em produção/homolog SEM HTTPS direto no Kestrel, deixe desabilitado
            if (app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }
            
            app.UseSerilogRequestLogging(options =>
            {
                options.GetLevel = (httpContext, elapsed, ex) =>
                {
                    if (ex != null || httpContext.Response.StatusCode > 499) return LogEventLevel.Error;
                    if (httpContext.Response.StatusCode > 399) return LogEventLevel.Warning;
                    return LogEventLevel.Information;
                };
            });
            
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            
            app.UseAuthentication();
            app.UseAuthorization();
            
            app.MigrateDatabase();
            
            app.MapControllers();
            
            app.Run();

        }
    }
}
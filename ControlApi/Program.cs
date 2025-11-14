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
using System.Net;
using System.Text;



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
            
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<ICompanyService, CompanyService>();
            builder.Services.AddScoped<IPlanService, PlanService>();
            builder.Services.AddScoped<IPlanSubscriptionService, PlanSubscriptionService>();
            builder.Services.AddScoped<IProfessionalService, ProfessionalService>();
            builder.Services.AddScoped<ITeamService, TeamService>();
            builder.Services.AddScoped<ILeaderService, LeaderService>();
            builder.Services.AddScoped<IAppointmentService, AppointmentService>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<ICheckRecordService, CheckRecordService>();
            builder.Services.AddScoped<IRecurrenceService, RecurrenceService>();
            builder.Services.AddScoped<IGpsTrackingService, GpsTrackingService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
            builder.Services.AddScoped<IInternalFeedbackService, InternalFeedbackService>();
            builder.Services.AddScoped<ICancellationService, CancellationService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            
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
                .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);
            
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

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

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// DI: Repositórios/Serviços + Db (feito dentro de AddDIServices)
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
// CORS
// -----------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;

                if (origin.Equals("http://localhost:3000", StringComparison.OrdinalIgnoreCase)) return true;
                if (origin.Equals("http://localhost:3001", StringComparison.OrdinalIgnoreCase)) return true;
                if (origin.Equals("https://maidsflow.vercel.app", StringComparison.OrdinalIgnoreCase)) return true;

                try
                {
                    var host = new Uri(origin).Host;
                    return host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                           && host.StartsWith("maidsflow", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// -----------------------------
// Forwarded Headers (proxy Nginx / LB)
// -----------------------------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Proxy/LB conhecido (IP público do seu Nginx/LB)
    options.KnownProxies.Add(IPAddress.Parse("209.97.149.15"));

    // Em alguns ambientes o header pode aparecer fora de ordem;
    // isso evita descartes indevidos.
    options.RequireHeaderSymmetry = false;

    // Se existir mais de um proxy no caminho, ajuste o limite:
    // options.ForwardLimit = 2;
});

var app = builder.Build();

// -----------------------------
// Pipeline
// -----------------------------
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Deve vir cedo no pipeline, antes de autenticação/autorização
app.UseForwardedHeaders();

app.UseCors("AllowSpecificOrigins");
app.UseHttpsRedirection();

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

// 🔹 Aplica migrações usando sua extensão (que já resolve o DbContext certo)
app.MigrateDatabase();

app.MapControllers();
app.Run();

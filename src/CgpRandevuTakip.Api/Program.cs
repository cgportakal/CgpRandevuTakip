using System.Text.Json;
using System.Text.Json.Serialization;
using CgpRandevuTakip.Api.Auth;
using CgpRandevuTakip.Api.Endpoints;
using CgpRandevuTakip.Api.Infrastructure;
using CgpRandevuTakip.Api.Notifications;
using CgpRandevuTakip.Api.Realtime;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Content;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Notifications;
using CgpRandevuTakip.Application.Reminders;
using CgpRandevuTakip.Application.Reports;
using CgpRandevuTakip.Application.Rooms;
using CgpRandevuTakip.Application.Titles;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Infrastructure.Auditing;
using CgpRandevuTakip.Infrastructure.Persistence;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Infrastructure.Security;
using CgpRandevuTakip.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Sunucu verisi: Data:Root > CGP_API_DATA > ./data
var configuredRoot = builder.Configuration["Data:Root"];
var environmentRoot = Environment.GetEnvironmentVariable("CGP_API_DATA");

var dataRoot = !string.IsNullOrWhiteSpace(configuredRoot)
    ? configuredRoot
    : !string.IsNullOrWhiteSpace(environmentRoot)
        ? environmentRoot
        : Path.Combine(builder.Environment.ContentRootPath, "data");

var apiEnvironment = new ApiEnvironment(dataRoot);
var jwtService = new JwtTokenService(builder.Configuration, apiEnvironment);

builder.Services.AddSingleton(apiEnvironment);
builder.Services.AddSingleton<IAppEnvironment>(apiEnvironment);
builder.Services.AddSingleton(jwtService);
builder.Services.AddSingleton<ApiTokenStore>();

builder.Services.AddSingleton(new SqliteDbContextFactory(apiEnvironment.DatabasePath));
builder.Services.AddSingleton<IDbContextFactory<AppDbContext>>(
    sp => sp.GetRequiredService<SqliteDbContextFactory>());

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IAuditLogger, AuditLogger>();
builder.Services.AddSingleton<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddSingleton<ISecretProtector, AesSecretProtector>();

builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IHostTitleRepository, HostTitleRepository>();
builder.Services.AddSingleton<IMeetingRoomRepository, MeetingRoomRepository>();
builder.Services.AddSingleton<ICategoryRepository, CategoryRepository>();
builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
builder.Services.AddSingleton<IContentVersionRepository, ContentVersionRepository>();
builder.Services.AddSingleton<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddSingleton<IPersonRepository, PersonRepository>();
builder.Services.AddSingleton<IRecipientRepository, RecipientRepository>();

builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IAppointmentOutcomeService, AppointmentOutcomeService>();
builder.Services.AddScoped<IHostService, HostService>();
builder.Services.AddScoped<IHostTitleService, HostTitleService>();
builder.Services.AddScoped<IMeetingRoomService, MeetingRoomService>();
builder.Services.AddScoped<IContentAdminService, ContentAdminService>();
builder.Services.AddScoped<IReminderService, ReminderService>();

builder.Services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddSingleton<IPushNotifier, ServerPushNotifier>();
builder.Services.AddSingleton<VapidKeyProvider>();
builder.Services.AddSingleton<DeviceStore>();
builder.Services.AddSingleton<IDevicePushSender, DevicePushSender>();
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection("Push"));
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IReportQueries, ReportQueries>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtService.CreateValidationParameters();

        // SignalR WebSocket taşıyıcısı başlık gönderemez; belirteç query string ile gelir.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // Geliştirme varsayılanı: Expo/web istemcileri her porttan bağlanabilir.
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CGP - Randevu Takip API",
        Version = "v1",
        Description = "Masaüstü, web ve mobil istemciler için ortak randevu servisi."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Giriş yanıtındaki accessToken değerini girin."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<SqliteDbContextFactory>();
    using var db = factory.CreateDbContext();
    DatabaseInitializer.Initialize(db, apiEnvironment.DatabasePath);
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("EnableSwagger", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPublicEndpoints();
app.MapAuthEndpoints();
app.MapAppointmentEndpoints();
app.MapSeriesEndpoints();
app.MapReportEndpoints();
app.MapCatalogEndpoints();
app.MapContentEndpoints();
app.MapContentAdminEndpoints();
app.MapPushEndpoints();
app.MapHub<NotificationsHub>("/hubs/events");

app.Run();

/// <summary>WebApplicationFactory tabanlı testler için.</summary>
public partial class Program
{
}

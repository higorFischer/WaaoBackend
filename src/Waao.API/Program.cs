using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Waao.API.Hubs;
using Waao.API.Middleware;
using Waao.API.Notifications;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Infra.EF.Seeds;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Auth;
using CalendarService = Waao.Services.Services.CalendarService;
using CalendarEventService = Waao.Services.Services.CalendarEventService;
using Waao.Services.Gamification;
using Waao.Services.Services;
using Waao.Services.Validation;
using Waao.Services.Video;

var builder = WebApplication.CreateBuilder(args);

// Database — DATABASE_URL (Fly Postgres, postgres:// form) takes precedence over config
var connectionString = BuildConnectionString(builder.Configuration);
builder.Services.AddScoped<Waao.Services.Tenancy.TenantSaveChangesInterceptor>();
builder.Services.AddDbContext<WaaoDbContext>((sp, options) =>
{
	options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
	// Phase 2: stamp tenant_id on every new row from the request's ITenantContext.
	options.AddInterceptors(sp.GetRequiredService<Waao.Services.Tenancy.TenantSaveChangesInterceptor>());
});

// JWT settings — bind from config, fall back to a dev-only key
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.Key))
	jwtSettings = jwtSettings with { Key = "dev-only-do-not-use-in-prod-please-rotate-me-32+chars" };
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<JwtIssuer>();

// Authentication / authorization
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = jwtSettings.Issuer,
			ValidateAudience = true,
			ValidAudience = jwtSettings.Audience,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
			ClockSkew = TimeSpan.FromSeconds(30),
		};
		// SignalR WebSocket transport: read JWT from the access_token query string for /hubs paths
		options.Events = new JwtBearerEvents
		{
			OnMessageReceived = ctx =>
			{
				var token = ctx.Request.Query["access_token"].ToString();
				var path = ctx.HttpContext.Request.Path;
				if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
					ctx.Token = token;
				return Task.CompletedTask;
			},
		};
	});

builder.Services.AddAuthorization(options =>
{
	options.AddPolicy("Admin",        p => p.RequireRole(CollaboratorRoleKind.Admin.ToString()));
	options.AddPolicy("HR",           p => p.RequireRole(CollaboratorRoleKind.Admin.ToString(), CollaboratorRoleKind.HR.ToString()));
	options.AddPolicy("Collaborator", p => p.RequireAuthenticatedUser());
	// Default fallback: every endpoint requires auth unless [AllowAnonymous]
	options.FallbackPolicy = new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.Build();
});

// Validators (from Waao.Services assembly)
builder.Services.AddValidatorsFromAssemblyContaining<CreateCollaboratorValidator>();

// Services
builder.Services.AddScoped<GamificationEngine>();
builder.Services.AddScoped<StreakTracker>();
builder.Services.AddScoped<BadgeEvaluator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICollaboratorService, CollaboratorService>();
builder.Services.AddScoped<ICareerEventService, CareerEventService>();
builder.Services.AddScoped<IGamificationService, GamificationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IKanbanService, Waao.Services.Services.Kanban.KanbanService>();
builder.Services.AddScoped<IAllocationService, Waao.Services.Services.Allocation.AllocationService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICourseCompletionService, CourseCompletionService>();
builder.Services.AddScoped<IChallengeService, ChallengeService>();
builder.Services.AddScoped<IChallengeAttemptService, ChallengeAttemptService>();

// Calendar
builder.Services.AddSingleton<IRecurrenceExpander, Waao.Services.Calendar.RecurrenceExpander>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<ICalendarEventService, CalendarEventService>();

// Meetings
builder.Services.AddScoped<IMeetingService, Waao.Services.Services.MeetingService>();
builder.Services.AddScoped<IMeetingTranscriptService, Waao.Services.Transcription.MeetingTranscriptService>();

// Feature requests
builder.Services.AddScoped<IFeatureRequestService, Waao.Services.Services.FeatureRequestService>();

// Internal feedback (employee → admin)
builder.Services.AddScoped<IFeedbackService, Waao.Services.Services.FeedbackService>();

// Time Off
builder.Services.AddScoped<ITimeOffService, Waao.Services.Services.TimeOff.TimeOffService>();

// Kudos
builder.Services.AddScoped<IKudosService, Waao.Services.Services.Kudos.KudosService>();

// Weekly Focus (admin-curated)
builder.Services.AddScoped<IWeeklyFocusService, Waao.Services.Services.Focus.WeeklyFocusService>();

// 1:1s
builder.Services.AddScoped<IOneOnOneService, Waao.Services.Services.OneOnOnes.OneOnOneService>();

// Call channels (Discord-style voice rooms)
builder.Services.AddSingleton<ICallPresenceTracker, Waao.Services.Services.Calls.CallPresenceTracker>();
builder.Services.AddScoped<ICallChannelService, Waao.Services.Services.Calls.CallChannelService>();

// Peer-to-peer feedback
builder.Services.AddScoped<IPeerFeedbackService, Waao.Services.Services.Feedback.PeerFeedbackService>();

// Announcements (scheduled banners)
builder.Services.AddScoped<IAnnouncementService, Waao.Services.Services.Announcements.AnnouncementService>();

// Multi-tenancy — scoped TenantContext populated per-request from the JWT claim.
builder.Services.AddScoped<ITenantContext, Waao.Services.Tenancy.TenantContext>();
builder.Services.AddScoped<ITenantService, Waao.Services.Tenancy.TenantService>();

// Anniversary / birthday celebrations (daily background tick)
builder.Services.AddHostedService<Waao.API.HostedServices.AnniversaryHostedService>();

// Badge admin (manual flair badges)
builder.Services.AddScoped<IBadgeAdminService, Waao.Services.Services.Badges.BadgeAdminService>();

// R2 storage (used for chat attachments)
builder.Services.Configure<Waao.Services.Storage.R2Options>(builder.Configuration.GetSection("R2"));
builder.Services.AddSingleton<Waao.Services.Abstractions.Services.IR2StorageService, Waao.Services.Storage.R2StorageService>();

// LiveKit video
builder.Services.Configure<LiveKitOptions>(builder.Configuration.GetSection("LiveKit"));
builder.Services.AddSingleton<ILiveKitTokenService, LiveKitTokenService>();

// Transcription
builder.Services.Configure<Waao.Services.Transcription.TranscriptionOptions>(builder.Configuration.GetSection("Transcription"));

// Messaging
builder.Services.AddSingleton<IPresenceTracker, Waao.Services.Presence.PresenceTracker>();
// Message body encryption at rest (AES-256-GCM). Key = Fly secret MessageCrypto__Key (base64 32 bytes); empty = off.
builder.Services.Configure<Waao.Services.Security.MessageCryptoOptions>(builder.Configuration.GetSection("MessageCrypto"));
builder.Services.AddSingleton<Waao.Services.Abstractions.Services.IMessageTextProtector, Waao.Services.Security.MessageTextProtector>();
builder.Services.AddScoped<IChannelService, ChannelService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddSignalR();

// Notifications
builder.Services.Configure<Waao.Services.Push.VapidOptions>(builder.Configuration.GetSection("Vapid"));
builder.Services.AddScoped<INotificationBroadcaster, SignalRNotificationBroadcaster>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Documentation viewer (clones WaaoDocs locally + serves to frontend)
builder.Services.Configure<Waao.Services.Documentation.DocumentationOptions>(builder.Configuration.GetSection("Documentation"));
builder.Services.AddSingleton<IDocumentationService, Waao.Services.Documentation.DocumentationService>();

// Controllers + Swagger (with bearer auth)
builder.Services.AddControllers().AddJsonOptions(opts =>
{
	// Accept and emit enums as their string names (e.g. "Team", "Critical")
	opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Description = "JWT bearer. Format: \"Bearer {token}\".",
		Name = "Authorization",
		In = ParameterLocation.Header,
		Type = SecuritySchemeType.ApiKey,
		Scheme = "Bearer",
	});
	options.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme
			{
				Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
			},
			Array.Empty<string>()
		},
	});
});

// CORS — origins from config (Cors:AllowedOrigins, comma-separated); defaults to dev servers
// AllowCredentials is required for SignalR WebSocket transport
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173,http://localhost:3000")
	.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
	options.AddPolicy("Frontend", p => p
		.WithOrigins(corsOrigins)
		.AllowAnyHeader()
		.AllowAnyMethod()
		.AllowCredentials()));

builder.Services.AddHttpClient("resend", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<Waao.Services.Abstractions.Services.IEmailSender>(sp =>
{
	var cfg = sp.GetRequiredService<IConfiguration>();
	var key = cfg["Resend:ApiKey"];
	if (string.IsNullOrWhiteSpace(key))
		return new Waao.Services.Email.LoggingEmailSender(sp.GetRequiredService<ILogger<Waao.Services.Email.LoggingEmailSender>>());
	var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("resend");
	return new Waao.Services.Email.ResendEmailSender(http, sp.GetRequiredService<ILogger<Waao.Services.Email.ResendEmailSender>>(), key, cfg["Email:From"] ?? "WAAO <no-reply@waao.com.br>");
});

var app = builder.Build();

// Apply migrations + seed reference data on startup.
// Serialize across instances with a Postgres session advisory lock: with >1 machine
// (or overlapping machines during a rolling deploy) booting at once, the seed guards
// (AnyAsync) and the single SaveChanges are not atomic across processes — both would
// pass the empty-table check and the second insert would violate ix_badges_code.
// The lock makes one instance run migrate+seed while others wait, then no-op via the
// guards. Session-scoped + finally: auto-released on machine death, so no deadlock.
using (var scope = app.Services.CreateScope())
{
	const long startupInitLockKey = 727274001L;
	var db = scope.ServiceProvider.GetRequiredService<WaaoDbContext>();
	var conn = db.Database.GetDbConnection();
	await conn.OpenAsync();
	try
	{
		await using (var lockCmd = conn.CreateCommand())
		{
			lockCmd.CommandText = $"SELECT pg_advisory_lock({startupInitLockKey})";
			await lockCmd.ExecuteNonQueryAsync();
		}

		await db.Database.MigrateAsync();
		await DbInitializer.SeedAsync(db);

		// One-time idempotent backfills (no-op once complete): encrypt any plaintext message bodies,
		// and move legacy public chat attachments into the private bucket.
		var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupBackfills");
		await Waao.Services.Maintenance.StartupBackfills.EncryptLegacyMessageBodiesAsync(
			db, scope.ServiceProvider.GetRequiredService<Waao.Services.Abstractions.Services.IMessageTextProtector>(), startupLogger);
		await Waao.Services.Maintenance.StartupBackfills.MigrateLegacyAttachmentsToPrivateAsync(
			db, scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Waao.Services.Storage.R2Options>>().Value, startupLogger);
	}
	finally
	{
		await using var unlockCmd = conn.CreateCommand();
		unlockCmd.CommandText = $"SELECT pg_advisory_unlock({startupInitLockKey})";
		await unlockCmd.ExecuteNonQueryAsync();
	}
}

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("Frontend");
app.UseAuthentication();
// Multi-tenancy: must run after auth (needs the parsed JWT claims) and before any
// route handler so ITenantContext is populated for the whole pipeline.
app.UseMiddleware<Waao.API.Middleware.TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MessagingHub>("/hubs/messaging");
app.MapHub<Waao.API.Hubs.CallsHub>("/hubs/calls");

app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "waao-api", timestamp = DateTime.UtcNow }))
	.AllowAnonymous();

app.Run();

// Build the EF connection string. Fly Postgres injects DATABASE_URL in postgres:// form;
// fall back to ConnectionStrings:Default for local dev. Respects the URL's sslmode
// (Fly attach uses Flycast internal network with ?sslmode=disable).
static string BuildConnectionString(IConfiguration config)
{
	var url = Environment.GetEnvironmentVariable("DATABASE_URL");
	if (string.IsNullOrWhiteSpace(url))
		return config.GetConnectionString("Default")
			?? "Host=localhost;Port=5432;Database=WaaoLocal;Username=postgres;Password=postgres";

	var uri = new Uri(url);
	var userInfo = uri.UserInfo.Split(':', 2);
	var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
	var sslMode = query["sslmode"]?.ToLowerInvariant() switch
	{
		"disable" => Npgsql.SslMode.Disable,
		"require" => Npgsql.SslMode.Require,
		_ => Npgsql.SslMode.Prefer
	};
	return new Npgsql.NpgsqlConnectionStringBuilder
	{
		Host = uri.Host,
		Port = uri.Port > 0 ? uri.Port : 5432,
		Database = uri.AbsolutePath.TrimStart('/'),
		Username = Uri.UnescapeDataString(userInfo[0]),
		Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
		SslMode = sslMode
	}.ConnectionString;
}

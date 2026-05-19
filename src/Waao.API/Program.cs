using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Waao.API.Middleware;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Infra.EF.Seeds;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Auth;
using Waao.Services.Gamification;
using Waao.Services.Services;
using Waao.Services.Validation;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("Default")
	?? "Host=localhost;Port=5432;Database=WaaoLocal;Username=postgres;Password=postgres";
builder.Services.AddDbContext<WaaoDbContext>(options =>
	options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

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

// CORS for the frontend dev server
builder.Services.AddCors(options =>
	options.AddPolicy("Frontend", p => p
		.WithOrigins("http://localhost:5173", "http://localhost:3000")
		.AllowAnyHeader()
		.AllowAnyMethod()));

var app = builder.Build();

// Apply migrations + seed reference data on startup (dev convenience)
using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<WaaoDbContext>();
	await db.Database.MigrateAsync();
	await DbInitializer.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

app.Run();

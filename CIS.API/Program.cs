
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using CIS.Application;
using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS_WebAPI.Clients;
using CIS.DataAccess;
using CIS_WebAPI.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddDataAccess(builder.Configuration);

// Current authenticated user
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Users API
var usersApiBaseUrl = builder.Configuration["UsersApi:BaseUrl"];

if (string.IsNullOrWhiteSpace(usersApiBaseUrl))
{
    throw new InvalidOperationException(
        "UsersApi:BaseUrl is not configured.");
}

builder.Services.AddHttpClient<IUsersApiClient, UsersApiClient>(client =>
{
    client.BaseAddress = new Uri(usersApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

// JWT configuration
var signingKey = builder.Configuration["Jwt:SigningKey"];

if (string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException(
        "JWT signing key is not configured.");
}

var keyBytes = Encoding.UTF8.GetBytes(signingKey);

if (keyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

                RequireSignedTokens = true,
                RequireExpirationTime = true,

                ValidateLifetime = true,

                // The current Java token has no issuer/audience.
                ValidateIssuer = false,
                ValidateAudience = false,

                // Allow minor clock differences.
                ClockSkew = TimeSpan.FromSeconds(30),

                // Java stores the user UUID in "sub".
                NameClaimType = JwtRegisteredClaimNames.Sub,

                // Must match the Java signing algorithm.
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
            };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var subject = context.Principal?
                    .FindFirst(JwtRegisteredClaimNames.Sub)?
                    .Value;

                if (!Guid.TryParseExact(
                    subject, "D", out _))
                {
                    context.Fail("Invalid user identifier.");
                }

                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

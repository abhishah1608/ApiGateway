using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true).AddEnvironmentVariables();

builder.Services.AddOcelot(builder.Configuration);

string configPath = Directory.GetCurrentDirectory() + "\\" + "Config";

string envfile = builder.Environment.EnvironmentName == "Development" ? "dev" : builder.Environment.EnvironmentName == "Staging" ? "stag" : "prod";

if(envfile != "prod")
{
    // Enable detailed logging to show PII for debugging purposes (in development)
    IdentityModelEventSource.ShowPII = true;
}

// set path of appsettings-envfile.json.
builder.Configuration
    .SetBasePath(configPath) // Optional, if needed for path resolution
    .AddJsonFile($"appsettings-{envfile}.json", optional: true, reloadOnChange: true) // Environment-specific settings
    .AddEnvironmentVariables(); // Optional: if you're using environment variables


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, // Validate the issuer
        ValidateAudience = true, // Validate the audience
        ValidateLifetime = true, // Validate the token's expiration
        ValidateIssuerSigningKey = true, // Validate the signing key
        ValidIssuer = "Online_Course_Admin",
        ValidAudience = "Online_Course_Users",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtToken:key"].ToString())), // The key used to sign the token

        ClockSkew = TimeSpan.Zero // Optional: Reduce the clock skew for token expiration (default is 5 minutes)
    };

        // Hook into the events to log JWT validation issues
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                // Log details about the failure
                logger.LogError($"JWT token validation failed: {context.Exception.Message}");

                // Log the token from the request headers if available
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                if (!string.IsNullOrEmpty(token))
                {
                    logger.LogError($"Invalid JWT token: {token}");
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation($"JWT token validated successfully for user: {context.Principal.Identity.Name}");

                return Task.CompletedTask;
            }
        };

});

builder.Services.AddLogging(config =>
{
    config.ClearProviders(); // Clear default providers
    config.AddConsole(); // Add console logging
    config.AddDebug(); // Add debug logging
    //config.AddFile("Logs/app.log"); // Optionally, add file logging (requires Serilog or similar)
});

builder.Services.AddAuthorization(auth =>
{
    auth.AddPolicy("Bearer", new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme‌​)
        .RequireAuthenticatedUser().Build());
});

var app = builder.Build();

app.UseAuthentication();

app.UseAuthorization();

app.UseOcelot().Wait();

app.Run();

using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Omny.Api;
using Omny.Api.Auth;
using WebApplication1;
using Omny.Cms.Api.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddAuthentication();

bool hasAuth0Config = builder.Configuration.GetSection("Auth0").Exists() &&
                     !string.IsNullOrEmpty(builder.Configuration["Auth0:Domain"]) &&
                     !string.IsNullOrEmpty(builder.Configuration["Auth0:ClientId"]);
if (hasAuth0Config)
{
    builder.Services.PostConfigure<OpenIdConnectOptions>(Auth0Constants.AuthenticationScheme, options =>
    {
        options.Events.OnTicketReceived = context =>
        {
            context.Properties!.IsPersistent = true;
            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);
            return Task.CompletedTask;
        };
    });
    
    builder.Services
        .AddAuth0WebAppAuthentication(options =>
        {
            options.Domain = builder.Configuration["Auth0:Domain"]!;
            options.ClientId = builder.Configuration["Auth0:ClientId"]!;
            options.Scope = "openid email";
        });
}

builder.Services.AddAuthorization();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        // Return 401 for API endpoints
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        // Default behavior for other paths
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        // Return 403 for API endpoints
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        // Default behavior for other paths
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddSingleton<IEmailValidator, UserAdminChecker>();
builder.Services.AddSingleton<IUserInfoProvider, UserInfoProvider>();
builder.Services.AddSingleton<IUserAdminChecker, UserAdminChecker>();
builder.Services.AddHttpContextAccessor();

// Configure repository options
builder.Services.Configure<RepositoryOptions>(
    builder.Configuration.GetSection(RepositoryOptions.SectionName));

builder.Services.Configure<UserOptions>(
    builder.Configuration.GetSection(UserOptions.SectionName));

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();
if (builder.Configuration.GetValue<string>("OverrideHost") is { } overrideHost)
{
    app.Use(async (context, next) =>
    {
        context.Request.Host = new HostString(overrideHost);
        context.Request.Scheme = "https"; // Optional: set if you're running behind HTTP

        await next();
    });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarUi();
}

app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();


app.UseApiAuthorization(builder.Configuration["ApiAuthorization:SecretKey"]!);

app.UseEndpoints(_ => { });


app.AddAccountApis();

var apiGroup = app.MapGroup("/api");

apiGroup.AddCoreApis();

apiGroup.AddStorageApis();

#if DEBUG

if (app.Environment.IsDevelopment())
{
    app.UseBlazorFrameworkFiles(); // Serves the Blazor WASM app
    app.UseStaticFiles(); // Serves other static files
    app.MapFallbackToFile("index.html"); // Handles fallback routing
}
#endif

app.UseForwardedHeaders();

app.Run();

public partial class Program
{
}


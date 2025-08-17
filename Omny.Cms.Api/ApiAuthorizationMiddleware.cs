using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace WebApplication1;

public static class ApiAuthorizationMiddlewareExtensions
{
    private static string GetCsrfToken(string secretKey)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string timestamp = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
        byte[] dataBytes = Encoding.UTF8.GetBytes(timestamp);

        using HMACSHA256 hmac = new HMACSHA256(keyBytes);
        byte[] hashBytes = hmac.ComputeHash(dataBytes);
        string hash = Convert.ToBase64String(hashBytes);

        string token = $"{timestamp}:{hash}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
    }

    private static bool ValidateCsrfToken(string token, string secretKey, bool indefinite = false)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string decoded;
        try
        {
            byte[] tokenBytes = Convert.FromBase64String(token);
            decoded = Encoding.UTF8.GetString(tokenBytes);
        }
        catch
        {
            return false;
        }

        string[] parts = decoded.Split(':');
        if (parts.Length != 2)
            return false;

        string timestamp = parts[0];
        string expectedHash = parts[1];

        byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
        byte[] dataBytes = Encoding.UTF8.GetBytes(timestamp);

        using HMACSHA256 hmac = new HMACSHA256(keyBytes);
        byte[] hashBytes = hmac.ComputeHash(dataBytes);
        string actualHash = Convert.ToBase64String(hashBytes);

        bool isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash),
            Encoding.UTF8.GetBytes(actualHash));

        if (!isValid)
        {
            return false;
        }

        if (!long.TryParse(timestamp, out long ts))
        {
            return false;
        }

        DateTimeOffset tokenTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        TimeSpan age = DateTimeOffset.UtcNow - tokenTime;

        return age < TimeSpan.FromMinutes(30) || indefinite;
    }
    public static IApplicationBuilder UseApiAuthorization(this WebApplication app, string secretKey)
    {
        app.Use(async (context, next) =>
        {
            // Check if the request is for the API
            if (context.Request.Path.ToString().StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                // Skip checks if the endpoint has [AllowAnonymous]
                var endpoint = context.GetEndpoint();
                if (endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() != null)
                {
                    await next();
                    return;
                }

                // Check if the user is authenticated
                if (!context.User.Identity?.IsAuthenticated ?? true)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                
                // validate CSRF token
                if (!context.Request.Headers.TryGetValue("X-CSRF-Token", out var csrfToken) ||
                    string.IsNullOrWhiteSpace(csrfToken) ||
                    !ValidateCsrfToken(csrfToken!, secretKey))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                var email = context.User.Claims.FirstOrDefault(c =>
                    c.Type == "email" ||
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

                var emailVerified = context.User.Claims.FirstOrDefault(c =>
                    c.Type == "email_verified" ||
                    c.Type == "http://schemas.auth0.com/email_verified")?.Value;

                if(emailVerified != "true")
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                var emailValidator = context.RequestServices.GetRequiredService<IEmailValidator>();
                if (!(await emailValidator.IsValidAsync(email)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

            }

            // Continue to the next middleware
            await next();
        });
        
        app.MapPost("/api/csrf/token",  GenerateCsrfToken).AllowAnonymous();
        
        return app;
    }
    
    private static async Task<IResult> GenerateCsrfToken(HttpContext context, IConfiguration config)
    {
        await Task.Yield();
        if (context.User.Identity?.IsAuthenticated != true)
        {
            // returning an empty token for unauthenticated since it's simpler
            return TypedResults.Ok(new { Token = "" });
        }

        string csrfToken = GetCsrfToken(config["ApiAuthorization:SecretKey"] ?? "default");
        return TypedResults.Ok(new { Token = csrfToken });
    }
}

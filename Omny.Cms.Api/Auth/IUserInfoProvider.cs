using Microsoft.Extensions.Options;

namespace Omny.Api.Auth;

public record UserInfo(string? Email);
public interface IUserInfoProvider
{
    Task<UserInfo> GetCurrentUserAsync();
}

public class UserInfoProvider
    (IHttpContextAccessor httpContextAccessor)
    : IUserInfoProvider
{
    
    public async Task<UserInfo> GetCurrentUserAsync()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }
        
        var email = user.Claims.FirstOrDefault(c =>
            c.Type == "email" ||
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
        
        return await Task.FromResult(new UserInfo(email));
    }
}

public class UserOptions
{
    public List<UserInfo> Admins { get; set; } = new();
    public List<UserInfo> Users { get; set; } = new();
    
    public const string SectionName = "Users";
}
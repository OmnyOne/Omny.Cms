using Microsoft.Extensions.Options;
using WebApplication1;

namespace Omny.Api.Auth;

public interface IUserAdminChecker
{
    Task<bool> IsUserAdminAsync();
}

public class UserAdminChecker(
    IUserInfoProvider userInfoProvider,
    IOptions<UserOptions> options) : IUserAdminChecker, IEmailValidator
{
    

    public async Task<bool> IsUserAdminAsync()
    {
        var userInfo = await userInfoProvider.GetCurrentUserAsync();
        
        return options.Value.Admins.Any(a => 
            string.Equals(a.Email, userInfo.Email, StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> IsValidAsync(string? email)
    {
        var result = options.Value.Admins.Concat(options.Value.Users).Any( a =>
            string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result);
            
    }
}
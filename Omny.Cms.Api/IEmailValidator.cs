using System.Threading.Tasks;

namespace WebApplication1
{
    public interface IEmailValidator
    {
        Task<bool> IsValidAsync(string? email);
    }
}

using PlannerApi.Models;

namespace PlannerApi.Services;

public interface ITokenService
{
    string CreateToken(AppUser user);
}

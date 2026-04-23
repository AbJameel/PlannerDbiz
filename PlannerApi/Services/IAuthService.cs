using PlannerApi.Dtos.Auth;
using PlannerApi.Dtos.Users;
using PlannerApi.Models;

namespace PlannerApi.Services;

public interface IAuthService
{
    Task<IReadOnlyList<UserListItem>> GetUsersAsync();
    Task<IReadOnlyList<RoleItem>> GetRolesAsync();
    Task<int> CreateUserAsync(CreateUserRequest request, string? ipAddress);
    Task<bool> VerifyOtpAsync(VerifyOtpRequest request, string? ipAddress);
    Task<bool> SetInitialPasswordAsync(SetInitialPasswordRequest request, string? ipAddress);
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress);
}

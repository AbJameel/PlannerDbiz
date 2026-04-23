using PlannerApi.Dtos.Users;
using PlannerApi.Models;

namespace PlannerApi.Repositories;

public interface IAuthRepository
{
    Task<IReadOnlyList<UserListItem>> GetUsersAsync();
    Task<IReadOnlyList<RoleItem>> GetRolesAsync();
    Task<AppUser?> GetUserByEmailAsync(string email);
    Task<AppUser?> GetUserByIdAsync(int userId);
    Task<int> CreateUserAsync(string fullName, string email, string roleCode, int? vendorId, bool isActive);
    Task<int> CreateActivationAsync(int userId, Guid activationToken, string otpCode, DateTime otpExpiry);
    Task<UserActivation?> GetValidActivationAsync(string email, Guid token, string otpCode);
    Task SetInitialPasswordAsync(int userId, string passwordHash);
    Task AddPasswordHistoryAsync(int userId, string passwordHash);
    Task WriteAuditAsync(int? userId, string actionType, string actionDetail, string? ipAddress);
}

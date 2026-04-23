using PlannerApi.Dtos.Auth;
using PlannerApi.Dtos.Users;
using PlannerApi.Models;
using PlannerApi.Repositories;

namespace PlannerApi.Services;

public class AuthService(
    IAuthRepository repository,
    IPasswordService passwordService,
    ITokenService tokenService,
    IEmailService emailService) : IAuthService
{
    public Task<IReadOnlyList<UserListItem>> GetUsersAsync() => repository.GetUsersAsync();
    public Task<IReadOnlyList<RoleItem>> GetRolesAsync() => repository.GetRolesAsync();

    public async Task<int> CreateUserAsync(CreateUserRequest request, string? ipAddress)
    {
        var existing = await repository.GetUserByEmailAsync(request.Email);
        if (existing is not null) throw new InvalidOperationException("User with this email already exists.");

        var userId = await repository.CreateUserAsync(request.FullName.Trim(), request.Email.Trim(), request.RoleCode.Trim(), request.VendorId, request.IsActive);
        var token = Guid.NewGuid();
        var otp = Random.Shared.Next(100000, 999999).ToString();
        var expiry = DateTime.UtcNow.AddMinutes(15);
        await repository.CreateActivationAsync(userId, token, otp, expiry);

        var user = await repository.GetUserByIdAsync(userId) ?? throw new InvalidOperationException("Created user not found.");
        await emailService.SendUserActivationAsync(user.Email, user.FullName, token, otp, user.RoleCode);
        await repository.WriteAuditAsync(userId, "USER_CREATED", $"User created with role {user.RoleCode}", ipAddress);
        return userId;
    }

    public async Task<bool> VerifyOtpAsync(VerifyOtpRequest request, string? ipAddress)
    {
        var activation = await repository.GetValidActivationAsync(request.Email, request.ActivationToken, request.OtpCode);
        if (activation is null)
        {
            await repository.WriteAuditAsync(null, "OTP_VERIFY_FAILED", $"Failed OTP verification for {request.Email}", ipAddress);
            return false;
        }
        await repository.WriteAuditAsync(activation.UserId, "OTP_VERIFIED", "OTP verified successfully", ipAddress);
        return true;
    }

    public async Task<bool> SetInitialPasswordAsync(SetInitialPasswordRequest request, string? ipAddress)
    {
        if (request.NewPassword != request.ConfirmPassword) throw new InvalidOperationException("Password and confirm password do not match.");
        if (request.NewPassword.Length < 8) throw new InvalidOperationException("Password must be at least 8 characters.");

        var activation = await repository.GetValidActivationAsync(request.Email, request.ActivationToken, request.OtpCode);
        if (activation is null) return false;

        var passwordHash = passwordService.HashPassword(request.NewPassword);
        await repository.SetInitialPasswordAsync(activation.UserId, passwordHash);
        await repository.AddPasswordHistoryAsync(activation.UserId, passwordHash);
        await repository.WriteAuditAsync(activation.UserId, "INITIAL_PASSWORD_SET", "User completed first-time password setup", ipAddress);
        return true;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress)
    {
        var user = await repository.GetUserByEmailAsync(request.Email);
        if (user is null)
        {
            await repository.WriteAuditAsync(null, "LOGIN_FAILED", $"Unknown email: {request.Email}", ipAddress);
            return new LoginResponse { Success = false, Message = "Invalid email or password." };
        }
        if (!user.IsActive) return new LoginResponse { Success = false, Message = "User is inactive." };
        if (user.IsLocked) return new LoginResponse { Success = false, Message = "User is locked." };
        if (user.IsFirstLogin || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return new LoginResponse { Success = false, Message = "First-time activation required.", RequiresFirstLogin = true, UserId = user.UserId, RoleCode = user.RoleCode };
        }
        if (!passwordService.VerifyPassword(user.PasswordHash, request.Password))
        {
            await repository.WriteAuditAsync(user.UserId, "LOGIN_FAILED", "Invalid password", ipAddress);
            return new LoginResponse { Success = false, Message = "Invalid email or password." };
        }
        var token = tokenService.CreateToken(user);
        await repository.WriteAuditAsync(user.UserId, "LOGIN_SUCCESS", "User logged in successfully", ipAddress);
        return new LoginResponse { Success = true, Message = "Login successful.", Token = token, RoleCode = user.RoleCode, UserId = user.UserId, RequiresFirstLogin = false };
    }
}

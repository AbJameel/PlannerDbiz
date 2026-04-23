using Microsoft.AspNetCore.Mvc;
using PlannerApi.Dtos.Auth;
using PlannerApi.Services;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var ok = await authService.VerifyOtpAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString());
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = "Invalid or expired OTP." });
    }

    [HttpPost("set-initial-password")]
    public async Task<IActionResult> SetInitialPassword([FromBody] SetInitialPasswordRequest request)
    {
        var ok = await authService.SetInitialPasswordAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString());
        return ok ? Ok(new { success = true, message = "Password set successfully." })
                  : BadRequest(new { success = false, message = "Invalid or expired activation." });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerApi.Dtos.Users;
using PlannerApi.Services;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(IAuthService authService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> GetUsers() => Ok(await authService.GetUsersAsync());

    [HttpGet("roles")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRoles() => Ok(await authService.GetRolesAsync());

    [HttpPost]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var id = await authService.CreateUserAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(new { success = true, userId = id });
    }
}

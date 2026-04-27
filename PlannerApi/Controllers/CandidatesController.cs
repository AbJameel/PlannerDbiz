using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerApi.Models;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CandidatesController(IPostgresRepository repository) : ControllerBase
{
    private string? UserRole => User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))?.Value;
    private int? VendorId => int.TryParse(User.Claims.FirstOrDefault(c => c.Type == "vendor_id")?.Value, out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (UserRole == "VENDOR")
        {
            if (!VendorId.HasValue) return Unauthorized("VendorId missing in token. Please log out and log in again.");
            return Ok(await repository.GetSubmittedCandidatesForVendorAsync(VendorId.Value));
        }

        return Ok(await repository.GetCandidatesAsync());
    }

    [HttpGet("vendor-submitted")]
    public async Task<IActionResult> GetVendorSubmitted()
    {
        if (UserRole != "VENDOR") return Forbid();
        if (!VendorId.HasValue) return Unauthorized("VendorId missing in token. Please log out and log in again.");
        return Ok(await repository.GetSubmittedCandidatesForVendorAsync(VendorId.Value));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Candidate candidate)
    {
        if (UserRole == "VENDOR") return Forbid();
        var id = await repository.CreateCandidateAsync(candidate);
        return Ok(new { id });
    }
}

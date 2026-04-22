using Microsoft.AspNetCore.Mvc;
using PlannerApi.Models;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VendorsController(IPostgresRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await repository.GetVendorsAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Vendor vendor)
    {
        var id = await repository.CreateVendorAsync(vendor);
        return Ok(new { id });
    }
}

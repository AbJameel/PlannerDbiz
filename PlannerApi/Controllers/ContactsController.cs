using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerApi.Models;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactsController(IPostgresRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await repository.GetContactsAsync();
        return Ok(items);
    }

    [HttpPost("bulk-save")]
    public async Task<IActionResult> BulkSave([FromBody] List<PlannerContact> contacts)
    {
        await repository.SaveContactsAsync(contacts, User?.Identity?.Name ?? "Recruiter");
        return Ok(new { success = true, message = "Contacts saved successfully." });
    }
}

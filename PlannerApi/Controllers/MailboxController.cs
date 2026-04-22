using Microsoft.AspNetCore.Mvc;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MailboxController(IPostgresRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await repository.GetMailboxAsync());
}

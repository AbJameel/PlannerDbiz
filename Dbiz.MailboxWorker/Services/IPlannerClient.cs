using Dbiz.MailboxWorker.Models;

namespace Dbiz.MailboxWorker.Services;

public interface IPlannerClient
{
    Task<bool> CreatePlannerAsync(CreatePlannerFromMailRequest request, CancellationToken cancellationToken);
}

using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.EmailOutbox;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Email
{
    public class EmailOutboxRepository : IEmailOutboxRepository
    {
        private readonly ApplicationDBContext _context;
        public EmailOutboxRepository(ApplicationDBContext context)
        {
            _context = context;
        }

        public void Add(EmailOutbox email) => _context.EmailOutbox.Add(email);

        public void Detach(EmailOutbox email) => _context.Entry(email).State = EntityState.Detached;

        public Task<List<EmailOutbox>> GetDueAsync(DateTime now, int take) =>
            _context.EmailOutbox
                .Where(e => e.Status == EmailStatus.Pending && e.NextAttemptOn <= now)
                .OrderBy(e => e.NextAttemptOn).ThenBy(e => e.EmailOutboxID)
                .Take(take)
                .ToListAsync();

        public Task<bool> AnyQueuedSinceAsync(string kind, string relatedTable, string relatedId, DateTime since) =>
            _context.EmailOutbox.AnyAsync(e => e.RelatedTable == relatedTable && e.RelatedID == relatedId
                                            && e.Kind == kind && e.CreatedOn >= since);

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
    }
}

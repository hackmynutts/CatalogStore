using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.EventLogs;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.EventLogs
{
    public class EventlogRepository : IEventlogRepository
    {
        private readonly ApplicationDBContext _dbContext;
        public EventlogRepository(ApplicationDBContext dbContext) { _dbContext = dbContext; }
        public async Task<List<Eventlog>> GetAllEventsAsync() => await _dbContext.Eventlogs.AsNoTracking().ToListAsync();
        public async Task AddAsync(Eventlog eventlog)
        {
            _dbContext.Eventlogs.Add(eventlog);
            await _dbContext.SaveChangesAsync();
        }
    }
}

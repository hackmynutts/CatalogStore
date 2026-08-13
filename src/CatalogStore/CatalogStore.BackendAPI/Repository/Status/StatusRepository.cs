using CatalogStore.BackendAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Status
{
    public class StatusRepository : IStatusRepository
    {
        private readonly ApplicationDBContext _dbContext;
        public StatusRepository(ApplicationDBContext dbContext) { _dbContext = dbContext; }
        public async Task<List<Models.Status.Status>> GetAllStatusesAsync() => await _dbContext.Statuses.AsNoTracking().ToListAsync();
        public async Task<Models.Status.Status> GetStatusAsync(int id) => await _dbContext.Statuses.AsNoTracking().FirstOrDefaultAsync(s => s.StatusID == id);
        public async Task<int> AddAsync(Models.Status.Status status)
        {
            _dbContext.Statuses.Add(status);
            await _dbContext.SaveChangesAsync();
            return status.StatusID;
        }
        public async Task<bool> UpdateAsync(Models.Status.Status status)
        {
            _dbContext.Statuses.Update(status);
            return await _dbContext.SaveChangesAsync() > 0;
        }
        public async Task<bool> DeleteAsync(Models.Status.Status status)
        {
            _dbContext.Statuses.Remove(status);
            return await _dbContext.SaveChangesAsync() > 0;
        }
    }
}

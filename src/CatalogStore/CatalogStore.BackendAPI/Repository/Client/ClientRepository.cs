using CatalogStore.BackendAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Client
{
    public class ClientRepository : IClientRepository
    {
        private readonly ApplicationDBContext _dbContext;
        public ClientRepository(ApplicationDBContext dbContext) { _dbContext = dbContext; }
        public async Task<List<Models.Client.Client>> GetAllClientsAsync() => await _dbContext.Clients.AsNoTracking().ToListAsync();
        public async Task<List<Models.Client.Client>> GetActiveClientsAsync() => await _dbContext.Clients.Where(c => c.StatusID == 1).AsNoTracking().ToListAsync();
        public async Task<Models.Client.Client> GetClientAsync(int ID) => await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ID);
        public async Task<int> AddAsync(Models.Client.Client client) 
        {
            _dbContext.Clients.Add(client);
            await _dbContext.SaveChangesAsync();
            return client.Id;
        }
        public async Task<bool> UpdateAsync(Models.Client.Client client)
        {
            _dbContext.Clients.Update(client);
            return await _dbContext.SaveChangesAsync() > 0;
        }
        public async Task<bool> DeleteAsync(int ID) 
        {
            Models.Client.Client cliente = await _dbContext.Clients.FindAsync(ID);
            if (cliente == null) return false;

            _dbContext.Clients.Remove(cliente);
            return await _dbContext.SaveChangesAsync() > 0;
        }
    }
}

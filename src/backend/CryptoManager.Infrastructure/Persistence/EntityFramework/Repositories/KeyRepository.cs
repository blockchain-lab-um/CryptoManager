using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework
{
    public sealed class KeyRepository : IKeyRepository
    {
        private readonly AppDbContext dbContext;

        public KeyRepository(AppDbContext context)
        {
            dbContext = context;
        }

        public async Task<Key?> GetByIdAsync(KeyId id)
        {
            return await dbContext.Keys
                .Include(k => k.Versions)
                .FirstOrDefaultAsync(k => k.Id == id);
        }

        public async Task<Key?> GetByNameAsync(string name)
        {
            return await dbContext.Keys
                .Include(k => k.Versions)
                .FirstOrDefaultAsync(k => k.Name == name);
        }

        public async Task<IReadOnlyList<Key>> ListAllAsync()
        {
            return await dbContext.Keys
                .Include(k => k.Versions)
                .ToListAsync();
        }

        public async Task AddAsync(Key key)
        {
            await dbContext.Keys.AddAsync(key);
        }

        public async Task UpdateAsync(Key key)
        {
            dbContext.Keys.Update(key);
            await Task.CompletedTask;
        }
    }
}

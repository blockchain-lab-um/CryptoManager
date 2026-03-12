using CryptoManager.Domain.Entities;
using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Application.Abstractions
{
    public interface IKeyRepository
    {
        Task<Key?> GetByIdAsync(KeyId id);
        Task<Key?> GetByNameAsync(string name);
        Task<IReadOnlyList<Key>> ListAllAsync();
        Task AddAsync(Key key);
        Task UpdateAsync(Key key);
        Task DeleteAsync(Key key);
    }
}

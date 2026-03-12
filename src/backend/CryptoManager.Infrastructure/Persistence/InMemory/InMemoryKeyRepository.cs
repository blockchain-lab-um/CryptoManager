using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Infrastructure.Persistence.InMemory
{
    public sealed class InMemoryKeyRepository : IKeyRepository
    {
        private readonly ConcurrentDictionary<Guid, Key> _byId = new();
        private readonly ConcurrentDictionary<string, Guid> _idByName = new(StringComparer.OrdinalIgnoreCase);

        public Task<Key?> GetByIdAsync(KeyId id)
        {
            _byId.TryGetValue(id.Value, out var key);
            return Task.FromResult(key);
        }

        public Task<Key?> GetByNameAsync(string name)
        {
            if (_idByName.TryGetValue(name, out var id) && _byId.TryGetValue(id, out var key))
                return Task.FromResult<Key?>(key);

            return Task.FromResult<Key?>(null);
        }

        public Task<IReadOnlyList<Key>> ListAllAsync()
        {
            IReadOnlyList<Key> keys = _byId.Values.ToList();
            return Task.FromResult(keys);
        }

        public Task<IReadOnlyList<Key>> ListByOwnerAsync(string ownerId)
        {
            IReadOnlyList<Key> keys = _byId.Values.Where(k => k.OwnerId == ownerId).ToList();
            return Task.FromResult(keys);
        }

        public Task AddAsync(Key key)
        {
            if (!_byId.TryAdd(key.Id.Value, key))
                throw new DomainException("Key already exists.");

            _idByName[key.Name] = key.Id.Value;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Key key)
        {
            _byId[key.Id.Value] = key;
            _idByName[key.Name] = key.Id.Value;
            return Task.CompletedTask;
        }
        
        public Task DeleteAsync(Key key)
        {
            _byId.TryRemove(key.Id.Value, out _);
            _idByName.TryRemove(key.Name, out _);
            return Task.CompletedTask;
        }
    }
}

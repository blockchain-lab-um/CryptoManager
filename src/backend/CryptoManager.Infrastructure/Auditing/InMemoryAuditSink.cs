using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Infrastructure.Auditing
{
    public sealed class InMemoryAuditSink : IAuditSink
    {
        private readonly ConcurrentQueue<AuditEvent> _events = new();
        public IReadOnlyCollection<AuditEvent> Events => _events.ToArray();

        public Task WriteAsync(AuditEvent evt)
        {
            _events.Enqueue(evt);
            return Task.CompletedTask;
        }
    }
}

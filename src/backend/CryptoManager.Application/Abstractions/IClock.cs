using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Application.Abstractions
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }
}

using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Application.DTOs
{
    public sealed record SignFileCommand(
        KeyId KeyId,
        Mechanism Mechanism,
        string OriginalFileName,
        string? OriginalContentType,
        byte[] FileBytes,
        StampOptions? Stamp
    );
}

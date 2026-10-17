using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Application.DTOs
{
    public sealed record SignFileResult(
        KeyId KeyId,
        int KeyVersion,
        Mechanism Mechanism,
        string SignedFormat,
        string OutputFileName,
        string OutputContentType,
        byte[] SignedFileBytes,
        AuditEventId AuditEventId
    );
}

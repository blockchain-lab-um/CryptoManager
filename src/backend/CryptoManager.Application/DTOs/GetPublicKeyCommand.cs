using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Application.DTOs
{
    public sealed record GetPublicKeyCommand
    (
        KeyId KeyId,
        int? KeyVersion
    );
}

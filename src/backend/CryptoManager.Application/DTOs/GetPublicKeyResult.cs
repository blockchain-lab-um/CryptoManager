using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Application.DTOs
{
    public sealed record GetPublicKeyResult
    (
        KeyId KeyId,
        int KeyVersion,
        PublicKeyMaterial PublicKey
    );
}

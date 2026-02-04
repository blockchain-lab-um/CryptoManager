using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Infrastructure.HSM.PKCS11
{
    public sealed class Pkcs11Options
    {
        public string LibraryPath { get; init; } = default!;  // e.g. yubihsm_pkcs11.dll path
        public string TokenLabel { get; init; } = default!;  // token label shown by PKCS#11
        public string UserPin { get; init; } = default!;  // token PIN (or YubiHSM auth via module config)
        public int RsaKeySizeBits { get; init; } = 2048;       // MVP default
    }
}

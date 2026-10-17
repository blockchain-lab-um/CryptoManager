namespace CryptoManager.Domain.Enums;

public enum CertificateSource
{
    SelfSigned  = 1,  // issued by the backend's own soft CA
    ExternalCA  = 2,  // enrolled via CSR to a third-party CA
    Imported    = 3,  // uploaded directly by a user
}
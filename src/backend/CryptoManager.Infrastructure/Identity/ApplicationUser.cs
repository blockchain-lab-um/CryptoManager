using Microsoft.AspNetCore.Identity;

namespace CryptoManager.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    // IdentityUser already provides: Id, UserName, Email, PasswordHash, etc.
    // Extend here if user-specific profile fields are needed in the future.
}

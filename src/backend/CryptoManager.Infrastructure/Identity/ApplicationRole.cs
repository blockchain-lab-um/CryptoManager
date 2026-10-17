using Microsoft.AspNetCore.Identity;

namespace CryptoManager.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
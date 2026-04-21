using Microsoft.AspNetCore.Identity;

namespace ApiApostas.Models;

public class ApplicationUser : IdentityUser
{
    public string NomeCompleto { get; set; } = string.Empty;
    public DateTime DataRegisto { get; set; } = DateTime.UtcNow;
}

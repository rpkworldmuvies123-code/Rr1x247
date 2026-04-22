using ApiApostas.Models;
using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    JwtService jwtService,
    PagamentosClient pagamentos) : ControllerBase
{
    public record RegistarRequest(string Email, string Password, string NomeCompleto);
    public record LoginRequest(string Email, string Password);

    [HttpPost("registar")]
    public async Task<IActionResult> Registar([FromBody] RegistarRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NomeCompleto))
            return BadRequest(new { message = "NomeCompleto é obrigatório." });

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            NomeCompleto = req.NomeCompleto,
            DataRegisto = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = "Erro ao criar utilizador.", errors = result.Errors.Select(e => e.Description) });

        await userManager.AddToRoleAsync(user, "User");

        // Criar saldo inicial (50 EUR) via ApiPagamentos
        try
        {
            await pagamentos.CriarSaldoUtilizadorAsync(user.Id);
        }
        catch
        {
            await userManager.DeleteAsync(user);
            return StatusCode(500, new { message = "Erro ao inicializar saldo. Registo revertido." });
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = jwtService.GerarToken(user, roles);

        return Ok(new { token, utilizadorId = user.Id, email = user.Email, nomeCompleto = user.NomeCompleto });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user == null || !await userManager.CheckPasswordAsync(user, req.Password))
            return Unauthorized(new { message = "Credenciais inválidas." });

        var roles = await userManager.GetRolesAsync(user);
        var token = jwtService.GerarToken(user, roles);

        return Ok(new { token, utilizadorId = user.Id, email = user.Email, nomeCompleto = user.NomeCompleto });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        var roles = await userManager.GetRolesAsync(user);
        return Ok(new { utilizadorId = user.Id, email = user.Email, nomeCompleto = user.NomeCompleto, roles });
    }
}

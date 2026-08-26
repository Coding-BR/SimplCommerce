using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdealCreative.Api.Contracts;
using IdealCreative.Api.Models;
using IdealCreative.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/users/privacy")]
[Authorize]
public sealed class PrivacyController(UserManager<ApplicationUser> users, AccountDeletionService deletionService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;

    [HttpGet("deletion-preview")]
    public async Task<ActionResult<AccountDeletionPreview>> Preview(CancellationToken ct)
    {
        var user = await users.FindByIdAsync(UserId);
        if (user is null) return Unauthorized();
        if (!string.Equals(user.AccountState, AccountStates.Active, StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Esta conta já possui uma solicitação de exclusão ou foi encerrada." });
        return Ok(await deletionService.PreviewAsync(user.Id, ct));
    }

    [HttpPost("delete-account")]
    public async Task<ActionResult<AccountDeletionResult>> DeleteAccount(DeleteAccountRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return BadRequest(new { message = "Confirme sua senha atual para excluir a conta." });

        var user = await users.FindByIdAsync(UserId);
        if (user is null) return Unauthorized();
        if (!await users.CheckPasswordAsync(user, request.CurrentPassword))
            return Unauthorized(new { message = "Senha atual inválida." });

        var result = await deletionService.RequestAsync(user, ct);
        return result.Anonymized ? Ok(result) : Accepted(result);
    }
}

using IdealCreative.Api.Contracts;
using IdealCreative.Api.Models;
using IdealCreative.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using System.Diagnostics;
using System.Text;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> users,
    ITokenService tokens,
    IEmailQueue emailQueue,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string PasswordResetMessage = "Se existir uma conta ativa com este e-mail, enviaremos as instruções para redefinir sua senha.";

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = request.DisplayName.Trim() };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return ValidationProblem(new ValidationProblemDetails(result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));

        var roleResult = await users.AddToRoleAsync(user, "Customer");
        if (!roleResult.Succeeded)
        {
            await users.DeleteAsync(user);
            return ValidationProblem(new ValidationProblemDetails(roleResult.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));
        }
        return Ok(await tokens.CreateAsync(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null)
            return Unauthorized(new { message = "E-mail ou senha inválidos." });

        if (!string.Equals(user.AccountState, AccountStates.Active, StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { message = "Esta conta foi encerrada ou possui uma solicitação de exclusão em andamento." });

        if (await users.IsLockedOutAsync(user))
            return Unauthorized(new { message = "Acesso temporariamente bloqueado. Tente novamente mais tarde." });

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return Unauthorized(new { message = "E-mail ou senha inválidos." });
        }

        await users.ResetAccessFailedCountAsync(user);

        return Ok(await tokens.CreateAsync(user));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var user = await users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
            if (user is not null && string.Equals(user.AccountState, AccountStates.Active, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var token = await users.GeneratePasswordResetTokenAsync(user);
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                    var publicUrl = (configuration["Frontend:PublicUrl"] ?? "http://localhost:5289").TrimEnd('/');
                    var resetUrl = $"{publicUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(encodedToken)}";
                    await emailQueue.QueueAsync(new EmailWorkItem(EmailWorkType.PasswordReset, user.Email!, resetUrl), cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Não foi possível preparar a recuperação de senha para o usuário {UserId}", user.Id);
                }
            }
        }

        var minimumResponseTime = TimeSpan.FromMilliseconds(250);
        if (stopwatch.Elapsed < minimumResponseTime)
            await Task.Delay(minimumResponseTime - stopwatch.Elapsed, cancellationToken);
        return Accepted(new { message = PasswordResetMessage });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "O link de recuperação é inválido ou expirou." });

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "O link de recuperação é inválido ou expirou." });
        }

        var user = await users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !string.Equals(user.AccountState, AccountStates.Active, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "O link de recuperação é inválido ou expirou." });

        var result = await users.ResetPasswordAsync(user, token, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = "O link de recuperação é inválido, expirou ou a nova senha não atende aos requisitos." });

        user.TokenVersion++;
        var updateResult = await users.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            logger.LogError("A senha foi alterada, mas não foi possível invalidar as sessões do usuário {UserId}", user.Id);
            return Problem("Não foi possível concluir a redefinição da senha.");
        }

        await emailQueue.QueueAsync(new EmailWorkItem(EmailWorkType.PasswordChanged, user.Email!), HttpContext.RequestAborted);
        return Ok(new { message = "Senha alterada com sucesso. Entre com sua nova senha." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<object>> Me()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(new { user.Id, user.Email, user.DisplayName, IsAdmin = await users.IsInRoleAsync(user, "Admin") });
    }
}

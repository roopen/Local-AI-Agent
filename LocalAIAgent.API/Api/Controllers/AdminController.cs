using System.Security.Claims;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Api.Controllers;

[ApiController]
[Authorize(Roles = AuthRoles.Owner)]
[Route("api/admin")]
public sealed class AdminController(UserContext context, IConfiguration configuration, TimeProvider timeProvider) : ControllerBase
{
    [HttpPost("invitations")]
    public async Task<ActionResult<CreatedInvitationDto>> CreateInvitation(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out int ownerId))
            return Unauthorized();

        string token = InvitationTokens.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        Invitation invitation = new()
        {
            TokenHash = InvitationTokens.Hash(token),
            CreatedByUserId = ownerId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        };
        context.Invitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);

        string origin = (configuration["PUBLIC_ORIGIN"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
        return Ok(new CreatedInvitationDto(
            invitation.Id,
            $"{origin}/register#invite={token}",
            invitation.ExpiresAt));
    }

    [HttpGet("invitations")]
    public async Task<ActionResult<IReadOnlyList<InvitationDto>>> ListInvitations(CancellationToken cancellationToken)
    {
        List<InvitationDto> invitations = await context.Invitations
            .AsNoTracking()
            .OrderByDescending(i => i.Id)
            .Select(i => new InvitationDto(
                i.Id,
                i.CreatedAt,
                i.ExpiresAt,
                i.RedeemedAt,
                i.RevokedAt,
                i.RedeemedByUser == null ? null : i.RedeemedByUser.Username))
            .ToListAsync(cancellationToken);
        return Ok(invitations);
    }

    [HttpDelete("invitations/{invitationId:int}")]
    public async Task<IActionResult> RevokeInvitation(int invitationId, CancellationToken cancellationToken)
    {
        Invitation? invitation = await context.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
        if (invitation is null)
            return NotFound();
        if (invitation.RedeemedAt is not null)
            return Conflict("A redeemed invitation cannot be revoked.");

        invitation.RevokedAt ??= timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<ManagedUserDto>>> ListUsers(CancellationToken cancellationToken)
    {
        List<ManagedUserDto> users = await context.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new ManagedUserDto(u.Id, u.Username, u.Role.ToString(), u.IsDisabled))
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPatch("users/{userId:int}")]
    public async Task<IActionResult> SetUserState(
        int userId,
        [FromBody] UpdateManagedUserDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out int ownerId))
            return Unauthorized();
        if (userId == ownerId && request.IsDisabled)
            return BadRequest("The owner cannot disable their own account.");

        User? user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return NotFound();
        if (user.Role == UserRole.Owner && request.IsDisabled)
            return BadRequest("An owner account cannot be disabled.");

        user.IsDisabled = request.IsDisabled;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record CreatedInvitationDto(int Id, string InviteUrl, DateTimeOffset ExpiresAt);
public sealed record InvitationDto(
    int Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RedeemedAt,
    DateTimeOffset? RevokedAt,
    string? RedeemedByUsername);
public sealed record ManagedUserDto(int Id, string Username, string Role, bool IsDisabled);
public sealed record UpdateManagedUserDto(bool IsDisabled);

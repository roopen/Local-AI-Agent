using Fido2NetLib;
using Fido2NetLib.Objects;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers.Text;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIAgent.API.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public partial class Fido2Controller(
        IFido2 fido2,
        IMemoryCache memoryCache,
        UserContext userContext,
        IGetUserUseCase getUserUseCase,
        IMetadataService mds,
        TimeProvider timeProvider) : ControllerBase
    {
        [HttpGet]
        [Route("/api/auth/registration-status")]
        [AllowAnonymous]
        public async Task<RegistrationStatusDto> GetRegistrationStatus(CancellationToken cancellationToken)
        {
            bool hasUsers = await userContext.Users.AsNoTracking().AnyAsync(cancellationToken);
            return new RegistrationStatusDto(hasUsers ? "InviteRequired" : "OwnerBootstrap");
        }

        [HttpPost]
        [Route("/api/auth/register/options")]
        [AllowAnonymous]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("authentication")]
        public async Task<ActionResult<CredentialCreateOptions>> MakeCredentialOptionsAsync(
            [FromBody] RegistrationOptionsRequest request,
            CancellationToken cancellationToken)
        {
            string username = request.Username.Trim();
            if (username.Length is < 1 or > 64 || username.Any(char.IsControl))
                return BadRequest("Username must be between 1 and 64 characters.");
            if (await userContext.Users.AsNoTracking().AnyAsync(u => u.Username == username, cancellationToken))
                return Conflict("Unable to create this account.");

            bool hasUsers = await userContext.Users.AsNoTracking().AnyAsync(cancellationToken);
            PendingRegistration pending;
            if (!hasUsers)
            {
                pending = new PendingRegistration(
                    new User { Fido2Id = GenerateCredentialId(), Username = username, Preferences = new(), Role = UserRole.Owner },
                    IsBootstrap: true,
                    InvitationId: null,
                    InvitationTokenHash: null);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.InviteToken))
                    return StatusCode(StatusCodes.Status403Forbidden, "A valid invitation is required.");

                if (!InvitationTokens.TryHash(request.InviteToken, out string tokenHash))
                    return StatusCode(StatusCodes.Status403Forbidden, "A valid invitation is required.");
                DateTimeOffset now = timeProvider.GetUtcNow();
                Invitation? invitation = await userContext.Invitations.AsNoTracking().FirstOrDefaultAsync(
                    i => i.TokenHash == tokenHash
                        && i.RedeemedAt == null
                        && i.RevokedAt == null,
                    cancellationToken);
                if (invitation is null || invitation.ExpiresAt <= now)
                    return StatusCode(StatusCodes.Status403Forbidden, "A valid invitation is required.");

                pending = new PendingRegistration(
                    new User { Fido2Id = GenerateCredentialId(), Username = username, Preferences = new(), Role = UserRole.Member },
                    IsBootstrap: false,
                    invitation.Id,
                    tokenHash);
            }

            return Ok(GetOptionsForNewUserCreation(pending));
        }

        [HttpPost]
        [Route("/api/auth/register/complete")]
        [AllowAnonymous]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("authentication")]
        public async Task<ActionResult<RegisteredPublicKeyCredential>> MakeCredential(
            [FromBody] CredentialRegistrationRequest attestationResponse,
            CancellationToken cancellationToken)
        {
            try
            {
                RegisteredPublicKeyCredential credential = await CreateCredentialForNewUser(
                    attestationResponse.Attestation,
                    attestationResponse.CredentialName,
                    cancellationToken);
                return Ok(credential);
            }
            catch (RegistrationGrantException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (DbUpdateException)
            {
                return Conflict("Unable to create this account.");
            }
        }

        private CredentialCreateOptions GetOptionsForNewUserCreation(PendingRegistration pending)
        {
            User user = pending.User;
            var fido2User = new Fido2User
            {
                DisplayName = user.Username,
                Name = user.Username,
                Id = user.Fido2Id
            };

            var authenticatorSelection = new AuthenticatorSelection
            {
                AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required
            };

            var exts = new AuthenticationExtensionsClientInputs()
            {
                Extensions = true,
                UserVerificationMethod = true,
                CredProps = true
            };

            var options = fido2.RequestNewCredential(
                new RequestNewCredentialParams
                {
                    User = fido2User,
                    AuthenticatorSelection = authenticatorSelection,
                    AttestationPreference = AttestationConveyancePreference.Direct,
                    Extensions = exts
                });

            string challenge = Base64Url.EncodeToString(options.Challenge);
            memoryCache.Set($"{_credentialOptionsCacheKey}.{challenge}", options, TimeSpan.FromMinutes(5));
            memoryCache.Set($"{_userCacheKey}.{challenge}", pending, TimeSpan.FromMinutes(5));

            return options;
        }

        private async Task<RegisteredPublicKeyCredential> CreateCredentialForNewUser(
            AuthenticatorAttestationRawResponse attestationResponse,
            string credentialName,
            CancellationToken cancellationToken)
        {
            var clientData = CollectedClientData.FromRawAttestation(attestationResponse.Response.ClientDataJson);
            var options = memoryCache
                .Get<CredentialCreateOptions>($"{_credentialOptionsCacheKey}.{clientData.Challenge}")
                ?? throw new InvalidOperationException("Credential options not found for this user");
            PendingRegistration pending = memoryCache.Get<PendingRegistration>($"{_userCacheKey}.{clientData.Challenge}")
                ?? throw new RegistrationGrantException("Registration attempt expired.");
            User user = pending.User;
            var fido2User = new Fido2User
            {
                DisplayName = user.Username,
                Name = user.Username,
                Id = user.Fido2Id
            };

            async Task<bool> IsCredentialIdUniqueToUserCallback(IsCredentialIdUniqueToUserParams args, CancellationToken cancellationToken)
            {
                var user = userContext.Fido2Credentials.AsNoTracking()
                    .FirstOrDefault(c => c.Id.SequenceEqual(args.CredentialId));

                if (user is not null)
                    return false;

                return true;
            }

            var credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = IsCredentialIdUniqueToUserCallback
            }, cancellationToken: cancellationToken);

            var authenticator = await VerifyAuthenticator(credential, cancellationToken);

            Fido2Credential storedCredential = new()
            {
                Id = credential.Id,
                PublicKey = credential.PublicKey,
                UserFido2Id = fido2User.Id,
                UserId = user.Id,
                SignCount = credential.SignCount,
                Type = credential.Type,
                RegDate = DateTime.UtcNow,
                CredentialName = authenticator?.MetadataStatement.Description ?? credentialName,
                AaGuid = credential.AaGuid,
                Transports = credential.Transports,
                IsBackedUp = credential.IsBackedUp,
                IsBackupEligible = credential.IsBackupEligible,
                AttestationFormat = credential.AttestationFormat,
                AttestationClientDataJson = credential.AttestationClientDataJson,
                AttestationObject = credential.AttestationObject,
            };
            user.Fido2Credentials.Add(storedCredential);

            await using var transaction = await userContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            if (await userContext.Users.AnyAsync(u => u.Username == user.Username, cancellationToken))
                throw new RegistrationGrantException("Unable to create this account.");

            Invitation? invitation = null;
            if (pending.IsBootstrap)
            {
                if (await userContext.Users.AnyAsync(cancellationToken))
                    throw new RegistrationGrantException("Owner registration is already complete.");
            }
            else
            {
                DateTimeOffset now = timeProvider.GetUtcNow();
                invitation = await userContext.Invitations.FirstOrDefaultAsync(
                    i => i.Id == pending.InvitationId
                        && i.TokenHash == pending.InvitationTokenHash
                        && i.RedeemedAt == null
                        && i.RevokedAt == null,
                    cancellationToken);
                if (invitation is null || invitation.ExpiresAt <= now)
                    throw new RegistrationGrantException("Invitation is invalid, expired, or already used.");
            }

            userContext.Users.Add(user);
            await userContext.SaveChangesAsync(cancellationToken);

            if (invitation is not null)
            {
                invitation.RedeemedAt = timeProvider.GetUtcNow();
                invitation.RedeemedByUserId = user.Id;
                await userContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            memoryCache.Remove($"{_credentialOptionsCacheKey}.{clientData.Challenge}");
            memoryCache.Remove($"{_userCacheKey}.{clientData.Challenge}");

            await LogIn(user);

            return credential;
        }

        private static byte[] GenerateCredentialId(int length = 32)
        {
            var credentialId = new byte[length];
            RandomNumberGenerator.Fill(credentialId);
            return credentialId;
        }

        private List<PublicKeyCredentialDescriptor> GetExistingCredentials(User user)
        {
            return userContext.Fido2Credentials
                .Where(c => c.UserFido2Id == user.Fido2Id)
                .Select(c => new PublicKeyCredentialDescriptor(c.Id))
                .ToList();
        }

        private async Task<MetadataBLOBPayloadEntry?> VerifyAuthenticator(RegisteredPublicKeyCredential credential, CancellationToken cancellationToken)
        {
            var entry = await mds.GetEntryAsync(credential.AaGuid, cancellationToken);

            if (entry is not null)
            {
                foreach (var statusReport in entry.StatusReports)
                {
                    if (statusReport.Status is AuthenticatorStatus.REVOKED || statusReport.Status is AuthenticatorStatus.ATTESTATION_KEY_COMPROMISE)
                    {
                        throw new InvalidDataException("The authenticator used is compromised or revoked.");
                    }
                }
            }

            return entry;
        }
    }

    internal sealed record CollectedClientData
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; } // e.g., "webauthn.create" or "webauthn.get"

        [JsonPropertyName("challenge")]
        public required string Challenge { get; set; } // Base64Url encoded string

        [JsonPropertyName("origin")]
        public required string Origin { get; set; } // e.g., "https://yoursite.com"

        [JsonPropertyName("crossOrigin")]
        public bool? CrossOrigin { get; set; } // Optional boolean

        public static CollectedClientData FromRawAttestation(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes);
            var clientData = JsonSerializer.Deserialize<CollectedClientData>(json)
                ?? throw new InvalidOperationException("Failed to deserialize CollectedClientData from JSON.");

            return clientData;
        }
    }

    public record CredentialRegistrationRequest
    {
        public required AuthenticatorAttestationRawResponse Attestation { get; set; }
        public required string CredentialName { get; set; }
    }

    public sealed record RegistrationStatusDto(string Mode);
    public sealed record RegistrationOptionsRequest(string Username, string? InviteToken);
    internal sealed record PendingRegistration(
        User User,
        bool IsBootstrap,
        int? InvitationId,
        string? InvitationTokenHash);
    internal sealed class RegistrationGrantException(string message) : Exception(message);
}

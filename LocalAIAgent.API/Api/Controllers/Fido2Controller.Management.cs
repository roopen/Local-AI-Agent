using Fido2NetLib;
using Fido2NetLib.Objects;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers.Text;
using System.Security.Claims;

namespace LocalAIAgent.API.Api.Controllers
{
    public partial class Fido2Controller
    {
        [Authorize]
        [HttpPost]
        [Route("/makeCredentialOptionsExistingUser")]
        public async Task<CredentialCreateOptions> MakeCredentialOptionsForExistingUserAsync()
        {
            string? userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId))
            {
                throw new UnauthorizedAccessException("User ID not found in claims.");
            }

            var user = await getUserUseCase.GetUserById(userId) ?? throw new InvalidOperationException("User not found");

            var fido2User = new Fido2User
            {
                DisplayName = user.Username,
                Name = user.Username,
                Id = user.Fido2Id
            };

            var existingKeys = GetExistingCredentials(new User
            {
                Fido2Id = user.Fido2Id,
                Username = user.Username,
                Id = user.Id
            });

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
                    ExcludeCredentials = existingKeys.ToList(),
                    AuthenticatorSelection = authenticatorSelection,
                    AttestationPreference = AttestationConveyancePreference.Direct,
                    Extensions = exts
                });

            string challenge = Base64Url.EncodeToString(options.Challenge);

            memoryCache.Set($"{_credentialOptionsCacheKey}.{challenge}", options, TimeSpan.FromMinutes(5));

            return options;
        }

        [Authorize]
        [HttpPost]
        [Route("/addCredentialExistingUser")]
        public async Task<RegisteredPublicKeyCredential> AddCredentialToExistingUser(
            [FromBody] CredentialRegistrationRequest attestationRequest,
            CancellationToken cancellationToken)
        {
            var clientData = CollectedClientData.FromRawAttestation(attestationRequest.Attestation.Response.ClientDataJson);
            var options = memoryCache
                .Get<CredentialCreateOptions>($"{_credentialOptionsCacheKey}.{clientData.Challenge}")
                ?? throw new InvalidOperationException("Credential options not found for this user");

            string? userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId))
            {
                throw new UnauthorizedAccessException("User ID not found in claims.");
            }
            var user = await getUserUseCase.GetUserById(userId) ?? throw new InvalidOperationException("User not found");

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
                AttestationResponse = attestationRequest.Attestation,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = IsCredentialIdUniqueToUserCallback
            }, cancellationToken: cancellationToken);

            var authenticator = await VerifyAuthenticator(credential, cancellationToken);

            userContext.Fido2Credentials.Add(new Fido2Credential
            {
                Id = credential.Id,
                UserFido2Id = fido2User.Id,
                UserId = user.Id,
                CredentialName = authenticator?.MetadataStatement.Description ?? attestationRequest.CredentialName,
                PublicKey = credential.PublicKey,
                User = fido2User,
                SignCount = credential.SignCount,
                Type = credential.Type,
                RegDate = DateTime.UtcNow,
                AaGuid = credential.AaGuid,
                Transports = credential.Transports,
                IsBackedUp = credential.IsBackedUp,
                IsBackupEligible = credential.IsBackupEligible,
                AttestationFormat = credential.AttestationFormat,
                AttestationClientDataJson = credential.AttestationClientDataJson,
                AttestationObject = credential.AttestationObject,
            });
            await userContext.SaveChangesAsync(cancellationToken);

            memoryCache.Remove($"{_credentialOptionsCacheKey}.{clientData.Challenge}");
            memoryCache.Remove($"{_userCacheKey}.{clientData.Challenge}");

            return credential;
        }

        [Authorize]
        [HttpPost]
        [Route("/removeCredential")]
        public async Task<IActionResult> RemoveCredential([FromBody] byte[] credentialId, CancellationToken cancellationToken)
        {
            string? userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId))
                throw new UnauthorizedAccessException("User ID not found in claims.");

            var user = await getUserUseCase.GetUserById(userId) ?? throw new InvalidOperationException("User not found");

            var credentials = await userContext.Fido2Credentials
                .Where(c => c.UserFido2Id == user.Fido2Id.Value).ToListAsync(cancellationToken);

            var credential = credentials.FirstOrDefault(c => c.Id.SequenceEqual(credentialId));

            if (credential == null)
                return NotFound("Credential not found for this user.");

            userContext.Fido2Credentials.Remove(credential);
            await userContext.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [Authorize]
        [HttpGet]
        [Route("/listCredentials")]
        public async Task<List<CredentialInfo>> ListCredentials(CancellationToken cancellationToken)
        {
            string? userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId))
                throw new UnauthorizedAccessException("User ID not found in claims.");

            var user = await getUserUseCase.GetUserById(userId) ?? throw new InvalidOperationException("User not found");

            var credentials = await userContext.Fido2Credentials
                .Where(c => c.UserFido2Id == user.Fido2Id.Value)
                .Select(c => new CredentialInfo
                {
                    Name = c.CredentialName,
                    Id = c.Id,
                    RegDate = c.RegDate,
                    AaGuid = c.AaGuid,
                }).ToListAsync(cancellationToken);

            return credentials;
        }
    }

    public record CredentialCreateRequest
    {
        public required string CredentialName { get; init; }
        public required AuthenticatorAttestationRawResponse authenticatorAttestationRawResponse { get; init; }
    }

    public class CredentialInfo
    {
        public required string Name { get; set; }
        public required byte[] Id { get; set; }
        public DateTimeOffset RegDate { get; set; }
        public Guid AaGuid { get; set; }
    }
}

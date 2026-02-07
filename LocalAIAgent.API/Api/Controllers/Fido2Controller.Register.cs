using Fido2NetLib;
using Fido2NetLib.Objects;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIAgent.API.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public partial class Fido2Controller(
        IFido2 fido2,
        IMemoryCache memoryCache,
        UserContext userContext,
        IGetUserUseCase getUserUseCase,
        IMetadataService mds) : ControllerBase
    {
        [HttpPost]
        [Route("/makeCredentialOptions")]
        public async Task<CredentialCreateOptions> MakeCredentialOptionsAsync(string username)
        {
            return await GetOptionsForNewUserCreation(username);
        }

        [HttpPost]
        [Route("/makeCredential")]
        public async Task<RegisteredPublicKeyCredential> MakeCredential(
            [FromBody] CredentialRegistrationRequest attestationResponse,
            CancellationToken cancellationToken)
        {
            return await CreateCredentialForNewUser(attestationResponse.Attestation, attestationResponse.CredentialName, cancellationToken);
        }

        private async Task<CredentialCreateOptions> GetOptionsForNewUserCreation(string username)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentNullException(nameof(username));

            User user = new() { Fido2Id = GenerateCredentialId(), Username = username, Preferences = new() };
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
            memoryCache.Set($"{_userCacheKey}.{challenge}", user, TimeSpan.FromMinutes(5));

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
            var user = memoryCache.Get<User>($"{_userCacheKey}.{clientData.Challenge}")
                ?? throw new InvalidOperationException("User not found for this credential creation");
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

            user.Fido2Credentials.Add(new Fido2Credential
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
            });
            userContext.Users.Add(user);
            await userContext.SaveChangesAsync(cancellationToken);

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

    internal record CollectedClientData
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
}
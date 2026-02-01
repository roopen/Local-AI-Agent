using Fido2NetLib;
using Fido2NetLib.Objects;
using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIAgent.API.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Fido2Controller(
        IFido2 fido2,
        IMemoryCache memoryCache,
        UserContext userContext,
        ICreateUserUseCase createUserUseCase) : ControllerBase
    {
        public static IMetadataService? _mds;
        private const string _credentialOptionsCacheKey = "fido2.credentialOptions";
        private const string _assertionOptionsCacheKey = "fido2.assertionOptions";
        private const string _userCacheKey = "fido2.user";

        [HttpPost]
        [Route("/makeCredentialOptions")]
        public async Task<CredentialCreateOptions> MakeCredentialOptionsAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentNullException(nameof(username));

            try
            {
                await createUserUseCase.CreateUser(new UserRegistrationDto { Username = username });
            }
            catch { }
            User user = await GetUserAsync(username);
            var fido2User = new Fido2User
            {
                DisplayName = user.Username,
                Name = user.Username,
                Id = user.Fido2Id
            };
            var existingKeys = GetExistingCredentials(user);

            var authenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Preferred,
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
            memoryCache.Set($"{_userCacheKey}.{challenge}", user, TimeSpan.FromMinutes(5));

            return options;
        }

        [HttpPost]
        [Route("/makeCredential")]
        public async Task<RegisteredPublicKeyCredential> MakeCredential(
            [FromBody] AuthenticatorAttestationRawResponse attestationResponse,
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
                var user = userContext.Fido2Credentials
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

            userContext.Fido2Credentials.Add(new Infrastructure.Models.Fido2Credential
            {
                Id = credential.Id,
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

        [HttpPost]
        [Route("/assertionOptions")]
        public async Task<AssertionOptions> AssertionOptionsPostAsync()
        {
            var exts = new AuthenticationExtensionsClientInputs()
            {
                Extensions = true,
                UserVerificationMethod = true
            };

            var uv = UserVerificationRequirement.Required;
            var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams()
            {
                UserVerification = uv,
                Extensions = exts
            });

            string challenge = Base64Url.EncodeToString(options.Challenge);
            memoryCache.Set($"{_assertionOptionsCacheKey}.{challenge}", options, TimeSpan.FromMinutes(5));

            return options;
        }

        [HttpPost]
        [Route("/makeAssertion")]
        public async Task<AttestationResult> MakeAssertion([FromBody] AuthenticatorAssertionRawResponse clientResponse, CancellationToken cancellationToken)
        {
            var clientData = CollectedClientData.FromRawAttestation(clientResponse.Response.ClientDataJson);
            var options = memoryCache
                .Get<AssertionOptions>($"{_assertionOptionsCacheKey}.{clientData.Challenge}")
                ?? throw new InvalidOperationException("Assertion options not found for this user");

            var credential = userContext.Fido2Credentials.Where(c => c.Id == clientResponse.RawId).FirstOrDefault()
                ?? throw new InvalidDataException("Unknown credentials");

            var storedCounter = credential.SignCount;

            async Task<bool> IsUserHandleOwnerOfCredentialIdCallback(IsUserHandleOwnerOfCredentialIdParams args, CancellationToken cancellationToken)
            {
                var storedCreds = await userContext.Fido2Credentials.Where(c => c.User.Id == args.UserHandle).ToListAsync(cancellationToken);
                return storedCreds.Exists(c => c.Id.SequenceEqual(args.CredentialId));
            }

            var res = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = clientResponse,
                OriginalOptions = options,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = storedCounter,
                IsUserHandleOwnerOfCredentialIdCallback = IsUserHandleOwnerOfCredentialIdCallback
            }, cancellationToken: cancellationToken);

            credential.SignCount = res.SignCount;
            await userContext.SaveChangesAsync(cancellationToken);

            memoryCache.Remove($"{_assertionOptionsCacheKey}.{clientData.Challenge}");

            var userId = await userContext.Fido2Credentials
                .Where(c => c.Id == clientResponse.RawId)
                .Select(c => c.User.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var user = await userContext.Users.Where(u => u.Fido2Id == userId).FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidDataException("user not found");

            await LogIn(user);

            return new AttestationResult { User = user, VerifyAssertionResult = res };
        }

        private async Task<User> GetUserAsync(string username)
        {
            return await userContext.Users.Where(u => u.Username == username).FirstOrDefaultAsync() ?? throw new InvalidDataException("user not found");
        }

        private List<PublicKeyCredentialDescriptor> GetExistingCredentials(User user)
        {
            return userContext.Fido2Credentials
                .Where(c => c.User.Id == user.Fido2Id)
                .Select(c => new PublicKeyCredentialDescriptor(c.Id))
                .ToList();
        }

        private async Task LogIn(User user)
        {
            List<Claim> claims =
            [
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, "User"),
                new Claim("amr", "mfa"),
                new Claim("amr", "passwordless")
            ];

            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(3600)
                });
        }
    }

    public class CollectedClientData
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

    public record AttestationResult
    {
        public required VerifyAssertionResult VerifyAssertionResult { get; set; }
        public required User User { get; set; }
    }
}
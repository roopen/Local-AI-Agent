using Fido2NetLib;
using Fido2NetLib.Objects;
using LocalAIAgent.API.Infrastructure.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers.Text;

namespace LocalAIAgent.API.Api.Controllers
{
    public partial class Fido2Controller
    {
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
                .Select(c => c.UserFido2Id)
                .FirstOrDefaultAsync(cancellationToken);
            var user = await userContext.Users.Where(u => u.Fido2Id == userId).FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidDataException("User not found");

            await LogIn(user);

            return new AttestationResult { User = user.MapToDomainUser(), VerifyAssertionResult = res };
        }

        public record AttestationResult
        {
            public required VerifyAssertionResult VerifyAssertionResult { get; set; }
            public required Domain.User User { get; set; }
        }
    }
}

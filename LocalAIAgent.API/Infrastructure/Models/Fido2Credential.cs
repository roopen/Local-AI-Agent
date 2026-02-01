using Fido2NetLib.Objects;

namespace LocalAIAgent.API.Infrastructure.Models
{
    public class Fido2Credential : RegisteredPublicKeyCredential
    {
        public DateTimeOffset RegDate { get; set; }
        public new uint SignCount { get; set; }
    }
}
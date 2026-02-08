using Fido2NetLib;
using Fido2NetLib.Objects;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalAIAgent.API.Infrastructure.Models
{
    public class Fido2Credential : RegisteredPublicKeyCredential
    {
        public required string CredentialName { get; set; }
        public DateTimeOffset RegDate { get; set; }
        public new uint SignCount { get; set; }

        [ForeignKey(nameof(Owner))]
        public required int UserId { get; set; }
        public virtual User? Owner { get; set; }
        public required byte[] UserFido2Id { get; set; }

        [NotMapped]
        public new Fido2User? User { get; set; }
    }
}
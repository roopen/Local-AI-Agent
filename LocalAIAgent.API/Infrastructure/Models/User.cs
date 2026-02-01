using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalAIAgent.API.Infrastructure.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    [ForeignKey(nameof(Fido2Id))]
    public required byte[] Fido2Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserPreferences? Preferences { get; set; }
    public List<Fido2Credential> Fido2Credentials { get; set; } = [];
}
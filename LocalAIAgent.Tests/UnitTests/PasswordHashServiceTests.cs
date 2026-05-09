using LocalAIAgent.API.Infrastructure;

namespace LocalAIAgent.Tests.UnitTests;

public class PasswordHashServiceTests
{
    private readonly PasswordHashService _sut = new();

    [Fact]
    public void Hash_ReturnsValueDifferentFromPlaintext()
    {
        string hash = _sut.Hash("hunter2");

        Assert.NotEqual("hunter2", hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Hash_TwoCallsForSamePassword_ProduceDifferentHashes()
    {
        // Salts are random; same input must yield different output to prevent rainbow tables.
        string a = _sut.Hash("hunter2");
        string b = _sut.Hash("hunter2");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        string hash = _sut.Hash("hunter2");

        Assert.True(_sut.Verify(hash, "hunter2"));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        string hash = _sut.Hash("hunter2");

        Assert.False(_sut.Verify(hash, "Hunter2"));
        Assert.False(_sut.Verify(hash, "hunter"));
        Assert.False(_sut.Verify(hash, ""));
    }

    [Fact]
    public void Verify_HashFromAnotherPassword_ReturnsFalse()
    {
        string hashA = _sut.Hash("first");
        string hashB = _sut.Hash("second");

        Assert.False(_sut.Verify(hashA, "second"));
        Assert.False(_sut.Verify(hashB, "first"));
    }
}

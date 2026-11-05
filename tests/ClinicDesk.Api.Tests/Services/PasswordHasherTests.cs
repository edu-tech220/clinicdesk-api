using ClinicDesk.Api.Infrastructure;

namespace ClinicDesk.Api.Tests.Services;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var (hash, salt) = _hasher.Hash("Sup3rSecreta!");

        Assert.True(_hasher.Verify("Sup3rSecreta!", hash, salt));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var (hash, salt) = _hasher.Hash("Sup3rSecreta!");

        Assert.False(_hasher.Verify("OtraClave!", hash, salt));
    }

    [Fact]
    public void Hash_ProducesDifferentOutputs_ForSamePassword()
    {
        var first = _hasher.Hash("Sup3rSecreta!");
        var second = _hasher.Hash("Sup3rSecreta!");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }
}

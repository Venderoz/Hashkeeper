namespace Hashkeeper.Crypto.Tests;

public class Argon2idMasterKeyDeriverTests
{
    private const string Password = "correct horse battery staple";

    private static readonly byte[] FixedSalt = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");

    // Expected outputs computed independently with argon2-cffi (reference Argon2 C library):
    // Argon2id v0x13, t = 3, m = 65536 KiB, p = 1, length = 32, salt = FixedSalt.
    [Theory]
    [InlineData(Password, "0d1a3c6523c8f06e4e0af9c515aa5b5448cfebd6838f2d52c3d8b6ef8ddc3c2e")]
    [InlineData("Zażółć gęślą jaźń", "4a3cc505bc1fa23e5d815baf2983adca9291b12496654455c38f9031029e1612")]
    public void DeriveMasterKey_MatchesReferenceArgon2id(string password, string expectedHex)
    {
        var masterKey = Argon2idMasterKeyDeriver.DeriveMasterKey(password, FixedSalt);

        Assert.Equal(expectedHex, Convert.ToHexStringLower(masterKey));
    }

    [Fact]
    public void DeriveMasterKey_IsDeterministic()
    {
        var salt = Argon2idMasterKeyDeriver.GenerateSalt();

        var first = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, salt);
        var second = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, salt);

        Assert.Equal(Argon2idMasterKeyDeriver.KeySize, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void DeriveMasterKey_DifferentSalts_GiveDifferentKeys()
    {
        var first = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, Argon2idMasterKeyDeriver.GenerateSalt());
        var second = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, Argon2idMasterKeyDeriver.GenerateSalt());

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DeriveMasterKey_DifferentPasswords_GiveDifferentKeys()
    {
        var first = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, FixedSalt);
        var second = Argon2idMasterKeyDeriver.DeriveMasterKey(Password + "!", FixedSalt);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DeriveMasterKey_DifferentIterations_GiveDifferentKeys()
    {
        var defaults = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, FixedSalt);
        var moreIterations = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, FixedSalt, iterations: 4);

        Assert.NotEqual(defaults, moreIterations);
    }

    [Fact]
    public void DeriveMasterKey_DifferentMemorySizes_GiveDifferentKeys()
    {
        var defaults = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, FixedSalt);
        var moreMemory = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, FixedSalt, memoryKiB: 2 * 65536);

        Assert.NotEqual(defaults, moreMemory);
    }

    [Theory]
    // "ą" as one code point (U+0105) and as "a" + combining ogonek (U+0328): canonical equivalence.
    [InlineData("p\u0105ss", "pa\u0328ss")]
    // The "fi" ligature (U+FB01) and "fi": compatibility equivalence, which NFC would not fold.
    [InlineData("\uFB01sh", "fish")]
    public void DeriveMasterKey_NormalisesPasswordToNfkc(string password, string equivalent)
    {
        Assert.NotEqual(password, equivalent);

        var first = Argon2idMasterKeyDeriver.DeriveMasterKey(password, FixedSalt);
        var second = Argon2idMasterKeyDeriver.DeriveMasterKey(equivalent, FixedSalt);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeriveMasterKey_DoesNotTrimPassword()
    {
        var plain = Argon2idMasterKeyDeriver.DeriveMasterKey(Password, FixedSalt);
        var padded = Argon2idMasterKeyDeriver.DeriveMasterKey(" " + Password + " ", FixedSalt);

        Assert.NotEqual(plain, padded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(32)]
    public void DeriveMasterKey_InvalidSaltLength_Throws(int length)
    {
        Assert.Throws<ArgumentException>(
            "salt", () => Argon2idMasterKeyDeriver.DeriveMasterKey(Password, new byte[length]));
    }

    [Theory]
    [InlineData(2, Argon2idMasterKeyDeriver.DefaultMemoryKiB)]
    [InlineData(Argon2idMasterKeyDeriver.DefaultIterations, 65535)]
    [InlineData(17, Argon2idMasterKeyDeriver.DefaultMemoryKiB)]
    [InlineData(int.MaxValue, Argon2idMasterKeyDeriver.DefaultMemoryKiB)]
    [InlineData(Argon2idMasterKeyDeriver.DefaultIterations, 1024 * 1024 + 1)]
    [InlineData(Argon2idMasterKeyDeriver.DefaultIterations, int.MaxValue)]
    public void DeriveMasterKey_ParametersOutOfRange_Throw(int iterations, int memoryKiB)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Argon2idMasterKeyDeriver.DeriveMasterKey(Password, FixedSalt, iterations, memoryKiB));
    }

    // MaxMemoryKiB is not exercised: a 1 GiB derivation is too heavy for a unit test.
    [Fact]
    public void DeriveMasterKey_AcceptsMaxIterations()
    {
        var masterKey = Argon2idMasterKeyDeriver.DeriveMasterKey(
            Password, FixedSalt, iterations: Argon2idMasterKeyDeriver.MaxIterations);

        Assert.Equal(Argon2idMasterKeyDeriver.KeySize, masterKey.Length);
    }

    [Fact]
    public void DeriveMasterKey_EmptyPassword_ReturnsKey()
    {
        var masterKey = Argon2idMasterKeyDeriver.DeriveMasterKey("", FixedSalt);

        Assert.Equal(Argon2idMasterKeyDeriver.KeySize, masterKey.Length);
    }

    [Fact]
    public void DeriveMasterKey_NullPassword_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Argon2idMasterKeyDeriver.DeriveMasterKey(null!, FixedSalt));
    }

    [Fact]
    public void GenerateSalt_Returns16RandomBytes()
    {
        var first = Argon2idMasterKeyDeriver.GenerateSalt();
        var second = Argon2idMasterKeyDeriver.GenerateSalt();

        Assert.Equal(Argon2idMasterKeyDeriver.SaltSize, first.Length);
        Assert.Equal(Argon2idMasterKeyDeriver.SaltSize, second.Length);
        Assert.NotEqual(first, second);
    }
}

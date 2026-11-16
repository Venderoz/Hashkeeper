using System.Security.Cryptography;

namespace Hashkeeper.Crypto.Tests;

public class HkdfSubkeyDeriverTests
{
    private static byte[] NewMasterKey() => RandomNumberGenerator.GetBytes(HkdfSubkeyDeriver.KeySize);

    [Fact]
    public void DeriveAuthHash_MatchesDotNetHkdf()
    {
        var masterKey = NewMasterKey();

        var expected = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, masterKey, 32, salt: [], info: "hashkeeper:v1:auth-hash"u8.ToArray());

        Assert.Equal(expected, HkdfSubkeyDeriver.DeriveAuthHash(masterKey));
    }

    [Fact]
    public void DeriveWrapKey_MatchesDotNetHkdf()
    {
        var masterKey = NewMasterKey();

        var expected = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, masterKey, 32, salt: [], info: "hashkeeper:v1:vault-key-wrap"u8.ToArray());

        Assert.Equal(expected, HkdfSubkeyDeriver.DeriveWrapKey(masterKey));
    }

    [Fact]
    public void Derive_IsDeterministic()
    {
        var masterKey = NewMasterKey();

        Assert.Equal(HkdfSubkeyDeriver.DeriveAuthHash(masterKey), HkdfSubkeyDeriver.DeriveAuthHash(masterKey));
        Assert.Equal(HkdfSubkeyDeriver.DeriveWrapKey(masterKey), HkdfSubkeyDeriver.DeriveWrapKey(masterKey));
    }

    [Fact]
    public void AuthHashAndWrapKey_AreDifferent_AndNotEqualToMasterKey()
    {
        var masterKey = NewMasterKey();

        var authHash = HkdfSubkeyDeriver.DeriveAuthHash(masterKey);
        var wrapKey = HkdfSubkeyDeriver.DeriveWrapKey(masterKey);

        Assert.Equal(HkdfSubkeyDeriver.KeySize, authHash.Length);
        Assert.Equal(HkdfSubkeyDeriver.KeySize, wrapKey.Length);
        Assert.NotEqual(authHash, wrapKey);
        Assert.NotEqual(masterKey, authHash);
        Assert.NotEqual(masterKey, wrapKey);
    }

    [Fact]
    public void Derive_DifferentMasterKeys_GiveDifferentSubkeys()
    {
        var first = NewMasterKey();
        var second = NewMasterKey();

        Assert.NotEqual(HkdfSubkeyDeriver.DeriveAuthHash(first), HkdfSubkeyDeriver.DeriveAuthHash(second));
        Assert.NotEqual(HkdfSubkeyDeriver.DeriveWrapKey(first), HkdfSubkeyDeriver.DeriveWrapKey(second));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Derive_InvalidMasterKeyLength_Throws(int length)
    {
        Assert.Throws<ArgumentException>(() => HkdfSubkeyDeriver.DeriveAuthHash(new byte[length]));
        Assert.Throws<ArgumentException>(() => HkdfSubkeyDeriver.DeriveWrapKey(new byte[length]));
    }

    [Fact]
    public void WrapKey_WorksWithVaultKeyWrapper()
    {
        var masterKey = NewMasterKey();
        var vaultKey = VaultKeyWrapper.GenerateVaultKey();

        var wrapped = VaultKeyWrapper.Wrap(vaultKey, HkdfSubkeyDeriver.DeriveWrapKey(masterKey));
        var unwrapped = VaultKeyWrapper.Unwrap(wrapped, HkdfSubkeyDeriver.DeriveWrapKey(masterKey));

        Assert.Equal(vaultKey, unwrapped);
    }
}
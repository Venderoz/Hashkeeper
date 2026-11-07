using System.Security.Cryptography;

namespace Hashkeeper.Crypto.Tests;

public class VaultKeyWrapperTests
{
    private static byte[] NewMasterKey() => RandomNumberGenerator.GetBytes(VaultKeyWrapper.KeySize);

    [Fact]
    public void GenerateVaultKey_ReturnsRandom32ByteKeys()
    {
        var first = VaultKeyWrapper.GenerateVaultKey();
        var second = VaultKeyWrapper.GenerateVaultKey();

        Assert.Equal(VaultKeyWrapper.KeySize, first.Length);
        Assert.Equal(VaultKeyWrapper.KeySize, second.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Wrap_ThenUnwrap_ReturnsOriginalKey()
    {
        var vaultKey = VaultKeyWrapper.GenerateVaultKey();
        var masterKey = NewMasterKey();

        var wrapped = VaultKeyWrapper.Wrap(vaultKey, masterKey);
        var unwrapped = VaultKeyWrapper.Unwrap(wrapped, masterKey);

        Assert.Equal(vaultKey, unwrapped);
    }

    [Fact]
    public void Wrap_ProducesVersionedOutputOfExpectedSize()
    {
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), NewMasterKey());

        Assert.Equal(VaultKeyWrapper.WrappedSize, wrapped.Length);
        Assert.Equal(VaultKeyWrapper.FormatVersion, wrapped[0]);
    }

    [Fact]
    public void Wrap_DoesNotContainPlaintextKey()
    {
        var vaultKey = VaultKeyWrapper.GenerateVaultKey();

        var wrapped = VaultKeyWrapper.Wrap(vaultKey, NewMasterKey());

        Assert.Equal(-1, wrapped.AsSpan().IndexOf(vaultKey));
    }

    [Fact]
    public void Wrap_SameKeyTwice_UsesFreshNonceEachTime()
    {
        var vaultKey = VaultKeyWrapper.GenerateVaultKey();
        var masterKey = NewMasterKey();

        var first = VaultKeyWrapper.Wrap(vaultKey, masterKey);
        var second = VaultKeyWrapper.Wrap(vaultKey, masterKey);

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.AsSpan(1, 24).ToArray(), second.AsSpan(1, 24).ToArray());
    }

    [Fact]
    public void Unwrap_WithWrongMasterKey_Throws()
    {
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), NewMasterKey());

        Assert.Throws<CryptographicException>(() => VaultKeyWrapper.Unwrap(wrapped, NewMasterKey()));
    }

    [Theory]
    [InlineData(1)]                               // first nonce byte
    [InlineData(24)]                              // last nonce byte
    [InlineData(25)]                              // first ciphertext byte
    [InlineData(VaultKeyWrapper.WrappedSize - 1)] // last tag byte
    public void Unwrap_TamperedData_Throws(int index)
    {
        var masterKey = NewMasterKey();
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), masterKey);

        wrapped[index] ^= 0x01;

        Assert.Throws<CryptographicException>(() => VaultKeyWrapper.Unwrap(wrapped, masterKey));
    }

    [Fact]
    public void Unwrap_UnsupportedVersion_Throws()
    {
        var masterKey = NewMasterKey();
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), masterKey);

        wrapped[0] = 2;

        Assert.Throws<CryptographicException>(() => VaultKeyWrapper.Unwrap(wrapped, masterKey));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(VaultKeyWrapper.WrappedSize - 1)]
    [InlineData(VaultKeyWrapper.WrappedSize + 1)]
    public void Unwrap_InvalidLength_Throws(int length)
    {
        Assert.Throws<CryptographicException>(() => VaultKeyWrapper.Unwrap(new byte[length], NewMasterKey()));
    }

    [Theory]
    [InlineData(16, 32)]
    [InlineData(32, 31)]
    [InlineData(32, 0)]
    public void Wrap_InvalidKeyLength_Throws(int vaultKeyLength, int masterKeyLength)
    {
        Assert.Throws<ArgumentException>(
            () => VaultKeyWrapper.Wrap(new byte[vaultKeyLength], new byte[masterKeyLength]));
    }

    [Fact]
    public void Unwrap_InvalidMasterKeyLength_Throws()
    {
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), NewMasterKey());

        Assert.Throws<ArgumentException>(() => VaultKeyWrapper.Unwrap(wrapped, new byte[16]));
    }
}

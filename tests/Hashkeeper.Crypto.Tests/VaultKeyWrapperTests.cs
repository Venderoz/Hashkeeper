using System.Security.Cryptography;

namespace Hashkeeper.Crypto.Tests;

public class VaultKeyWrapperTests
{
    private static byte[] NewWrapKey() => RandomNumberGenerator.GetBytes(VaultKeyWrapper.KeySize);

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
        var wrapKey = NewWrapKey();

        var wrapped = VaultKeyWrapper.Wrap(vaultKey, wrapKey);
        var unwrapped = VaultKeyWrapper.Unwrap(wrapped, wrapKey);

        Assert.Equal(vaultKey, unwrapped);
    }

    [Fact]
    public void Wrap_ProducesVersionedOutputOfExpectedSize()
    {
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), NewWrapKey());

        Assert.Equal(VaultKeyWrapper.WrappedSize, wrapped.Length);
        Assert.Equal(VaultKeyWrapper.FormatVersion, wrapped[0]);
    }

    [Fact]
    public void Wrap_DoesNotContainPlaintextKey()
    {
        var vaultKey = VaultKeyWrapper.GenerateVaultKey();

        var wrapped = VaultKeyWrapper.Wrap(vaultKey, NewWrapKey());

        Assert.Equal(-1, wrapped.AsSpan().IndexOf(vaultKey));
    }

    [Fact]
    public void Wrap_SameKeyTwice_UsesFreshNonceEachTime()
    {
        var vaultKey = VaultKeyWrapper.GenerateVaultKey();
        var wrapKey = NewWrapKey();

        var first = VaultKeyWrapper.Wrap(vaultKey, wrapKey);
        var second = VaultKeyWrapper.Wrap(vaultKey, wrapKey);

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.AsSpan(1, 24).ToArray(), second.AsSpan(1, 24).ToArray());
    }

    [Fact]
    public void Unwrap_WithWrongWrapKey_Throws()
    {
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), NewWrapKey());

        Assert.Throws<CryptographicException>(() => VaultKeyWrapper.Unwrap(wrapped, NewWrapKey()));
    }

    [Theory]
    [InlineData(1)]                               // first nonce byte
    [InlineData(24)]                              // last nonce byte
    [InlineData(25)]                              // first ciphertext byte
    [InlineData(VaultKeyWrapper.WrappedSize - 1)] // last tag byte
    public void Unwrap_TamperedData_Throws(int index)
    {
        var wrapKey = NewWrapKey();
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), wrapKey);

        wrapped[index] ^= 0x01;

        Assert.Throws<CryptographicException>(() => VaultKeyWrapper.Unwrap(wrapped, wrapKey));
    }

    [Fact]
    public void Unwrap_UnsupportedVersion_Throws()
    {
        var wrapKey = NewWrapKey();
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), wrapKey);

        wrapped[0] = 2;

        Assert.Throws<CryptographicException>(() => VaultKeyWrapper.Unwrap(wrapped, wrapKey));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(VaultKeyWrapper.WrappedSize - 1)]
    [InlineData(VaultKeyWrapper.WrappedSize + 1)]
    public void Unwrap_InvalidLength_Throws(int length)
    {
        Assert.Throws<CryptographicException>(() => VaultKeyWrapper.Unwrap(new byte[length], NewWrapKey()));
    }

    [Theory]
    [InlineData(16, 32)]
    [InlineData(32, 31)]
    [InlineData(32, 0)]
    public void Wrap_InvalidKeyLength_Throws(int vaultKeyLength, int wrapKeyLength)
    {
        Assert.Throws<ArgumentException>(
            () => VaultKeyWrapper.Wrap(new byte[vaultKeyLength], new byte[wrapKeyLength]));
    }

    [Fact]
    public void Unwrap_InvalidWrapKeyLength_Throws()
    {
        var wrapped = VaultKeyWrapper.Wrap(VaultKeyWrapper.GenerateVaultKey(), NewWrapKey());

        Assert.Throws<ArgumentException>(() => VaultKeyWrapper.Unwrap(wrapped, new byte[16]));
    }
}

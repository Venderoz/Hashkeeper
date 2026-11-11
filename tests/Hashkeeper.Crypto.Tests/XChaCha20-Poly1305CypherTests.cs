using System;
using System.Collections.Generic;
using System.Text;

namespace Hashkeeper.Crypto.Tests
{
    internal class XChaCha20_Poly1305CypherTests
    {
        private static byte[] NewKey() =>
            RandomNumberGenerator.GetBytes(XChaCha20Poly1305Cipher.KeySize);

        [Fact]
        public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
        {
            var plaintext = RandomNumberGenerator.GetBytes(128);
            var key = NewKey();
            var associatedData = "Hashkeeper test context"u8.ToArray();

            var encrypted = XChaCha20Poly1305Cipher.Encrypt(plaintext, key, associatedData);
            var decrypted = XChaCha20Poly1305Cipher.Decrypt(encrypted, key, associatedData);

            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void Encrypt_ProducesOutputOfExpectedSize()
        {
            var plaintext = RandomNumberGenerator.GetBytes(128);

            var encrypted = XChaCha20Poly1305Cipher.Encrypt(plaintext, NewKey(), []);

            Assert.Equal(
                XChaCha20Poly1305Cipher.NonceSize + plaintext.Length + XChaCha20Poly1305Cipher.TagSize,
                encrypted.Length);
        }

        [Fact]
        public void Encrypt_EmptyPlaintext_ProducesNonceAndTag()
        {
            var encrypted = XChaCha20Poly1305Cipher.Encrypt([], NewKey(), []);

            Assert.Equal(
                XChaCha20Poly1305Cipher.NonceSize + XChaCha20Poly1305Cipher.TagSize,
                encrypted.Length);
        }

        [Fact]
        public void Encrypt_EmptyPlaintext_ThenDecrypt_ReturnsEmptyPlaintext()
        {
            var key = NewKey();

            var encrypted = XChaCha20Poly1305Cipher.Encrypt([], key, []);
            var decrypted = XChaCha20Poly1305Cipher.Decrypt(encrypted, key, []);

            Assert.Empty(decrypted);
        }

        [Fact]
        public void Encrypt_SamePlaintextTwice_UsesFreshNonceEachTime()
        {
            var plaintext = RandomNumberGenerator.GetBytes(128);
            var key = NewKey();
            var associatedData = "Hashkeeper test context"u8.ToArray();

            var first = XChaCha20Poly1305Cipher.Encrypt(plaintext, key, associatedData);
            var second = XChaCha20Poly1305Cipher.Encrypt(plaintext, key, associatedData);

            Assert.NotEqual(first, second);
            Assert.NotEqual(
                first.AsSpan(0, XChaCha20Poly1305Cipher.NonceSize).ToArray(),
                second.AsSpan(0, XChaCha20Poly1305Cipher.NonceSize).ToArray());
        }

        [Fact]
        public void Encrypt_DoesNotModifyPlaintext()
        {
            var plaintext = RandomNumberGenerator.GetBytes(128);
            var original = plaintext.ToArray();

            XChaCha20Poly1305Cipher.Encrypt(plaintext, NewKey(), []);

            Assert.Equal(original, plaintext);
        }

        [Fact]
        public void Decrypt_WithWrongKey_Throws()
        {
            var encrypted = XChaCha20Poly1305Cipher.Encrypt(
                RandomNumberGenerator.GetBytes(128),
                NewKey(),
                []);

            Assert.Throws<CryptographicException>(
                () => XChaCha20Poly1305Cipher.Decrypt(encrypted, NewKey(), []));
        }

        [Fact]
        public void Decrypt_WithWrongAssociatedData_Throws()
        {
            var key = NewKey();
            var encrypted = XChaCha20Poly1305Cipher.Encrypt(
                RandomNumberGenerator.GetBytes(128),
                key,
                "original context"u8);

            Assert.Throws<CryptographicException>(
                () => XChaCha20Poly1305Cipher.Decrypt(
                    encrypted,
                    key,
                    "different context"u8));
        }

        [Theory]
        [InlineData(0)]  // first nonce byte
        [InlineData(23)] // last nonce byte
        [InlineData(24)] // first ciphertext byte
        [InlineData(39)] // last ciphertext byte for a 16-byte plaintext
        [InlineData(40)] // first tag byte for a 16-byte plaintext
        [InlineData(55)] // last tag byte
        public void Decrypt_TamperedData_Throws(int index)
        {
            var key = NewKey();
            var plaintext = RandomNumberGenerator.GetBytes(16);

            var encrypted = XChaCha20Poly1305Cipher.Encrypt(plaintext, key, []);

            encrypted[index] ^= 0x01;

            Assert.Throws<CryptographicException>(
                () => XChaCha20Poly1305Cipher.Decrypt(encrypted, key, []));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(23)]
        [InlineData(39)]
        public void Decrypt_InvalidLength_Throws(int length)
        {
            Assert.Throws<CryptographicException>(
                () => XChaCha20Poly1305Cipher.Decrypt(new byte[length], NewKey(), []));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(33)]
        public void Encrypt_InvalidKeyLength_Throws(int keyLength)
        {
            Assert.Throws<ArgumentException>(
                () => XChaCha20Poly1305Cipher.Encrypt(
                    RandomNumberGenerator.GetBytes(32),
                    new byte[keyLength],
                    []));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(33)]
        public void Decrypt_InvalidKeyLength_Throws(int keyLength)
        {
            var encrypted = XChaCha20Poly1305Cipher.Encrypt(
                RandomNumberGenerator.GetBytes(32),
                NewKey(),
                []);

            Assert.Throws<ArgumentException>(
                () => XChaCha20Poly1305Cipher.Decrypt(
                    encrypted,
                    new byte[keyLength],
                    []));
        }

        [Fact]
        public void Decrypt_ValidCiphertextWithDifferentPlaintextLength_Throws()
        {
            var key = NewKey();
            var encrypted = XChaCha20Poly1305Cipher.Encrypt(
                RandomNumberGenerator.GetBytes(32),
                key,
                []);

            encrypted[24] ^= 0x01;

            Assert.Throws<CryptographicException>(
                () => XChaCha20Poly1305Cipher.Decrypt(encrypted, key, []));
        }
    }
}

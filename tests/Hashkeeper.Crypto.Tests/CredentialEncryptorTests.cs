namespace Hashkeeper.Crypto.Tests
{
    internal class CredentialEncryptorTests
    {
        private const string ValidEntryId = "3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b";
        private const byte FormatVersion = 0x01;

        private static byte[] NewKey() =>
            RandomNumberGenerator.GetBytes(XChaCha20Poly1305Cipher.KeySize);

        [Fact]
        public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
        {
            var plaintext = RandomNumberGenerator.GetBytes(128);
            var key = NewKey();

            var encrypted = CredentialEncryptor.Encrypt(plaintext, key, ValidEntryId);
            var decrypted = CredentialEncryptor.Decrypt(encrypted, key, ValidEntryId);

            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void Encrypt_ProducesVersionedOutputOfExpectedSize()
        {
            var plaintext = RandomNumberGenerator.GetBytes(128);

            var encrypted = CredentialEncryptor.Encrypt(plaintext, NewKey(), ValidEntryId);

            Assert.Equal(FormatVersion, encrypted[0]);
            Assert.Equal(
                1 + XChaCha20Poly1305Cipher.NonceSize + plaintext.Length + XChaCha20Poly1305Cipher.TagSize,
                encrypted.Length);
        }

        [Fact]
        public void Encrypt_EmptyPlaintext_ProducesVersionNonceAndTag()
        {
            var encrypted = CredentialEncryptor.Encrypt([], NewKey(), ValidEntryId);

            Assert.Equal(FormatVersion, encrypted[0]);
            Assert.Equal(
                1 + XChaCha20Poly1305Cipher.NonceSize + XChaCha20Poly1305Cipher.TagSize,
                encrypted.Length);
        }

        [Fact]
        public void Encrypt_EmptyPlaintext_ThenDecrypt_ReturnsEmptyPlaintext()
        {
            var key = NewKey();

            var encrypted = CredentialEncryptor.Encrypt([], key, ValidEntryId);
            var decrypted = CredentialEncryptor.Decrypt(encrypted, key, ValidEntryId);

            Assert.Empty(decrypted);
        }

        [Fact]
        public void Encrypt_SamePlaintextTwice_UsesFreshNonceEachTime()
        {
            var plaintext = RandomNumberGenerator.GetBytes(128);
            var key = NewKey();

            var first = CredentialEncryptor.Encrypt(plaintext, key, ValidEntryId);
            var second = CredentialEncryptor.Encrypt(plaintext, key, ValidEntryId);

            Assert.NotEqual(first, second);
            Assert.NotEqual(
                first.AsSpan(1, XChaCha20Poly1305Cipher.NonceSize).ToArray(),
                second.AsSpan(1, XChaCha20Poly1305Cipher.NonceSize).ToArray());
        }

        [Fact]
        public void Encrypt_DoesNotModifyPlaintext()
        {
            var plaintext = RandomNumberGenerator.GetBytes(128);
            var original = plaintext.ToArray();

            CredentialEncryptor.Encrypt(plaintext, NewKey(), ValidEntryId);

            Assert.Equal(original, plaintext);
        }

        [Fact]
        public void Encrypt_UsesSpecifiedAssociatedData()
        {
            var plaintext = RandomNumberGenerator.GetBytes(64);
            var key = NewKey();
            var expectedAssociatedData = Encoding.ASCII.GetBytes(
                "hashkeeper:entry:v1:" + ValidEntryId);

            var encrypted = CredentialEncryptor.Encrypt(plaintext, key, ValidEntryId);
            var decrypted = XChaCha20Poly1305Cipher.Decrypt(
                encrypted.AsSpan(1),
                key,
                expectedAssociatedData);

            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void Decrypt_WithDifferentEntryId_Throws()
        {
            var key = NewKey();
            var encrypted = CredentialEncryptor.Encrypt(
                RandomNumberGenerator.GetBytes(128),
                key,
                ValidEntryId);
            const string differentEntryId = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";

            Assert.Throws<CryptographicException>(
                () => CredentialEncryptor.Decrypt(encrypted, key, differentEntryId));
        }

        [Fact]
        public void Decrypt_WithWrongKey_Throws()
        {
            var encrypted = CredentialEncryptor.Encrypt(
                RandomNumberGenerator.GetBytes(128),
                NewKey(),
                ValidEntryId);


            Assert.Throws<CryptographicException>(
                () => CredentialEncryptor.Decrypt(encrypted, NewKey(), ValidEntryId));
        }

        [Theory]
        [InlineData(1)]  // first nonce byte (after the version byte)
        [InlineData(24)] // last nonce byte
        [InlineData(25)] // first ciphertext byte
        [InlineData(40)] // last ciphertext byte for a 16-byte plaintext
        [InlineData(41)] // first tag byte for a 16-byte plaintext
        [InlineData(56)] // last tag byte for a 16-byte plaintext
        public void Decrypt_TamperedData_Throws(int index)
        {
            var key = NewKey();
            var plaintext = RandomNumberGenerator.GetBytes(16);
            var encrypted = CredentialEncryptor.Encrypt(plaintext, key, ValidEntryId);

            encrypted[index] ^= 0x01;

            Assert.Throws<CryptographicException>(
                () => CredentialEncryptor.Decrypt(encrypted, key, ValidEntryId));
        }

        [Fact]
        public void Decrypt_UnsupportedFormatVersion_Throws()
        {
            var encrypted = CredentialEncryptor.Encrypt([], NewKey(), ValidEntryId);
            encrypted[0] = 0x02;

            Assert.Throws<CryptographicException>(
                () => CredentialEncryptor.Decrypt(encrypted, NewKey(), ValidEntryId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(24)]
        [InlineData(40)]
        public void Decrypt_InvalidLength_Throws(int length)
        {
            var encrypted = new byte[length];
            if (encrypted.Length > 0)
            {
                // Use a supported version so the cipher's minimum-length check is exercised.
                encrypted[0] = FormatVersion;
            }

            Assert.Throws<CryptographicException>(
                () => CredentialEncryptor.Decrypt(encrypted, NewKey(), ValidEntryId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(33)]
        public void Encrypt_InvalidKeyLength_Throws(int keyLength)
        {
            Assert.Throws<ArgumentException>(
                () => CredentialEncryptor.Encrypt(
                    RandomNumberGenerator.GetBytes(32),
                    new byte[keyLength],
                    ValidEntryId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("123")]
        [InlineData("3f2b8c1e9a4d4e6f8b7a1c2d3e4f5a6b")]
        [InlineData("3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6")]
        [InlineData("3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6g")]
        [InlineData("3F2B8C1E-9A4D-4E6F-8B7A-1C2D3E4F5A6B")]
        [InlineData("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")]
        public void Encrypt_InvalidEntryId_ThrowsArgumentException(string entryId)
        {
            Assert.Throws<ArgumentException>(
                () => CredentialEncryptor.Encrypt([], NewKey(), entryId));
        }

        [Fact]
        public void Encrypt_ValidEntryId_DoesNotThrow()
        {
            var encrypted = CredentialEncryptor.Encrypt(
                RandomNumberGenerator.GetBytes(32),
                NewKey(),
                ValidEntryId);

            Assert.NotEmpty(encrypted);
        }
    }
}
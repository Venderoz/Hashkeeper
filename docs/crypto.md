# Hashkeeper crypto spec

**Status:** v1 (draft until the first release). **Applies to:** `Hashkeeper.Crypto` (C#, NSec) and `src/dashboard/src/crypto` (TypeScript, libsodium-wrappers-sumo / WebCrypto).

This document is the single source of truth for every cryptographic operation in Hashkeeper. Both implementations must follow it byte for byte; compatibility is checked by the shared test vectors in [`tests/crypto-vectors/`](../tests/crypto-vectors/) (HK-17). Any change to a value marked **normative** below requires a new format version (see [Versioning](#versioning)).

The keywords MUST, MUST NOT, SHOULD and MAY are used as in RFC 2119.

---

## 1. Overview

```
 Master Password ──(NFC, UTF-8)──► Argon2id(salt, params) ──► Master Key (32 B)
                                                                 │
                                   ┌─────────────────────────────┼──────────────────────────────┐
                                   ▼                             ▼                              │
                     HKDF-SHA256("hashkeeper:v1:auth-hash")    XChaCha20-Poly1305 unwrap        │
                                   │                             │                              │
                                   ▼                             ▼                              │
                         Auth Hash (32 B) ──► server     Vault Key (32 B, random) ──► encrypts each entry
                         (server re-hashes it)           (stored only wrapped)        (XChaCha20-Poly1305)
```

| Value | Size | Where it exists | Leaves the client? |
|---|---|---|---|
| Master Password | – | typed by the user, memory only | never |
| Master Key | 32 B | memory only, while deriving / unwrapping | never |
| Vault Key | 32 B | memory only, while the vault is unlocked | only wrapped |
| Wrapped Vault Key | 73 B | local vault; server (sync mode) | yes |
| Auth Hash | 32 B | memory only; sent at sign-in / registration | yes (sync mode) |
| Encrypted entry | ≥ 41 B | local vault; server (sync mode) | yes |
| KDF salt + params | – | local vault; server (sync mode) | yes (not secret) |

## 2. Primitives

| Purpose | Algorithm | Reference | C# (NSec) | TypeScript |
|---|---|---|---|---|
| Password hashing | Argon2id, version 0x13 | RFC 9106 | `PasswordBasedKeyDerivationAlgorithm.Argon2id` | `crypto_pwhash` with `crypto_pwhash_ALG_ARGON2ID13` |
| Key derivation | HKDF-SHA256 | RFC 5869 | `KeyDerivationAlgorithm.HkdfSha256` | WebCrypto `HKDF` with `SHA-256` |
| Authenticated encryption | XChaCha20-Poly1305 (IETF) | draft-irtf-cfrg-xchacha | `AeadAlgorithm.XChaCha20Poly1305` | `crypto_aead_xchacha20poly1305_ietf_*` |
| Randomness | OS CSPRNG | – | `RandomNumberGenerator` | `crypto.getRandomValues` / `randombytes_buf` |

No other algorithms are allowed. In particular, never use `Random`, `Math.random`, or a hand-rolled construction.

## 3. Encoding conventions (normative)

- **Bytes on the wire / in JSON:** standard Base64 (RFC 4648 §4, alphabet `A–Z a–z 0–9 + /`) **with** `=` padding. C#: `Convert.ToBase64String`. libsodium: `to_base64(x, base64_variants.ORIGINAL)`.
- **Bytes in the local SQLite vault:** raw `BLOB`, no encoding.
- **Bytes in test vectors:** lowercase hex, for readability.
- **Text to bytes:** UTF-8 without BOM.
- **Label strings** (HKDF `info`, AEAD associated data) are ASCII and used exactly as written here, without a terminating null byte.
- **Multi-byte integers:** there are none in v1 formats. If added later, they MUST be little-endian.

## 4. Master Key derivation (normative)

```
masterKey = Argon2id(password = UTF8(NFC(masterPassword)), salt, t, m, p = 1, length = 32)
```

### 4.1 Password normalisation

The Master Password MUST be normalised to Unicode **NFC** and then encoded as UTF-8. It MUST NOT be trimmed, lower-cased or otherwise altered.

- C#: `Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC))`, then use the `ReadOnlySpan<byte>` overload of `DeriveBytes` (not the `string` overload).
- TS: `new TextEncoder().encode(password.normalize("NFC"))`.

Without normalisation, the same password typed on different systems (e.g. `ą` as one code point vs. `a` + combining ogonek) would produce different keys.

### 4.2 Parameters

| Parameter | v1 default | Constraints |
|---|---|---|
| Iterations `t` (NSec `NumberOfPasses`, libsodium `opslimit`) | **3** | ≥ 3 |
| Memory `m` (NSec `MemorySize` in KiB) | **65 536 KiB** (64 MiB) | ≥ 65 536 KiB |
| libsodium `memlimit` (bytes) | **67 108 864** | = `m` × 1024 |
| Parallelism `p` | **1** | MUST be 1 (libsodium and NSec support only 1) |
| Salt | **16 bytes**, random | exactly 16 (NSec and `crypto_pwhash_SALTBYTES`) |
| Output length | **32 bytes** | – |

Defaults follow the RFC 9106 "second recommended option" (t = 3, 64 MiB), with p = 1 because that is all libsodium supports. Measured: ~65 ms on a desktop CPU with NSec; expect several times longer in the browser (WebAssembly).

- The salt is generated once, when the vault is created, and regenerated on every Master Password change.
- `t`, `m` and the salt are stored next to the wrapped Vault Key (local vault metadata; on the server they are returned by `/auth/prelogin`) so the defaults can be raised later without breaking existing vaults. Implementations MUST derive with the **stored** parameters, not the current defaults.
- Implementations MUST reject stored parameters below the constraints above (protects against a malicious server downgrading the KDF).

### 4.3 Stored KDF descriptor

```json
{
  "kdf": "argon2id",
  "iterations": 3,
  "memoryKiB": 65536,
  "parallelism": 1,
  "salt": "<16 bytes, base64>"
}
```

## 5. Auth Hash derivation (normative)

```
authHash = HKDF-SHA256(IKM = masterKey, salt = "" (empty), info = "hashkeeper:v1:auth-hash", L = 32)
```

- The empty salt is equivalent to 32 zero bytes (RFC 5869 §2.2). C#: `KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(masterKey, ReadOnlySpan<byte>.Empty, info, 32)`. WebCrypto: `deriveBits({ name: "HKDF", hash: "SHA-256", salt: new Uint8Array(0), info }, ikm, 256)`.
- The Auth Hash is sent to the server instead of any password or key (registration, sign-in, re-authentication). Because HKDF is one-way, it reveals nothing about the Master Key.
- The server MUST NOT store it as-is: it re-hashes it with its own Argon2id and random per-user salt (server-side parameters are not part of this spec and may differ from the client's).
- Note: the stored verifier is still derived from the Master Password, so a leaked server database allows offline guessing of weak passwords. See the README security model.

## 6. Vault Key and its wrapping (normative)

The Vault Key is **32 random bytes** from the CSPRNG, generated once when the vault is created. It never changes, even when the Master Password does (only its wrapping changes).

### 6.1 Wrap

```
nonce   = random(24)
ct||tag = XChaCha20-Poly1305-Encrypt(key = masterKey, nonce, ad = "hashkeeper:vault-key:v1", plaintext = vaultKey)
wrapped = 0x01 || nonce || ct || tag
```

| Offset | Length | Field |
|---|---|---|
| 0 | 1 | version = `0x01` |
| 1 | 24 | nonce |
| 25 | 32 | ciphertext (encrypted Vault Key) |
| 57 | 16 | Poly1305 tag |
| | **73** | total |

### 6.2 Unwrap

1. Reject if the length is not exactly 73 bytes.
2. Reject if `wrapped[0]` is not `0x01`.
3. Decrypt with the same associated data. Reject if authentication fails.

Every rejection MUST surface as a single generic error ("wrong Master Password or corrupted vault") — the UI MUST NOT tell the user which check failed.

Implemented by `VaultKeyWrapper` in `Hashkeeper.Crypto` (HK-27).

## 7. Entry encryption (normative)

Each vault entry is encrypted **separately** with the Vault Key.

### 7.1 Plaintext

The plaintext is the UTF-8 encoding of a JSON object with the entry's secret and descriptive fields (name, login, password, URL, notes, …). The exact field set belongs to the vault model (HK-39), not to this spec: the crypto layer treats the plaintext as opaque bytes. Unknown fields MUST be preserved when an entry is re-encrypted, so older clients don't drop data written by newer ones.

The entry **ID**, version number, timestamps and the deleted flag are **not** encrypted — they are sync metadata the server needs. Everything else MUST be inside the plaintext.

### 7.2 Encrypt

```
ad        = "hashkeeper:entry:v1:" || entryId
nonce     = random(24)
ct||tag   = XChaCha20-Poly1305-Encrypt(key = vaultKey, nonce, ad, plaintext)
encrypted = 0x01 || nonce || ct || tag
```

- `entryId` is the entry's UUID in canonical lowercase form with hyphens, 36 ASCII characters (e.g. `3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b`). C#: `id.ToString("D")`. TS: `id.toLowerCase()`.
- Binding the ID into the associated data stops a malicious server (or a corrupted database) from swapping ciphertexts between entries, e.g. showing the bank password under "Netflix".

| Offset | Length | Field |
|---|---|---|
| 0 | 1 | version = `0x01` |
| 1 | 24 | nonce |
| 25 | n | ciphertext (n = plaintext length) |
| 25 + n | 16 | Poly1305 tag |
| | **41 + n** | total |

### 7.3 Decrypt

1. Reject if the length is below 41 bytes.
2. Reject if `encrypted[0]` is not `0x01`.
3. Decrypt with `ad` built from the entry's own ID. Reject if authentication fails.

A failure MUST be reported as a corrupted entry; it MUST NOT abort decryption of the other entries.

## 8. Nonces (normative)

- Every encryption — every entry write and every Vault Key wrap — MUST use a **fresh 24-byte random nonce** from the CSPRNG. Re-encrypting an unchanged entry also gets a new nonce.
- Nonces MUST NOT be derived from counters, timestamps or entry IDs.
- With 192-bit nonces, the probability of a random collision is negligible for any realistic number of encryptions under one key, so no nonce tracking is needed.

## 9. Domain separation summary (normative)

| Label | Used as | Section |
|---|---|---|
| `hashkeeper:v1:auth-hash` | HKDF `info` | §5 |
| `hashkeeper:vault-key:v1` | AEAD associated data | §6 |
| `hashkeeper:entry:v1:<entryId>` | AEAD associated data | §7 |

Each label is used for exactly one purpose. New purposes MUST get a new label.

## 10. Operations

### Create vault (first run)
1. Generate `salt` (16 B) and use the default KDF parameters.
2. Derive `masterKey` (§4); generate `vaultKey` (§6).
3. Store the KDF descriptor and `wrap(vaultKey, masterKey)` as vault metadata.
4. Sync mode only: also derive `authHash` (§5) and register with email, `authHash`, KDF descriptor and wrapped Vault Key.

### Unlock
1. Read the KDF descriptor (locally, or from `/auth/prelogin` in the Dashboard); validate it (§4.2).
2. Derive `masterKey`, unwrap the Vault Key. On failure, show the generic error.
3. Sync mode / Dashboard: derive `authHash` and sign in.
4. Clear `masterKey` from memory; keep only `vaultKey` while unlocked.

### Change Master Password
1. Unlock with the current password (proves knowledge of it).
2. Generate a **new** salt, derive a new `masterKey` with the current default parameters.
3. Re-wrap the **same** `vaultKey`; entries are not re-encrypted.
4. Sync mode: derive the new `authHash` and update it on the server together with the new KDF descriptor and wrapped Vault Key, in one request.

### Lock
Clear `vaultKey` and all decrypted entries from memory.

## 11. Memory hygiene

- Keys SHOULD be held in byte arrays (not strings) and cleared as soon as they are no longer needed: C# `CryptographicOperations.ZeroMemory`, TS `sodium.memzero` / `array.fill(0)`.
- The Master Password string cannot be reliably wiped in .NET or JavaScript; keep it in scope for as short a time as possible and never log it.
- Plaintext entries, keys and the Auth Hash MUST NOT be written to logs, crash reports, telemetry or `localStorage`.

## 12. Versioning

- The first byte of every ciphertext format is its version. v1 is `0x01`.
- A new version is introduced only when the format or a normative value changes. Readers MUST reject versions they don't know rather than guess.
- When v2 exists, clients SHOULD keep reading v1 and SHOULD write only v2 (re-encrypting on the next write).
- KDF parameters are versioned separately through the stored descriptor (§4.3).

## 13. Test vectors

`tests/crypto-vectors/*.json` (HK-17) MUST contain, for each section above, fixed inputs (including fixed salts and nonces) and the expected outputs in hex. Both implementations run the same files:

```json
{
  "masterKey":   [{ "password": "…", "salt": "…", "iterations": 3, "memoryKiB": 65536, "masterKey": "…" }],
  "authHash":    [{ "masterKey": "…", "authHash": "…" }],
  "vaultKeyWrap":[{ "masterKey": "…", "vaultKey": "…", "nonce": "…", "wrapped": "…" }],
  "entry":       [{ "vaultKey": "…", "entryId": "…", "plaintext": "…", "nonce": "…", "encrypted": "…" }]
}
```

Vectors MUST include a non-ASCII password (to check NFC + UTF-8) and an empty-string plaintext. Production code never accepts a caller-supplied nonce; tests reach a fixed-nonce code path through an internal API only.

## 14. Out of scope / known limitations (v1)

- **Rollback:** the server can serve an older, validly encrypted version of an entry. Detecting this would need signed vault state; not planned for v1.
- **Length leakage:** ciphertext length reveals plaintext length (roughly the length of the stored password and notes). Padding may be added in a later version.
- **Metadata:** entry count, IDs and modification times are visible to the server.
- **Recovery key, 2FA, sharing:** not covered; will extend this spec when designed.
- **Malicious Dashboard server:** cannot be mitigated by cryptography in the page itself (see README).

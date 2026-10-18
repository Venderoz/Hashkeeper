# Hashkeeper

A **local-first, zero-knowledge** password manager for Windows. Passwords are encrypted and decrypted **client-side only (Tray app or browser), never on the server**. Cloud sync and the web dashboard are **optional and disabled by default**. When enabled, all vault data on the server is encrypted, and the server never sees your passwords or your Master Password. Account and session data (e.g. email, device list, refresh tokens, a hashed authentication value) are stored as regular server-side records.

The project consists of three parts:

| Component | Role | Required? | Stack |
|---|---|---|---|
| **Windows App (Tray)** | Day-to-day use: quick search, copy, add / edit passwords, local audit, local encrypted vault | Yes | .NET 10, WPF, SQLite |
| **Backend API** | Accounts, sessions, storage and sync of encrypted entries | Only with sync enabled | ASP.NET Core (.NET 10), PostgreSQL |
| **Web Dashboard** | Access from anywhere, vault management, full security audit incl. breach check, devices | Only with sync enabled | Vite, React, TypeScript |

See [Tech stack](#tech-stack) for details.

---

## Operating modes

### Local mode (default)

- The vault is stored only on your computer as an encrypted file.
- No account, no network traffic, nothing leaves the device – the Tray app makes **no calls to any external API**.
- The vault is fully manageable from the Tray app (including the Vault Manager and a local audit).
- The Web Dashboard, breach checks and remote features (remote logout, device management) are unavailable.

### Sync mode (opt-in)

- Enabled explicitly by the user in the Tray settings (**Settings → Cloud sync → Enable**).
- Requires creating an account (or signing in to an existing one) on the Backend API.
- The encrypted vault is synchronized with the Backend and becomes accessible from the Web Dashboard.
- Sync can be **disabled at any time**. On disabling, the user chooses whether to:
  - keep the encrypted copy on the server (sync paused), or
  - **delete all data from the server** (the vault stays local only).

> Nothing is ever uploaded unless sync has been explicitly turned on.

### Feature availability

| Feature | Local mode | Sync mode |
|---|:---:|:---:|
| Search bar, copy to clipboard, Quick Add | ✅ | ✅ |
| Password generator | ✅ | ✅ |
| Vault Manager (view, edit, delete entries) | ✅ | ✅ |
| Local audit: weak & reused passwords (Tray) | ✅ | ✅ |
| Auto-lock | ✅ | ✅ |
| Encrypted export / import | ✅ | ✅ |
| Access from other devices | ❌ | ✅ |
| Web Dashboard | ❌ | ✅ |
| Breach check | ❌ | ✅ |
| Device management, remote logout | ❌ | ✅ |
| Server-side encrypted backup | ❌ | ✅ |

External APIs (such as Have I Been Pwned) are used **only by the Web Dashboard**. The Tray app talks only to the Hashkeeper Backend, and only when sync is enabled.

---

## How it works

```
 ┌──────────── Client: Tray app or browser ────────────┐          ┌──────── Server (optional) ────────┐
 │                                                     │          │                                   │
 │  Master Password ──Argon2id──► Master Key           │          │                                   │
 │                                   │                 │          │                                   │
 │                     decrypts ◄────┘                 │          │                                   │
 │                         │                           │  sync    │                                   │
 │                     Vault Key ──► encrypts entries ─┼─ only if ┼─►  encrypted entries              │
 │                                        │            │ enabled  │   (unreadable blobs)              │
 │                                        ▼            │          │                                   │
 │                            local encrypted vault    │          │                                   │
 └─────────────────────────────────────────────────────┘          └───────────────────────────────────┘
```

Example scenario (sync enabled):

1. In the Tray app you add a new password for Netflix.
2. The Tray app encrypts the entry locally and saves it to the local vault.
3. Since sync is enabled, the encrypted entry is uploaded to the Backend.
4. At university, you sign in to the Web Dashboard.
5. The browser downloads the encrypted data and decrypts it client-side (in JavaScript, in the browser tab) after you enter your Master Password.

With sync disabled, the flow ends at step 2.

---

## Components

### 1. Windows App (Tray)

Runs in the background. The UI appears only for a moment when needed, then disappears.

**UI:**
- **Search bar** – a global hotkey opens a slim text field in the middle of the screen. Type `net`, pick "Netflix", the password goes to the clipboard and the bar disappears.
- **Quick Add** – a small window with Name, Login, Password (+ password generator) fields and a "Save" button.
- **Vault Manager** – a regular window opened from the context menu: list of all entries with search, editing and deletion, plus a **local audit** highlighting weak and reused passwords. Works fully offline.
- **Context menu** (right-click the tray icon): Open Vault Manager, Lock vault, Sync now *(only when sync is enabled)*, Settings, Exit.
- **Unlock screen** – a simple window to enter the Master Password when the vault is locked.
- **Settings** – auto-lock timeout, hotkey, clipboard timeout, **cloud sync on/off**, API address.

**Under the hood:**
- encrypts and decrypts entries locally and stores them in an encrypted local vault file,
- clears the clipboard after 15 seconds (only if it still holds the copied password) and excludes passwords from Windows clipboard history (`Win+V`) and cloud clipboard,
- locks the vault automatically: after inactivity, on Windows lock, on sleep, or on request from the Dashboard (sync mode only),
- when sync is enabled, synchronizes encrypted entries with the Backend in the background.

### 2. Backend API (optional)

A headless server application running 24/7. Used only by users who enabled sync.

**Does:**
- registration and sign-in (optionally with 2FA / TOTP),
- session and device management (tokens, revocation),
- storage and synchronization of encrypted entries,
- notifies clients about changes (e.g. "session revoked", "vault changed"),
- full deletion of a user's data on request.

**Does not:**
- know the user's passwords or Master Password,
- perform any encryption or decryption of vault data.

### 3. Web Dashboard (optional)

A website for "spring cleaning" of your passwords and security checks. Available only for accounts with sync enabled.

- **Full vault view** – a table of all entries with search, editing and bulk deletion.
- **Security audit** – weak and reused passwords, passwords found in breaches (Have I Been Pwned), charts. All analysis runs in the browser on decrypted data. The breach check is available only here – it is the only place where Hashkeeper calls an external API.
- **Device management** – list of active sessions and remote logout / lock of a selected device.
- **Account settings** – change Master Password, 2FA, export vault, disable sync and delete server data, delete account.

---

## Security model

### Keys

1. **Master Key** = `Argon2id(Master Password, salt)` – derived only on the client, never leaves it.
2. **Vault Key** – a random key generated when the vault is created. Stored (locally and, with sync, on the server) only in a form encrypted with the Master Key.
3. **Entries** – each entry is encrypted separately with the Vault Key (XChaCha20-Poly1305).

Every encryption – of each entry and of the wrapped Vault Key – uses a **fresh random 24-byte nonce**, which is stored and transmitted together with its ciphertext. A nonce is never reused with the same key, since that would break both confidentiality and authentication. The exact binary format will be defined in a crypto spec shared by the C# and TypeScript implementations.

Thanks to the separate Vault Key, changing the Master Password only requires re-encrypting a single key, not the whole vault.

### Where encryption happens

All encryption and decryption is **client-side** – in the Tray app or in the browser (JavaScript + libsodium in the Dashboard tab). Apart from account metadata, the server only ever receives the authentication hash and encrypted blobs; the Master Password, keys and plaintext passwords never reach it.

### Sign-in without revealing the password (sync mode)

Neither the Master Password nor the Master Key is ever sent to the server. The client derives a separate **authentication hash** from the Master Key (e.g. via HKDF), which the server hashes again and compares.

This prevents **direct** disclosure of keys: a leaked server database contains no Master Key or plaintext Vault Key. It does **not** rule out offline attacks – the stored verifier is derived from the Master Password, so an attacker can try to guess the password offline. A successful guess yields the Master Key, which unwraps the Vault Key and decrypts the vault. Argon2id (on the client) and re-hashing (on the server) make each guess expensive, but a weak Master Password remains the main risk – hence a strong Master Password is essential.

### Important consequences

- **No password recovery.** Losing the Master Password means losing access to the vault. A recovery key generated at vault creation may be considered.
- **Local mode has no backup by default.** If sync is disabled, the user is responsible for backing up the local vault file (the app can offer an encrypted export).
- **Remote logout is best effort.** The server revokes the session immediately, but an offline device will lock only after it reconnects (or after its local timeout).
- **Trust in the Dashboard.** The page code is served by the server, and the Dashboard handles the Master Password. A compromised or malicious server can replace the HTML, JavaScript, CSP and SRI metadata it serves – and thereby capture the Master Password. **This cannot be mitigated by the page itself**: using the Dashboard means trusting the server that serves it. A strict CSP, Subresource Integrity and no third-party scripts only reduce unrelated risks (script injection, compromised third-party code). Users who don't want this trade-off simply keep sync disabled and use only the Tray app.
- **Have I Been Pwned** (Dashboard only) – the k-anonymity API is used: only the first 5 characters of the SHA-1 hash are sent, never the password or the full hash.

---

## Synchronization (sync mode only)

- Each entry has an ID, a version number and a modification timestamp.
- The client uploads only changed entries and downloads changes since the last sync.
- Conflicts (same entry changed on two devices) – in the MVP the newer change wins; the previous version is kept in the entry history.
- Deletions are marked with tombstones so they propagate to other devices.
- Real-time notifications (e.g. SignalR / WebSocket) – instant lock and sync after changes from another device.
- When sync is first enabled, the existing local vault is uploaded; when it is disabled, the local vault remains fully functional.

---

## Tech stack

### Cryptography

The two sides use different libraries, both built on **libsodium**: NSec (a .NET wrapper over native libsodium) in the Tray app and libsodium-wrappers-sumo (libsodium compiled to WebAssembly) in the browser. Compatibility comes from using the same algorithms, parameters and ciphertext format (defined in the crypto spec), not from sharing code.

| Purpose | Algorithm | .NET | Web |
|---|---|---|---|
| Key derivation from Master Password | Argon2id | [NSec](https://nsec.rocks/) | [libsodium-wrappers-sumo](https://www.npmjs.com/package/libsodium-wrappers-sumo) |
| Entry encryption | XChaCha20-Poly1305 | NSec | libsodium-wrappers-sumo |
| Auth hash derivation | HKDF-SHA256 | NSec | libsodium-wrappers-sumo |

Cross-language compatibility is verified by shared test vectors: data encrypted in C# must decrypt in TypeScript and vice versa.

### Windows App (Tray)

| Concern | Choice |
|---|---|
| Runtime / UI | .NET 10, WPF |
| Tray icon & context menu | [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) |
| Global hotkey | Win32 `RegisterHotKey` (P/Invoke) |
| Local vault storage | SQLite (`Microsoft.Data.Sqlite`), one encrypted blob per entry |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) |
| Real-time notifications | `Microsoft.AspNetCore.SignalR.Client` (sync mode) |

### Backend API

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core (.NET 10), Minimal API |
| Database / ORM | PostgreSQL, EF Core (Npgsql) |
| Authentication | Custom sign-in with client-derived auth hash (server re-hashes with Argon2id), JWT access tokens + refresh tokens stored in DB (revocation = remote logout) |
| Real-time notifications | SignalR |
| API docs | OpenAPI + [Scalar](https://github.com/scalar/scalar) |

ASP.NET Core Identity is intentionally not used – clients never send the actual password, only the derived auth hash.

### Web Dashboard

| Concern | Choice |
|---|---|
| Build tool / framework | Vite, React, TypeScript |
| API types | Generated from the Backend's OpenAPI spec ([openapi-typescript](https://openapi-ts.dev/)) |
| Data fetching | TanStack Query |
| Routing | React Router |
| UI | Tailwind CSS, shadcn/ui |
| Charts | Recharts |
| Real-time notifications | `@microsoft/signalr` |

### Testing & tooling

| Concern | Choice |
|---|---|
| .NET tests | xUnit |
| Web tests | Vitest |
| API tests | Postman collections run with Postman CLI |
| Local environment | Docker Compose (PostgreSQL + API) |
| CI | GitHub Actions (build + tests on every push) |

### Hosting (demo, optional)

| Part | Choice |
|---|---|
| Backend API + database | Azure for Students (App Service + Azure Database for PostgreSQL) |
| Web Dashboard | Azure Static Web Apps or Vercel (free tier) |

---

## Architecture

### Project dependencies

```
 Hashkeeper.Tray ──────► Hashkeeper.Crypto
        │
        └──────────────► Hashkeeper.Contracts ◄────── Hashkeeper.Api ──► PostgreSQL
                                                            │
 dashboard ── HTTP + SignalR ───────────────────────────────┤
        ▲                                                   │
        └──────── TS types generated from OpenAPI ──────────┘
```

- **Hashkeeper.Crypto** is used only by the Tray app. The API never encrypts or decrypts vault data, so it does not reference it.
- **Hashkeeper.Contracts** holds the request/response DTOs shared by the Tray app and the API.
- **dashboard** cannot reference .NET libraries: it gets API types generated from the OpenAPI spec and has its own TypeScript crypto module, kept compatible with `Hashkeeper.Crypto` by shared test vectors.

### Windows App (Tray) – MVVM

- **Views** – XAML windows (search bar, Quick Add, Vault Manager, unlock, settings). Layout only, no logic.
- **ViewModels** – window state and commands, bound to the views (CommunityToolkit.Mvvm).
- **Services** – application logic used by the ViewModels: vault access, local storage, clipboard, auto-lock, hotkey, audit, sync.

### Web Dashboard

Code is organized by feature (auth, vault, audit, devices, settings) rather than by file type.

---

## Repository structure (planned)

```
Hashkeeper/
├── src/
│   ├── Hashkeeper.Crypto/      # Crypto logic for the Tray app (.NET, NSec)
│   ├── Hashkeeper.Contracts/   # DTOs shared by Tray and API
│   ├── Hashkeeper.Tray/        # Windows app (WPF)
│   ├── Hashkeeper.Api/         # Backend API (ASP.NET Core)
│   └── dashboard/              # Web Dashboard (Vite + React + TS)
├── tests/
│   ├── Hashkeeper.Crypto.Tests/
│   ├── Hashkeeper.Tray.Tests/  # ViewModels and services
│   ├── Hashkeeper.Api.Tests/
│   ├── crypto-vectors/         # Shared C# ↔ TS test vectors
│   └── postman/                # Postman collections for API tests
├── .github/workflows/          # CI
└── docker-compose.yml
```

The detailed layout of each project will be documented once the code exists.

---

## Roadmap

**MVP – local mode**
- [ ] Shared crypto module (C#) with tests
- [ ] Tray: create / unlock local vault, search bar, Quick Add, clipboard clearing
- [ ] Vault Manager: view, edit, delete entries
- [ ] Local audit: weak & reused passwords
- [ ] Auto-lock (inactivity, Windows lock, sleep)
- [ ] Password generator
- [ ] Encrypted export / import

**Sync mode**
- [ ] Backend: registration, sign-in, storage of encrypted entries
- [ ] TypeScript crypto module with C# ↔ TS compatibility tests
- [ ] Opt-in sync in Tray (enable, pause, disable + delete server data)
- [ ] Dashboard: sign-in, vault view and editing

**Later**
- [ ] Session management and remote logout
- [ ] Dashboard security audit + Have I Been Pwned breach check
- [ ] 2FA (TOTP)
- [ ] Import from other managers (CSV, Bitwarden, KeePass)
- [ ] Recovery key

---

## Getting started

_To be added once code exists._

## License

_To be decided._

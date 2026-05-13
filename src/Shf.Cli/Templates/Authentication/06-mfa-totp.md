---
slug: mfa-totp
title: "feat: MFA orchestrator + TOTP channel"
labels: enhancement,version:minor
depends_on: jwt
---

## What

First MFA channel + the orchestrator other channels plug into. Mirrors `sh-framework-template` **v3.12.0**.

### Entities
- `MfaFactor` — `Kind` (Totp/Email/Sms), encrypted `SecretCipher?`, `Destination?`, `VerifiedAt?`, `Status`.
- `MfaChallenge` — `MfaFactorId`, `CodeHash?` (used by Email/SMS; null for TOTP), `ExpiresAt`, `FailedAttempts`, `Status`.

### Contracts
- `IMfaChannel` per Kind — `IssueAsync` + `VerifyAsync`.
- `IMfaOrchestrator` — resolves channel by Kind via DI-injected `IEnumerable<IMfaChannel>`. Failed-attempt counter; auto-flips challenge to `Failed` at `MfaOptions.MaxFailedAttempts` (default 5).

### TOTP
`Otp.NET` 1.4.0. SHA-1, 6 digits, 30s step, ±1 step tolerance. `BuildOtpAuthUri` emits the standard `otpauth://totp` URI consumable by Authy / Google Authenticator / 1Password. Secret encrypted via `ICredentialProtector` (existing ASP.NET DataProtection).

### Login integration
LoginResponse becomes polymorphic: when User has any Active MfaFactor, returns `{ mfaRequired: true, challengeId, kind, expiresAt }`. Client follows up with `POST /api/v1/auth/mfa/verify` to mint Session + tokens.

### Endpoints
- `POST /api/v1/auth/mfa/verify` (pre-auth) — completes half-auth flow.
- `POST /api/v1/auth/mfa/totp/enroll` — generates secret + otpauthUri, factor in PendingEnrollment.
- `POST /api/v1/auth/mfa/totp/confirm` — verifies, flips Active.
- `DELETE /api/v1/auth/mfa/totp/{factorId}` — soft delete.

### Options
`Authentication:Mfa.{MaxFailedAttempts,ChallengeLifetime,TotpIssuer}`.

## Reference

Port from [`sh-framework-template` v3.12.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.12.0):
- `Business/Authentication/Mfa/{IMfaChannel,IMfaOrchestrator,IMfaFactorStore,IMfaChallengeStore,InMemory*,MfaOptions,MfaOrchestrator,MfaResultCode,MfaCodeHasher}.cs`
- `Business/Authentication/Mfa/Totp/*.cs`
- `Business/Features/Auth/{MfaTotp,MfaVerify}/*.cs`
- `WebApi/Endpoints/Auth/MfaEndpoints.cs`
- `Business/Features/Auth/Login/LoginHandler.cs` (Active-MFA branch)

## Acceptance
- [ ] Otp.NET round-trip via `Totp.ComputeTotp` → `Verify` ±1 step tolerance.
- [ ] Failed-attempt counter → status `Failed` at MaxFailedAttempts; subsequent attempts return `ChallengeFailed`.
- [ ] Expired challenge returns `ChallengeExpired` even on correct code.
- [ ] Login response gracefully degrades: no factors → full token pair; Active factor → mfaRequired form.

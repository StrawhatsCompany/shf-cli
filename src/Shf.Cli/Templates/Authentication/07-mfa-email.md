---
slug: mfa-email
title: "feat: MFA Email channel"
labels: enhancement,version:minor
depends_on: mfa-totp
---

## What

Email MFA channel. Reuses the existing `IMailProvider` for dispatch; codes stored as SHA-256 hashes on `MfaChallenge.CodeHash`. Mirrors `sh-framework-template` **v3.13.0**.

### Channel
`EmailMfaChannel` — `IssueAsync` generates N-digit numeric code (default 6), SHA-256 onto challenge, sets `ExpiresAt` to `EmailMfaOptions.Ttl` (10m default), renders template (`{{code}}` + `{{ttlMin}}` placeholders), dispatches via `IMailProvider`. `VerifyAsync` constant-time via `CryptographicOperations.FixedTimeEquals`.

### Enrollment
- `POST /api/v1/auth/mfa/email/enroll` — requires `User.EmailVerifiedAt`. Factor activates immediately (no confirm step needed; the email was already verified).
- `DELETE /api/v1/auth/mfa/email/{factorId}` — soft delete.

### Options (`Authentication:Mfa:Email`)
- `Subject` ("Your verification code")
- `BodyTemplate` ("Your code is {{code}}. It expires in {{ttlMin}} minutes.")
- `Ttl` (10m)
- `CodeLength` (6)

## Reference

Port from [`sh-framework-template` v3.13.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.13.0):
- `Business/Authentication/Mfa/Email/{EmailMfaChannel,EmailMfaOptions}.cs`
- `Business/Authentication/Mfa/MfaCodeHasher.cs` (already in {{slug:mfa-totp}})
- `Business/Features/Auth/MfaEmail/*.cs`
- `WebApi/Endpoints/Auth/MfaEndpoints.cs` (Enroll + Disable email endpoints)

## Acceptance
- [ ] Verify accepts the dispatched code via captured `SendAsync` body.
- [ ] Wrong code returns InvalidCode; expired challenge returns ChallengeExpired.
- [ ] Constant-time hash compare.

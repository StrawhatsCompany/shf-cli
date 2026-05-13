---
slug: mfa-sms
title: "feat: MFA SMS channel + new Providers.Sms project"
labels: enhancement,version:minor
depends_on: mfa-totp
---

## What

SMS MFA channel. Adds a new `Providers.Sms` project (parallel to `Providers.Mail`) with a Twilio first driver, then the channel wires SMS into the orchestrator. Mirrors `sh-framework-template` **v3.14.0**.

### New provider scaffold
- `Business/Providers/Sms/{ISmsProvider,SmsProviderCredential,SmsProviderType,SmsOptions,Contracts/SendSmsContract}.cs`
- `Providers.Sms/{TwilioSmsProvider,ProviderFactory,RegisterSmsProvider,SmsProviderResultCode}.cs` — Twilio 7.5.1, `MessageResource.CreateAsync`.

You can generate the scaffold with the existing CLI command first:
```
shf make:provider Sms --first-driver Twilio
```

Then hand-edit the Twilio adapter to call `MessageResource.CreateAsync` (the generator only seeds the factory switch, not the API call).

### Channel
`SmsMfaChannel` — numeric code, SHA-256 onto challenge, dispatch via `ISmsProvider`. **Per-user rate limit** — default 5 SMS/hour, sliding window via `ConcurrentDictionary` keyed on UserId (swap for Redis in production).

### Enrollment
- `POST /api/v1/auth/mfa/sms/enroll` (`{ phone }`) → returns `{ factorId, challengeId }`. Factor in PendingEnrollment.
- `POST /api/v1/auth/mfa/sms/confirm` (`{ factorId, challengeId, code }`) — flips Active. **If the phone matches `User.Phone` AND `PhoneVerifiedAt` is null, also stamps `PhoneVerifiedAt`** — SMS MFA enrollment doubles as phone verification.
- `DELETE /api/v1/auth/mfa/sms/{factorId}` — soft delete.

### Secrets
`Sms:AccountSid` + `Sms:AuthToken` (Twilio creds) — user-secrets / env, never appsettings. `Sms:FromNumber` in appsettings.

### Options (`Authentication:Mfa:Sms`)
- `BodyTemplate` ("Your code is {{code}}" — keep under 160 chars to fit one SMS segment).
- `Ttl` (5m), `CodeLength` (6).
- `RateLimitMaxIssuesPerUser` (5), `RateLimitWindow` (1h).

### Codes
`MfaResultCode.RateLimited` (4309), `DispatchFailed` (4310). `SmsProviderResultCode` 5100-5102.

## Reference

Port from [`sh-framework-template` v3.14.0](https://github.com/StrawhatsCompany/sh-framework-template/tree/v3.14.0):
- `Business/Providers/Sms/**` + `Providers.Sms/**`
- `Business/Authentication/Mfa/Sms/*.cs`
- `Business/Features/Auth/MfaSms/*.cs`
- `WebApi/Endpoints/Auth/MfaEndpoints.cs` (SMS endpoints) + Program.cs `.AddSmsProvider()`

## Acceptance
- [ ] Rate limiter blocks issuance after max-per-window per user.
- [ ] Provider failure returns DispatchFailed.
- [ ] `PhoneVerifiedAt` auto-stamped when enrolled number matches user's primary phone.
- [ ] docs/SECRETS.md lists Sms:AccountSid + Sms:AuthToken.

# Research: Notification Channel Providers — SMS, Email & Push

**Date**: 2026-02-21  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Tasks**: [tasks.md](tasks.md)  
**Scope**: Technology decisions for delivering notifications via SMS, email, and push channels

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Current State Analysis](#2-current-state-analysis)
3. [SMS Provider Options](#3-sms-provider-options)
4. [Email Provider Options](#4-email-provider-options)
5. [Push Notification Options](#5-push-notification-options)
6. [Dev/Test Tooling](#6-devtest-tooling)
7. [Architecture Decision: Provider Abstraction Layer](#7-architecture-decision-provider-abstraction-layer)
8. [Recipient Contact Resolution](#8-recipient-contact-resolution)
9. [OTP Delivery Architecture](#9-otp-delivery-architecture)
10. [Configuration Strategy](#10-configuration-strategy)
11. [Decisions Summary](#11-decisions-summary)

---

## 1. Problem Statement

The Notifications service has a fully built dispatch pipeline (retry with exponential backoff, channel fallback push → email → in_app, template engine with 14 templates, 7 MassTransit consumers). However, **actual channel delivery is entirely stubbed** — the `SendViaChannelAsync` method always returns `true` without contacting any external provider.

Similarly, the Identity service generates OTPs correctly (cryptographically secure, hashed, rate-limited) but **returns the plaintext OTP in the HTTP response** instead of sending it via SMS or email.

### Requirements Affected

| Requirement | Description | Status |
|-------------|-------------|--------|
| FR-100 | Send via push, in-app, email channels | ❌ Stubbed — no real delivery |
| FR-101 | SMS for critical events (overdue, disbursement) | ❌ No SMS provider |
| FR-103 | Template-based notifications | ✅ Implemented |
| FR-104 | Retry 3x with exponential backoff, then fallback | ✅ Logic implemented, but always "succeeds" |
| FR-105 | Failed notifications remain in in-app centre | ✅ Implemented |
| NFR-024 | Delivery within 60 seconds | ❌ Cannot verify without real providers |
| T031 | OTP: publish notification event | ❌ Not implemented — OTP returned in response |

---

## 2. Current State Analysis

### What's Built (Working)

| Component | Location | Notes |
|-----------|----------|-------|
| Notification entity with status tracking | `Domain/Entities/Notification.cs` | Channel, RetryCount, FailureReason, exponential backoff via `NextRetryAt` |
| Notification preferences per user per channel | `Domain/Entities/NotificationPreference.cs` | Toggle push/email/sms/in_app |
| Device token registration (APNs/FCM) | `Domain/Entities/DeviceToken.cs` | Stores `PushToken`, `Platform`, `DeviceId` |
| Template engine with 14 templates | `Infrastructure/Templates/NotificationTemplateEngine.cs` | `{{placeholder}}` substitution |
| Dispatch service with retry + fallback | `Domain/Services/NotificationDispatchService.cs` | Full logic: preferences → channel select → send → retry → fallback |
| 7 MassTransit event consumers | `Infrastructure/Consumers/NotificationConsumers.cs` | Contribution, loan, repayment, voting, dissolution events |
| REST API for feed, preferences, devices | `Api/Controllers/NotificationsController.cs` | GET feed, unread-count, PUT preferences, POST/DELETE devices |

### What's Missing (Gaps)

| Gap | Impact | Priority |
|-----|--------|----------|
| `SendViaChannelAsync` always returns `true` | No real delivery for any channel | **Critical** |
| No `ISmsSender` / `IEmailSender` / `IPushNotificationSender` interfaces | No provider abstraction | **Critical** |
| No recipient contact resolution (email/phone lookup) | Dispatch service only has `recipientId`, no way to get phone/email | **Critical** |
| No `OtpRequested` integration event | OTP not sent via SMS/email | **Critical** |
| OTP plaintext leaked in API response | Security issue in production | **High** |
| No MailHog/Mailpit in Docker Compose | No dev email testing | **Medium** |
| Fund-wide broadcast uses `Guid.Empty` | No per-member notification for fund events | **Medium** |
| No SMS/email NuGet packages | No provider SDKs installed | **Low** (build abstraction first) |

---

## 3. SMS Provider Options

### Option A: Twilio (Recommended for Production)

| Aspect | Detail |
|--------|--------|
| **SDK** | `Twilio` NuGet package |
| **Channels** | SMS, WhatsApp, Voice |
| **Pricing** | Pay-per-message (~$0.0079/SMS US, ~₹0.15/SMS India via local routes) |
| **.NET Support** | First-class: `MessageResource.CreateAsync(to, from, body)` |
| **Reliability** | 99.95% SLA, delivery receipts, webhook callbacks |
| **Setup** | Account SID + Auth Token + Phone Number (or Messaging Service SID) |
| **Dev/Test** | Test credentials available (no real SMS sent) |

```csharp
// Example: Twilio SMS
var client = new TwilioRestClient(accountSid, authToken);
var message = await MessageResource.CreateAsync(
    to: new PhoneNumber("+919876543210"),
    from: new PhoneNumber("+15551234567"),
    body: "Your OTP is 123456. Valid for 5 minutes.",
    client: client);
```

### Option B: Azure Communication Services (ACS)

| Aspect | Detail |
|--------|--------|
| **SDK** | `Azure.Communication.Sms` NuGet package |
| **Channels** | SMS (limited country support), Email (via ACS Email), Voice |
| **Pricing** | Pay-per-message (~$0.0075/SMS US); India support may require toll-free numbers |
| **.NET Support** | First-class: `SmsClient.SendAsync(from, to, message)` |
| **Reliability** | Azure SLA (99.9%), delivery reports |
| **Setup** | ACS resource + connection string + phone number |
| **Dev/Test** | No built-in test mode; requires real ACS resource |

### Option C: AWS SNS

| Aspect | Detail |
|--------|--------|
| **SDK** | `AWSSDK.SimpleNotificationService` NuGet package |
| **Channels** | SMS (transactional + promotional), Mobile Push (APNs/GCM) |
| **Pricing** | ~$0.00645/SMS (India) |
| **.NET Support** | `PublishAsync(new PublishRequest { PhoneNumber = "+91...", Message = "..." })` |
| **Reliability** | AWS SLA, delivery status logging |
| **Setup** | AWS credentials + region + topic/direct publish |
| **Dev/Test** | Sandbox mode limits to verified numbers only |

### Decision: Mock for Development, Twilio-Ready Abstraction

Build an `ISmsSender` interface. For dev, implement `ConsoleSmsSender` (logs to stdout). The interface signature is designed to be trivially implementable with Twilio, ACS, or AWS SNS.

---

## 4. Email Provider Options

### Option A: SendGrid (Twilio) (Recommended for Production)

| Aspect | Detail |
|--------|--------|
| **SDK** | `SendGrid` NuGet package |
| **Free Tier** | 100 emails/day (sufficient for dev/staging) |
| **Pricing** | $19.95/month for 50K emails |
| **.NET Support** | First-class: `SendGridClient.SendEmailAsync(msg)` |
| **Features** | Templates, tracking, delivery events, suppression lists |
| **Setup** | API key + verified sender identity |

### Option B: SMTP (Generic)

| Aspect | Detail |
|--------|--------|
| **SDK** | Built-in `System.Net.Mail.SmtpClient` or `MailKit` |
| **Free Tier** | Depends on SMTP server (Gmail: 500/day, Outlook: 300/day) |
| **Pricing** | Free for self-hosted SMTP or provider-specific |
| **.NET Support** | Native: `SmtpClient.SendMailAsync(message)` or MailKit's `SmtpClient` |
| **Features** | Basic send, TLS, authentication |
| **Setup** | SMTP host, port, credentials |
| **Dev/Test** | **MailHog** — captures all emails without relay, provides web UI |

### Option C: Azure Communication Services Email

| Aspect | Detail |
|--------|--------|
| **SDK** | `Azure.Communication.Email` NuGet package |
| **Free Tier** | 1,000 emails/month included with ACS |
| **Pricing** | $0.00025/email beyond free tier |
| **.NET Support** | `EmailClient.SendAsync(emailMessage)` |
| **Features** | Delivery tracking, Azure-native |
| **Setup** | ACS resource + Email Communication Service + verified domain |

### Option D: AWS SES

| Aspect | Detail |
|--------|--------|
| **SDK** | `AWSSDK.SimpleEmailV2` NuGet package |
| **Free Tier** | 3,000/month (from EC2), otherwise $0.10/1K emails |
| **.NET Support** | `SendEmailAsync(request)` |
| **Features** | Templates, tracking, bounce handling |
| **Setup** | AWS credentials + verified domain/email |

### Decision: MailHog (SMTP) for Development, SendGrid-Ready Abstraction

For dev/Docker, use **MailHog** (SMTP on port 1025, web UI on port 8025). The `IEmailSender` interface is designed to be implementable with any provider. The dev implementation uses `System.Net.Mail.SmtpClient` pointing at MailHog.

---

## 5. Push Notification Options

### Option A: Firebase Cloud Messaging (FCM) (Selected)

| Aspect | Detail |
|--------|--------|
| **SDK** | `FirebaseAdmin` NuGet package |
| **Pricing** | Free (unlimited messages) |
| **.NET Support** | `FirebaseMessaging.DefaultInstance.SendAsync(message)` |
| **Platforms** | Android (native), iOS (via APNs), Web (via service workers) |
| **Setup** | Firebase project + service account JSON key file |
| **Dev/Test** | Requires real Firebase project; can use topic messaging for testing |
| **React Native** | Expo's `expo-notifications` integrates with FCM natively |

### Option B: Azure Notification Hubs

| Aspect | Detail |
|--------|--------|
| **SDK** | `Microsoft.Azure.NotificationHubs` NuGet package |
| **Pricing** | Free tier: 1M pushes/month; Standard: $10/month for 10M |
| **.NET Support** | `NotificationHubClient.SendFcmNativeNotificationAsync(payload, tag)` |
| **Platforms** | Abstracts FCM + APNs + WNS + Baidu |
| **Setup** | Azure resource + APNs/FCM credentials configured in Azure |

### Option C: Expo Push Notifications

| Aspect | Detail |
|--------|--------|
| **SDK** | HTTP API (no official .NET SDK; simple HTTP POST) |
| **Pricing** | Free |
| **.NET Support** | Manual `HttpClient` — `POST https://exp.host/--/api/v2/push/send` |
| **Platforms** | iOS + Android via Expo managed workflow |
| **Setup** | Expo push tokens from the mobile app |
| **Limitation** | Only works for Expo-built apps; cannot send to web or non-Expo Android |

### Decision: FCM (stub for now)

FCM is the industry standard and integrates with both the React Native app (via `expo-notifications`) and potential web push (via FCM web). For now, implement a `ConsolePushSender` stub that logs to stdout. Real FCM integration requires a Firebase project and service account key, which can be configured later.

---

## 6. Dev/Test Tooling

### MailHog (Email Testing)

| Aspect | Detail |
|--------|--------|
| **Image** | `mailhog/mailhog:latest` |
| **SMTP Port** | 1025 (no auth, no TLS — for local dev only) |
| **Web UI** | 8025 — view all captured emails |
| **Docker** | Simple: `ports: ["8025:8025"]` + internal SMTP at `mailhog:1025` |
| **Usage** | All emails sent by the notifications service are captured and viewable in the web UI |

### Console SMS Logger (SMS Testing)

For dev, SMS content is logged to the application console via `ILogger<ConsoleSmsSender>`. No external service needed. Log format:

```
[INF] SMS → +919876543210: Your OTP is 123456. Valid for 5 minutes.
```

### Console Push Logger (Push Testing)

For dev, push notification content is logged to the application console. No Firebase credentials needed. Log format:

```
[INF] PUSH → <device-token> (android): Contribution Dues Generated — Contribution dues for period 202603 have been generated.
```

---

## 7. Architecture Decision: Provider Abstraction Layer

### Interface Design

Create three interfaces in `FundManager.BuildingBlocks/Notifications/`:

```csharp
// ISmsSender.cs
public interface ISmsSender
{
    Task<bool> SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

// IEmailSender.cs
public interface IEmailSender
{
    Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}

// IPushNotificationSender.cs
public interface IPushNotificationSender
{
    Task<bool> SendAsync(string deviceToken, string platform, string title, string body, CancellationToken ct = default);
}
```

### Recipient Contact Resolution

Create a `ChannelContact` record and `IRecipientResolver` interface:

```csharp
// ChannelContact.cs
public record ChannelContact(
    string? Email,
    string? Phone,
    IReadOnlyList<DeviceTokenInfo> DeviceTokens);

public record DeviceTokenInfo(string Token, string Platform);

// IRecipientResolver.cs
public interface IRecipientResolver
{
    Task<ChannelContact?> ResolveAsync(Guid recipientId, CancellationToken ct = default);
}
```

### DI Registration Pattern

```csharp
// Development / Docker
services.AddSingleton<ISmsSender, ConsoleSmsSender>();
services.AddSingleton<IEmailSender, SmtpEmailSender>();  // Points to MailHog
services.AddSingleton<IPushNotificationSender, ConsolePushSender>();

// Production (swap in later)
services.AddSingleton<ISmsSender, TwilioSmsSender>();
services.AddSingleton<IEmailSender, SendGridEmailSender>();
services.AddSingleton<IPushNotificationSender, FcmPushSender>();
```

### Why This Design

1. **Single Responsibility**: Each interface handles exactly one channel
2. **Testability**: Mock any provider in unit tests
3. **Environment Swapping**: DI registration determines which provider runs (dev vs prod)
4. **Gradual Adoption**: Start with mocks, swap in Twilio/SendGrid/FCM one at a time
5. **No vendor lock-in**: Code depends on `ISmsSender`, not `TwilioRestClient`

---

## 8. Recipient Contact Resolution

### Problem

The `NotificationDispatchService` receives `recipientId` (a `Guid`) but needs:
- **Email address** for email channel
- **Phone number** for SMS channel  
- **Device tokens** for push channel

The user's email and phone are stored in the Identity service (`identity.users` table). Device tokens are stored locally in the Notifications service (`notifications.device_tokens` table).

### Solution: Hybrid Resolution

| Data | Source | Method |
|------|--------|--------|
| Email, Phone | Identity service | HTTP call to internal API: `GET /api/users/{userId}/profile` |
| Device tokens | Notifications DB | Direct EF Core query on `DeviceTokens` DbSet |

### Implementation: `HttpRecipientResolver`

```csharp
public class HttpRecipientResolver : IRecipientResolver
{
    private readonly HttpClient _httpClient;  // Named client: "IdentityService"
    private readonly NotificationsDbContext _db;

    public async Task<ChannelContact?> ResolveAsync(Guid recipientId, CancellationToken ct)
    {
        // 1. Fetch user profile from Identity service
        var profile = await _httpClient.GetFromJsonAsync<UserProfile>(
            $"/api/users/{recipientId}/profile", ct);
        if (profile is null) return null;

        // 2. Fetch device tokens from local DB
        var deviceTokens = await _db.DeviceTokens
            .Where(d => d.UserId == recipientId)
            .Select(d => new DeviceTokenInfo(d.PushToken, d.Platform))
            .ToListAsync(ct);

        return new ChannelContact(profile.Email, profile.Phone, deviceTokens);
    }
}
```

### Caching Consideration

For high-throughput scenarios (e.g., fund-wide broadcast to 1,000 members), consider caching user contact info in Redis with a short TTL (5 minutes). This is a future optimisation — start without caching.

---

## 9. OTP Delivery Architecture

### Current Flow (Broken)

```
User → POST /auth/otp/request → Identity Service → generates OTP → returns OTP in response body
```

### Target Flow

```
User → POST /auth/otp/request → Identity Service
  ├── generates OTP
  ├── publishes OtpRequested event to RabbitMQ
  └── returns { challengeId, expiresAt, message: "OTP sent to +91****3210" }

RabbitMQ → Notifications Service → OtpRequestedConsumer
  ├── resolves channel (phone → SMS, email → Email)
  ├── renders "otp.requested" template with OTP code
  └── sends via ISmsSender or IEmailSender
```

### Integration Event

```csharp
public record OtpRequested(
    Guid Id,
    string Channel,    // "phone" or "email"
    string Target,     // "+919876543210" or "user@example.com"
    string Otp,        // Plaintext OTP (for sending only — NOT stored in cleartext)
    DateTime ExpiresAt,
    DateTime OccurredAt) : IIntegrationEvent;
```

### Security Consideration

The plaintext OTP travels over RabbitMQ (internal Docker network). This is acceptable because:
- RabbitMQ is not exposed to the internet (no external port mapping for 5672)
- The OTP is ephemeral (5-minute TTL) and single-use
- The message is consumed and discarded immediately

For production hardening:
- Enable RabbitMQ TLS
- Consider encrypting the OTP payload in the event
- Alternatively, call the SMS/email provider directly from the Identity service (bypassing the message bus) for lowest latency

### OTP Template

```
Template key: "otp.requested"
Title: "Your Verification Code"
Body: "Your verification code is {{otp}}. It expires in 5 minutes. Do not share this code with anyone."
```

---

## 10. Configuration Strategy

### appsettings.Docker.json (Notifications Service)

```json
{
  "Email": {
    "Provider": "Smtp",
    "SmtpHost": "mailhog",
    "SmtpPort": 1025,
    "FromAddress": "noreply@fundmanager.local",
    "FromName": "FundManager"
  },
  "Sms": {
    "Provider": "Console"
  },
  "Push": {
    "Provider": "Console"
  }
}
```

### appsettings.Development.json (Local without Docker)

```json
{
  "Email": {
    "Provider": "Smtp",
    "SmtpHost": "localhost",
    "SmtpPort": 1025,
    "FromAddress": "noreply@fundmanager.local",
    "FromName": "FundManager"
  },
  "Sms": {
    "Provider": "Console"
  },
  "Push": {
    "Provider": "Console"
  }
}
```

### Future: appsettings.Production.json

```json
{
  "Email": {
    "Provider": "SendGrid",
    "SendGridApiKey": "${SENDGRID_API_KEY}",
    "FromAddress": "noreply@fundmanager.com",
    "FromName": "FundManager"
  },
  "Sms": {
    "Provider": "Twilio",
    "TwilioAccountSid": "${TWILIO_ACCOUNT_SID}",
    "TwilioAuthToken": "${TWILIO_AUTH_TOKEN}",
    "TwilioFromNumber": "${TWILIO_FROM_NUMBER}"
  },
  "Push": {
    "Provider": "Fcm",
    "FcmCredentialPath": "/secrets/firebase-service-account.json"
  }
}
```

---

## 11. Decisions Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **SMS (dev)** | Console logger (`ConsoleSmsSender`) | No external dependencies; visible in Docker logs |
| **SMS (production)** | Twilio (future) | Best .NET SDK, India SMS support, delivery receipts |
| **Email (dev)** | MailHog via SMTP (`SmtpEmailSender`) | Captures all emails locally; web UI for verification |
| **Email (production)** | SendGrid (future) | Free tier, great .NET SDK, delivery tracking |
| **Push (dev)** | Console logger (`ConsolePushSender`) | No Firebase credentials needed for dev |
| **Push (production)** | Firebase Cloud Messaging (future) | Free, works with Expo/React Native, web push capable |
| **Provider abstraction** | `ISmsSender` / `IEmailSender` / `IPushNotificationSender` in BuildingBlocks | Vendor-agnostic; DI-swappable per environment |
| **Recipient resolution** | `IRecipientResolver` → HTTP call to Identity + local DeviceToken query | Cross-service contact lookup without shared DB |
| **OTP delivery** | MassTransit `OtpRequested` event → Notifications service consumer | Reuses existing notification pipeline; Identity stays focused on auth |
| **Dev email testing** | MailHog container in Docker Compose | Industry standard; zero config SMTP capture |
| **Docker network** | OTP plaintext over internal RabbitMQ (non-exposed) | Acceptable for dev; production should add TLS + encryption |

---

## Provider Comparison Matrix

| Feature | Twilio (SMS) | SendGrid (Email) | FCM (Push) | ACS (SMS+Email) | AWS SNS+SES |
|---------|-------------|-----------------|-----------|----------------|-------------|
| .NET SDK quality | ★★★★★ | ★★★★★ | ★★★★☆ | ★★★★☆ | ★★★★☆ |
| Free tier | Trial credit | 100/day | Unlimited | 1K email/mo | 3K email/mo |
| India SMS | ✅ (local routes) | N/A | N/A | ⚠️ Limited | ✅ |
| Delivery receipts | ✅ | ✅ | ✅ (partial) | ✅ | ✅ |
| Webhook callbacks | ✅ | ✅ | ✅ | ✅ | ✅ (via SNS) |
| Setup complexity | Low | Low | Medium | Medium | Medium |
| Vendor lock-in | Medium | Medium | Low (HTTP v1 API) | High (Azure) | High (AWS) |

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| OTP delivery latency via MassTransit | OTP may take 2–5s to arrive (message broker hop) | Acceptable for dev. For production, consider direct SMS call from Identity service (bypass MassTransit) |
| MailHog email size limit | Large emails (>10MB) may fail | Not relevant — notification emails are text-only with no attachments |
| Cross-service HTTP failure (Identity → Notifications for contact resolution) | Notification fails if Identity is down | Fallback to in_app notification (already implemented). Add retry with circuit breaker later |
| RabbitMQ message loss | OTP not delivered | RabbitMQ publisher confirms + consumer acknowledgements (already configured via MassTransit defaults) |
| Fund-wide broadcast N+1 problem | 1,000 HTTP calls to resolve 1,000 member contacts | Future: batch endpoint or event-carried state with local contact projection |

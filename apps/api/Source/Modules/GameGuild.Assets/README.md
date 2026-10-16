# GameGuild.Assets Module

**Purpose:** Content-addressable asset storage with multi-tenant isolation, secure delivery, and lifecycle management.

## Overview

The Assets module provides:
- File upload with chunked support and virus scanning
- Content deduplication via SHA-256 hashing
- Image transformation and thumbnailing
- Secure time-limited access token generation (HMAC-SHA256)
- Rate limiting and hotlink protection
- Integration with resource quotas for storage limits

## Architecture

```
Controllers (3):
├── AssetsController             [Authenticated - CRUD operations]
├── AssetsAdminController        [Admin only - system operations]
└── SecureDeliveryController     [Token-validated asset delivery]

Security Services:
├── AssetAuthorizationHandler    [Permission-based access control]
├── TenantAssetValidationService [Tenant isolation enforcement]
├── AssetRateLimitService        [Per-tenant/user rate limiting]
├── AssetTokenService            [HMAC-SHA256 token generation/validation]
├── DownloadWindowService        [Time-windowed access tokens]
└── HotlinkProtectionService     [Referrer validation]

Core Services:
├── AssetStorageService          [File system operations]
├── ContentHashService           [SHA-256 deduplication]
├── ImageTransformService        [On-the-fly transformations via Openinary]
└── AssetAccessService           [Unified access control]
```

## Integration: Openinary & Moderation

### Openinary Asset CDN
The module integrates with Openinary for scalable, high-performance asset delivery and transformation.
- **Backend:** Assets stored in Openinary are tagged with `BucketName="openinary"`.
- **Dual-Path Logic:** The system transparently routes between legacy S3-compatible storage and Openinary based on the asset's backend record.
- **Transformations:** Image transformations are handled by Openinary's URL-based transformation engine, removing the need for local processing.

### Auto-Moderation Worker
A background worker ensures content safety using an ML-based classifier.
- **Flow:** Upload $\rightarrow$ Quarantine $\rightarrow$ ML Classification $\rightarrow$ Auto-Approval/Rejection or Human Review.
- **Fail-Safe:** Any classification failure or low-confidence result triggers a `NeedsReview` status, ensuring no unverified content is auto-approved.
- **Tuning:** Thresholds for auto-approval and auto-rejection are configurable via `AutoModerationOptions`.

## Security Controls

### Authentication & Authorization

| Controller | Auth | Access Control |
|------------|------|----------------|
| `AssetsController` | `[Authorize]` | Tenant membership + asset ownership |
| `AssetsAdminController` | `[Authorize(Policy = "RequireAdminRole")]` | System admins only |
| `SecureDeliveryController` | Token-based | HMAC-SHA256 signed tokens |

### Token Security

The `AssetTokenService` generates secure, time-limited tokens:

- **Algorithm:** HMAC-SHA256 with configurable secret key
- **Token Format:** Base64URL encoded (22 bytes: timeWindow + expiry + signature)
- **Expiry:** Configurable via `AssetTokenOptions.DefaultExpiryHours` (default: 24)
- **Time Windows:** Anti-stampede with configurable window size (default: 8 hours)
- **Caching:** O(1) token validation via `ConcurrentDictionary` cache (max 10K entries)

### Rate Limiting

- Per-tenant rate limiting on upload/download
- Configurable burst and sliding window policies
- Integration with `RateLimitPolicies` from SharedKernel

### Content Security

- Virus scanning via `IVirusScanService` before storage
- Content-type validation and sanitization
- Maximum file size limits
- Allowed MIME type whitelist

## Threat Model (STRIDE)

### Spoofing

| Threat | Mitigation | Status |
|--------|------------|--------|
| Forge asset access tokens | HMAC-SHA256 with secret key; signature verification | ✅ Mitigated |
| Impersonate another tenant | `TenantAssetValidationService` verifies tenant membership | ✅ Mitigated |
| Access without authentication | `[Authorize]` on all controllers except token-validated delivery | ✅ Mitigated |

### Tampering

| Threat | Mitigation | Status |
|--------|------------|--------|
| Modify asset content | SHA-256 content addressing; any change creates new hash | ✅ Mitigated |
| Replay expired tokens | Token expiry timestamp + time window validation | ✅ Mitigated |
| Modify token claims | HMAC signature verification | ✅ Mitigated |

### Repudiation

| Threat | Mitigation | Status |
|--------|------------|--------|
| Deny asset upload | `CreatedAt`, `CreatedBy` audit fields on `AssetReference` | ✅ Mitigated |
| Deny asset access | Access logging with actor context | ✅ Mitigated |

### Information Disclosure

| Threat | Mitigation | Status |
|--------|------------|--------|
| Access other tenant's assets | Tenant isolation in `AssetAuthorizationHandler` | ✅ Mitigated |
| Enumerate asset IDs | Time-limited tokens; no direct ID access | ✅ Mitigated |
| Hotlinking from external sites | `HotlinkProtectionService` validates referrer | ✅ Mitigated |
| Token brute force | O(n) policy enumeration; rate limiting | ✅ Mitigated |

### Denial of Service

| Threat | Mitigation | Status |
|--------|------------|--------|
| Exhaust storage quota | Integration with `ResourceQuotaService` | ✅ Mitigated |
| Upload malicious files | Virus scanning before storage | ✅ Mitigated |
| Token validation CPU exhaustion | Token caching (10K max) with expiry eviction | ✅ Mitigated |
| Large file upload attacks | Configurable max file size limits | ✅ Mitigated |

### Elevation of Privilege

| Threat | Mitigation | Status |
|--------|------------|--------|
| User accesses admin endpoints | `RequireAdminRole` policy on `AssetsAdminController` | ✅ Mitigated |
| Cross-tenant asset access | `TenantAssetValidationService` enforces isolation | ✅ Mitigated |
| Token policy escalation | Policy embedded in token signature; cannot be changed | ✅ Mitigated |

## Configuration

```json
{
  "Assets": {
    "Token": {
      "SecretKey": "<base64-encoded-32-byte-key>",
      "DefaultExpiryHours": 24,
      "TimeWindowHours": 8
    },
    "Storage": {
      "MaxFileSizeBytes": 104857600,
      "AllowedMimeTypes": ["image/*", "video/*", "application/pdf"],
      "BasePath": "/data/assets"
    },
    "RateLimiting": {
      "UploadPerMinute": 10,
      "DownloadPerMinute": 100
    }
  }
}
```

## Dependencies

- `GameGuild.SharedKernel` - Base entities, CQRS, common abstractions
- `GameGuild.Identity.Authorization` - Permission-based authorization
- `GameGuild.Identity.Context` - Actor context for current user/tenant
- `GameGuild.Resources` - Quota integration for storage limits
- `GameGuild.Features` - Feature flag integration
- `GameGuild.Localization` - Error message localization

## Testing

Unit tests: `GameGuild.Assets.UnitTests` (68+ tests)
- Token generation and validation
- Authorization handler tests
- Virus scan integration tests
- Deduplication tests

---

**Last Updated:** 2026-01-15  
**Security Review:** ✅ Reference implementation for security patterns

# GameGuild.Compliance.Audit Module

## Overview

The `GameGuild.Compliance.Audit` module provides audit logging, tamper evidence,
retention and structured evidence packaging for accountable reviewer assessment.
Framework enum values do not establish full regulatory coverage or certification.

The packaging catalog currently includes the legacy ISO Annex A profile, ISO
ISMS/SoA, GDPR/DPIA and six explicit 2026 FedRAMP Rev5 class/path profiles. FedRAMP
uses tailored baseline identifiers, bundled official JSON schemas and signed
native JSON/text/CSV reviewer exports. Additional framework coverage and controlled
auditor delivery remain open in #178. See the [packaging architecture and verified
scope](../architecture/compliance-evidence-packaging.md).

**Namespace:** `GameGuild.Compliance.Audit`  
**Location:** `apps/api/Source/Modules/GameGuild.Compliance.Audit/`

## Purpose

This module provides enterprise-grade audit logging and compliance evidence generation capabilities, enabling organizations to:

- Track security-sensitive operations with detailed audit trails
- Generate compliance evidence packages for regulatory submissions
- Maintain tamper-evident logs with cryptographic verification
- Monitor and analyze security events and anomalies
- Export audit data for SIEM integration and external audits

## Key Features

### 1. Standard Audit Logging

Basic audit logging for tracking user actions, system events, and security-sensitive operations.

**Core Entities:**
- `AuditLog` - Standard audit log entries with metadata
- `AuditCategory` - Categorization (Authentication, Authorization, Permission, Data Access, etc.)
- `AuditRiskLevel` - Risk classification (Low, Medium, High, Critical)

**Key Services:**
- `IAuditService` - Main audit logging interface
- `AuditService` - Implementation with specialized logging methods

**Features:**
- IP address and user agent tracking
- Session correlation
- Success/failure tracking
- Correlation ID support for distributed operations
- Metadata as JSON for flexible custom fields

### 2. Compliance Evidence Packages

Generate structured compliance evidence for regulatory submissions and audits.

**Core Entity:**
- `ComplianceEvidencePackage` - Bundles audit logs with cryptographic proofs

**Supported Frameworks:**
- SOC2 (Service Organization Control 2)
- ISO 27001 (Information Security Management)
- GDPR (General Data Protection Regulation)
- HIPAA (Health Insurance Portability and Accountability Act)
- FERPA (Family Educational Rights and Privacy Act)
- PCI-DSS (Payment Card Industry Data Security Standard)

**Features:**
- Package versioning and approval workflow
- Digital signatures for tamper evidence
- SHA-256 package hashing
- Delivery tracking (SFTP, S3, Azure Blob Storage)
- Review and approval chain-of-custody

### 3. Tamper-Evident Audit Logs

Cryptographic hash chains for immutable audit trails (Write Once Read Many - WORM).

**Core Entity:**
- `TamperEvidentAuditLog` - Blockchain-inspired immutable logs

**Features:**
- Content hash calculation
- Previous hash chaining
- Sequence numbering
- Digital signatures with key management
- Verification status tracking
- SIEM integration for real-time forwarding
- Geographic location tracking (country, region, city)

**Use Cases:**
- Legal evidence preservation
- Forensic analysis
- Regulatory compliance requiring immutable records
- Change data capture with before/after snapshots

### 4. Field-Level Access Auditing

Granular tracking of read/write operations on sensitive data fields.

**Core Entity:**
- `FieldAccessAudit` - Tracks individual field access

**Features:**
- PII field masking in audit logs
- Sensitivity level classification
- Purpose tracking (e.g., "Customer Support Request")
- Access type (Read, Write, Delete)
- Compliance policy enforcement

**Sensitivity Levels:**
- Public
- Internal
- Confidential
- Restricted
- HighlyRestricted

### 5. Scheduled Audit Exports

Automated audit data export for compliance reporting and archival.

**Core Entities:**
- `ScheduledAuditExport` - Export job configuration
- `AuditExportHistory` - Execution tracking

**Features:**
- Cron-based scheduling
- Multiple export formats (CSV, JSON, XML, Parquet)
- Destination support (SFTP, S3, Azure Blob Storage, Google Cloud Storage)
- Framework-specific templates
- Encryption support
- Retention policy management

### 6. Security Audit Aggregation

Unified security monitoring across authentication, authorization, and audit logs.

**Service:**
- `ISecurityAuditAggregator` - Queries across multiple audit sources

**Features:**
- Failed authentication tracking
- Permission denial analysis
- High-risk action monitoring
- Security violation detection
- Anomaly detection

### 7. Audit Anomaly Detection

Identify suspicious patterns and security threats.

**Core Entity:**
- `AuditAnomaly` - Detected anomalies with severity classification

**Anomaly Types:**
- Unusual access patterns
- Privilege escalation attempts
- Data exfiltration indicators
- Brute force attacks
- Geographic location anomalies

**Severity Levels:**
- Low
- Medium
- High
- Critical

## Architecture

### Module Registration

```csharp
// In Program.cs or module configuration
services.AddAuditModule();
```

### Controllers

- `AuditController` - Standard audit log queries and statistics
- `SecurityAuditController` - Advanced security analytics and anomaly detection

### Database Context

Audit entities are registered in the `ApplicationDbContext` with:
- Soft delete filters
- Multi-tenancy support
- Optimistic concurrency control

## Usage Examples

### Basic Audit Logging

```csharp
await _auditService.LogAsync(new CreateAuditLogRequest
{
    ActionType = AuditActionTypes.UserCreated,
    ResourceType = "User",
    ResourceId = userId.ToString(),
    UserId = actorId,
    TenantId = tenantId,
    Description = "New user account created",
    Success = true,
    RiskLevel = AuditRiskLevel.Medium,
    Category = AuditCategory.Authentication
});
```

### Permission Auditing

```csharp
// On permission grant
await _auditService.LogPermissionGrantAsync(
    userId, 
    "admin:manage_users", 
    "Tenant", 
    tenantId.ToString()
);

// On permission denial
await _auditService.LogPermissionDenyAsync(
    userId, 
    "admin:delete_tenant", 
    "Tenant", 
    tenantId.ToString(), 
    "User lacks admin role"
);
```

### Tamper-Evident Logging

```csharp
var log = TamperEvidentAuditLog.Create(
    tenantId,
    userId,
    "UpdateUserProfile",
    "User",
    userId,
    beforeSnapshot,
    afterSnapshot,
    changes,
    "High",
    ipAddress,
    userAgent,
    country,
    region,
    city,
    previousHash,
    sequenceNumber
);

log.SetCryptographicHashes(contentHash, chainHash);
log.Sign(digitalSignature, signingKeyId);
```

### Compliance Package Creation

```csharp
var package = ComplianceEvidencePackage.Create(
    tenantId,
    "Q4 2025 SOC2 Audit",
    ComplianceFramework.SOC2,
    "1.0",
    startDate,
    endDate,
    preparedBy: "audit@company.com"
);

package.SetPackageContents(
    auditLogs: 15000,
    anomalies: 12,
    accessLogs: 50000,
    sizeBytes: packageSize
);

package.Sign(packageHash, digitalSignature);
package.MarkAsReviewed(reviewedBy: "security@company.com");
package.Approve(approvedBy: "ciso@company.com");
```

## Integration Points

### Identity Modules

The audit module integrates with:
- `GameGuild.Identity.Authentication` - Authentication event logging
- `GameGuild.Identity.Authorization` - Permission and policy audit trails
- `GameGuild.Identity.Users` - User lifecycle events

### Other Compliance Modules

Works alongside:
- `GameGuild.Compliance.KYC` - Identity verification auditing
- `GameGuild.Compliance.FERPA` - Educational privacy compliance

## Security Considerations

1. **Audit Log Integrity**: Use tamper-evident logs for critical security events
2. **PII Protection**: Enable field-level masking for sensitive data
3. **Retention Policies**: Configure appropriate retention based on regulatory requirements
4. **Access Control**: Restrict audit log access to authorized security personnel
5. **Encryption**: Enable encryption for audit exports and evidence packages
6. **SIEM Integration**: Forward critical events to SIEM for real-time monitoring

## Compliance Mappings

### SOC2 Type II
- CC6.1: Logical access controls - Permission audit trails
- CC7.2: Security monitoring - Anomaly detection
- CC7.3: Incident response - Security violation logging

### ISO 27001:2022
- A.9.4.1: Information access restriction - Field-level access auditing
- A.12.4.1: Event logging - Comprehensive audit logging
- A.12.4.2: Protection of log information - Tamper-evident logs
- A.12.4.3: Administrator and operator logs - Privileged action tracking

### GDPR
- Article 30: Records of processing activities - Audit trail maintenance
- Article 33: Breach notification - Security violation detection
- Article 32: Security of processing - Encryption and integrity controls

### HIPAA
- §164.308(a)(1)(ii)(D): Information system activity review - Audit log review
- §164.312(b): Audit controls - Comprehensive logging
- §164.312(c)(1): Integrity controls - Tamper-evident logs

## Testing

### Unit Tests
Location: `apps/api/Tests/GameGuild.Audit.UnitTests/`

Test coverage includes:
- AuditLog entity validation
- AuditService logging methods
- Compliance package workflow
- Tamper-evident hash chain verification

### Integration Tests
Location: `apps/api/Tests/GameGuild.Audit.IntegrationTests/`

Integration tests verify:
- Database persistence
- Multi-tenancy isolation
- Query performance
- Export functionality

### Performance Tests
Location: `apps/api/Tests/GameGuild.Audit.PerformanceTests/`

Performance benchmarks for:
- High-volume audit logging
- Compliance package generation
- Large dataset exports
- Anomaly detection queries

## Migration Notes

### From GameGuild.Audit (Pre-January 2026)

**Breaking Changes:**
- Namespace changed from `GameGuild.Audit` to `GameGuild.Compliance.Audit`
- Update all using statements: `using GameGuild.Compliance.Audit;`
- Update dependency injection registrations to use new namespace
- No database schema changes required - entities remain compatible

**Migration Steps:**
1. Update using statements in consuming code
2. Update project references in `.csproj` files
3. Rebuild solution to verify compilation
4. Run tests to ensure functionality unchanged

## Future Enhancements

Planned features:
- Machine learning-based anomaly detection
- Real-time compliance dashboard
- Automated compliance report generation
- Integration with cloud provider audit services (AWS CloudTrail, Azure Monitor, GCP Audit Logs)
- GraphQL API for audit queries
- Advanced search with ElasticSearch integration

## Related Documentation

- [Base Entity Documentation](./base-entity.md)
- [Authorization Module](./auth-module.md)
- [Tenant Module](./tenant-module.md)
- [DAC Strategy](../architecture/DAC-STRATEGY.md)
- [Security Audit Reports](../security/)

## Support

For questions or issues related to the Compliance.Audit module:
1. Check existing audit-related security documentation in `docs/security/`
2. Review integration tests for usage examples
3. Consult the compliance framework-specific guides

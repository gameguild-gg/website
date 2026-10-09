
namespace GameGuild.Commerce.Payments;

/// <summary>
///     Represents a customer's tax exemption certificate for a specific jurisdiction.
///     Used to validate whether a customer should be exempt from sales tax/VAT in a given region.
/// </summary>
public class CustomerTaxExemption : EntityBase
{
    private CustomerTaxExemption() { } // EF Core constructor

    /// <summary>
    ///     Creates a new customer tax exemption record.
    /// </summary>
    public static CustomerTaxExemption Create(
        Guid tenantId,
        Guid customerId,
        string jurisdictionCode,
        TaxExemptionType exemptionType,
        string certificateNumber,
        DateTime validFrom,
        DateTime? validUntil,
        string? issuingAuthority = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jurisdictionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateNumber);

        if (validUntil.HasValue && validUntil.Value <= validFrom)
        {
            throw new ArgumentException("Valid until date must be after valid from date", nameof(validUntil));
        }

        return new CustomerTaxExemption
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            JurisdictionCode = jurisdictionCode.ToUpperInvariant(),
            ExemptionType = exemptionType,
            CertificateNumber = certificateNumber,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            IssuingAuthority = issuingAuthority,
            Notes = notes,
            Status = TaxExemptionStatus.Active,
            VerificationStatus = ExemptionVerificationStatus.Pending
        };
    }

    /// <summary>
    ///     The customer who holds this exemption.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    ///     The jurisdiction code where this exemption applies (e.g., "US-CA", "DE", "GB").
    /// </summary>
    public string JurisdictionCode { get; private set; } = string.Empty;

    /// <summary>
    ///     The type of tax exemption.
    /// </summary>
    public TaxExemptionType ExemptionType { get; private set; }

    /// <summary>
    ///     The exemption certificate number issued by the tax authority.
    /// </summary>
    public string CertificateNumber { get; private set; } = string.Empty;

    /// <summary>
    ///     When the exemption becomes valid.
    /// </summary>
    public DateTime ValidFrom { get; private set; }

    /// <summary>
    ///     When the exemption expires (null = no expiration).
    /// </summary>
    public DateTime? ValidUntil { get; private set; }

    /// <summary>
    ///     The authority that issued the exemption certificate.
    /// </summary>
    public string? IssuingAuthority { get; private set; }

    /// <summary>
    ///     Additional notes about the exemption.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    ///     Current status of the exemption.
    /// </summary>
    public TaxExemptionStatus Status { get; private set; }

    /// <summary>
    ///     Verification status of the certificate.
    /// </summary>
    public ExemptionVerificationStatus VerificationStatus { get; private set; }

    /// <summary>
    ///     When the certificate was last verified.
    /// </summary>
    public DateTime? LastVerifiedAt { get; private set; }

    /// <summary>
    ///     Who verified the certificate (user ID or system identifier).
    /// </summary>
    public string? VerifiedBy { get; private set; }

    /// <summary>
    ///     Path to the uploaded certificate document.
    /// </summary>
    public string? CertificateDocumentPath { get; private set; }

    /// <summary>
    ///     Checks if this exemption is valid for the given date.
    /// </summary>
    public bool IsValidOn(DateTime date)
    {
        if (Status != TaxExemptionStatus.Active)
        {
            return false;
        }

        if (VerificationStatus != ExemptionVerificationStatus.Verified)
        {
            return false;
        }

        if (date < ValidFrom)
        {
            return false;
        }

        if (ValidUntil.HasValue && date > ValidUntil.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Checks if this exemption is valid now.
    /// </summary>
    public bool IsCurrentlyValid() => IsValidOn(SystemClock.UtcNow);

    /// <summary>
    ///     Marks the exemption as verified.
    /// </summary>
    public void MarkVerified(string verifiedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedBy);
        
        VerificationStatus = ExemptionVerificationStatus.Verified;
        LastVerifiedAt = SystemClock.UtcNow;
        VerifiedBy = verifiedBy;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Marks the exemption as rejected.
    /// </summary>
    public void MarkRejected(string rejectedBy, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedBy);
        
        VerificationStatus = ExemptionVerificationStatus.Rejected;
        Status = TaxExemptionStatus.Inactive;
        LastVerifiedAt = SystemClock.UtcNow;
        VerifiedBy = rejectedBy;
        if (!string.IsNullOrEmpty(reason))
        {
            Notes = $"{Notes}\nRejection reason: {reason}".Trim();
        }

        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Revokes an active exemption.
    /// </summary>
    public void Revoke(string revokedBy, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revokedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status == TaxExemptionStatus.Revoked)
        {
            throw new InvalidOperationException("Exemption is already revoked");
        }

        Status = TaxExemptionStatus.Revoked;
        Notes = $"{Notes}\nRevoked by {revokedBy}: {reason}".Trim();
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Updates the certificate document path.
    /// </summary>
    public void SetCertificateDocument(string documentPath)
    {
        CertificateDocumentPath = documentPath;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Extends the validity period of the exemption.
    /// </summary>
    public void ExtendValidity(DateTime newValidUntil)
    {
        if (Status != TaxExemptionStatus.Active)
        {
            throw new InvalidOperationException("Cannot extend inactive exemption");
        }

        if (newValidUntil <= ValidFrom)
        {
            throw new ArgumentException("New validity date must be after valid from date", nameof(newValidUntil));
        }

        ValidUntil = newValidUntil;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
///     Type of tax exemption.
/// </summary>
public enum TaxExemptionType
{
    /// <summary>Non-profit/charitable organization exemption</summary>
    NonProfit = 1,
    
    /// <summary>Educational institution exemption</summary>
    Educational = 2,
    
    /// <summary>Government entity exemption</summary>
    Government = 3,
    
    /// <summary>Reseller/wholesale exemption</summary>
    Reseller = 4,
    
    /// <summary>Agricultural exemption</summary>
    Agricultural = 5,
    
    /// <summary>Manufacturing exemption</summary>
    Manufacturing = 6,
    
    /// <summary>Diplomatic/embassy exemption</summary>
    Diplomatic = 7,
    
    /// <summary>Medical/healthcare exemption</summary>
    Medical = 8,
    
    /// <summary>Other exemption type</summary>
    Other = 99
}

/// <summary>
///     Status of a tax exemption.
/// </summary>
public enum TaxExemptionStatus
{
    /// <summary>Exemption is active and can be used</summary>
    Active = 1,
    
    /// <summary>Exemption is inactive (expired or deactivated)</summary>
    Inactive = 2,
    
    /// <summary>Exemption has been revoked</summary>
    Revoked = 3
}

/// <summary>
///     Verification status of an exemption certificate.
/// </summary>
public enum ExemptionVerificationStatus
{
    /// <summary>Certificate is pending verification</summary>
    Pending = 1,
    
    /// <summary>Certificate has been verified</summary>
    Verified = 2,
    
    /// <summary>Certificate was rejected during verification</summary>
    Rejected = 3
}

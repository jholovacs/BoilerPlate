namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     SAML2 settings data transfer object for tenant-level SSO configuration
/// </summary>
public class Saml2SettingsDto
{
    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Indicates whether SAML2 SSO is enabled for this tenant
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Identity Provider (IdP) entity ID
    /// </summary>
    public string? IdpEntityId { get; set; }

    /// <summary>
    ///     Identity Provider (IdP) SSO service URL (where to send authentication requests)
    /// </summary>
    public string? IdpSsoServiceUrl { get; set; }

    /// <summary>
    ///     Identity Provider (IdP) certificate (base64 encoded X.509 certificate)
    /// </summary>
    public string? IdpCertificate { get; set; }

    /// <summary>
    ///     Service Provider (SP) entity ID (this application's entity ID)
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    ///     Service Provider (SP) assertion consumer service (ACS) URL
    ///     This is where the IdP will send SAML responses
    /// </summary>
    public string? SpAcsUrl { get; set; }

    /// <summary>
    ///     Service Provider (SP) certificate (base64 encoded X.509 certificate for signing requests)
    /// </summary>
    public string? SpCertificate { get; set; }

    /// <summary>
    ///     Service Provider (SP) certificate private key (base64 encoded, optional if certificate includes private key)
    /// </summary>
    public string? SpCertificatePrivateKey { get; set; }

    /// <summary>
    ///     Name identifier format (e.g., "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress")
    /// </summary>
    public string? NameIdFormat { get; set; }

    /// <summary>
    ///     Attribute mapping: SAML attribute name to user property mapping (JSON format)
    ///     Example: {"email": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "username": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"}
    /// </summary>
    public string? AttributeMapping { get; set; }

    /// <summary>
    ///     Indicates whether to sign authentication requests
    /// </summary>
    public bool SignAuthnRequest { get; set; } = true;

    /// <summary>
    ///     Indicates whether to require signed SAML responses
    /// </summary>
    public bool RequireSignedResponse { get; set; } = true;

    /// <summary>
    ///     Indicates whether to require encrypted assertions
    /// </summary>
    public bool RequireEncryptedAssertion { get; set; } = false;

    /// <summary>
    ///     Clock skew tolerance in minutes (default: 5 minutes)
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 5;
}

/// <summary>
///     Request model for creating or updating SAML2 settings
/// </summary>
public class CreateOrUpdateSaml2SettingsRequest
{
    /// <summary>
    ///     Indicates whether SAML2 SSO is enabled for this tenant
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Identity Provider (IdP) entity ID
    /// </summary>
    public string? IdpEntityId { get; set; }

    /// <summary>
    ///     Identity Provider (IdP) SSO service URL (where to send authentication requests)
    /// </summary>
    public string? IdpSsoServiceUrl { get; set; }

    /// <summary>
    ///     Identity Provider (IdP) certificate (base64 encoded X.509 certificate)
    /// </summary>
    public string? IdpCertificate { get; set; }

    /// <summary>
    ///     Service Provider (SP) entity ID (this application's entity ID)
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    ///     Service Provider (SP) certificate (base64 encoded X.509 certificate for signing requests)
    /// </summary>
    public string? SpCertificate { get; set; }

    /// <summary>
    ///     Service Provider (SP) certificate private key (base64 encoded, optional if certificate includes private key)
    /// </summary>
    public string? SpCertificatePrivateKey { get; set; }

    /// <summary>
    ///     Name identifier format (e.g., "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress")
    /// </summary>
    public string? NameIdFormat { get; set; }

    /// <summary>
    ///     Attribute mapping: SAML attribute name to user property mapping (JSON format)
    ///     Example: {"email": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "username": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"}
    /// </summary>
    public string? AttributeMapping { get; set; }

    /// <summary>
    ///     Indicates whether to sign authentication requests
    /// </summary>
    public bool SignAuthnRequest { get; set; } = true;

    /// <summary>
    ///     Indicates whether to require signed SAML responses
    /// </summary>
    public bool RequireSignedResponse { get; set; } = true;

    /// <summary>
    ///     Indicates whether to require encrypted assertions
    /// </summary>
    public bool RequireEncryptedAssertion { get; set; } = false;

    /// <summary>
    ///     Clock skew tolerance in minutes (default: 5 minutes)
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 5;
}

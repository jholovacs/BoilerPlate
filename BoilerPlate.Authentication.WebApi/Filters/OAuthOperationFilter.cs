using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoilerPlate.Authentication.WebApi.Filters;

/// <summary>
///     Swagger operation filter to add comprehensive, well-formatted documentation for OAuth endpoints
/// </summary>
public class OAuthOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodName = context.MethodInfo.Name;
        var path = context.ApiDescription.RelativePath?.ToLowerInvariant() ?? "";

        // POST /oauth/token
        if (path.Contains("/oauth/token") && methodName == "Token")
        {
            operation.Summary = "OAuth2 Token Endpoint";
            operation.Description = @"Issues JWT access tokens and refresh tokens. Supports multiple OAuth2 grant types:

**Supported Grant Types:**

1. **Resource Owner Password Credentials Grant** (`grant_type=password`) - RFC 6749 Section 4.3
2. **Authorization Code Grant** (`grant_type=authorization_code`) - RFC 6749 Section 4.1.3

**Content-Type:**
- Password grant: `application/json` or `application/x-www-form-urlencoded`
- Authorization code grant: `application/x-www-form-urlencoded` (required)

**Password Grant Request Example (JSON):**
```json
{
  ""grant_type"": ""password"",
  ""username"": ""user@example.com"",
  ""password"": ""SecurePassword123!"",
  ""tenant_id"": ""11111111-1111-1111-1111-111111111111"",
  ""scope"": ""api.read api.write""
}
```

**Authorization Code Grant Request Example (form-encoded):**
```
grant_type=authorization_code
&code=abc123xyz789
&redirect_uri=https://myapp.com/callback
&client_id=my-web-app
&client_secret=client-secret-here
&code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk
```

**Response Example:**
```json
{
  ""access_token"": ""eyJhbGciOiJSUzI1NiIs..."",
  ""token_type"": ""Bearer"",
  ""expires_in"": 3600,
  ""refresh_token"": ""refresh-token-abc123..."",
  ""scope"": ""api.read api.write""
}
```

**Using the Token:**
Include the access token in the Authorization header:
```
Authorization: Bearer {access_token}
```

**Token Validation:**
The JWT is signed using RS256. Use the public key from `/.well-known/jwks.json` to validate the token signature.

**References:**
- [RFC 6749 Section 4.3](https://datatracker.ietf.org/doc/html/rfc6749#section-4.3) - Resource Owner Password Credentials Grant
- [RFC 6749 Section 4.1.3](https://datatracker.ietf.org/doc/html/rfc6749#section-4.1.3) - Authorization Code Grant Token Request
- [RFC 7519](https://datatracker.ietf.org/doc/html/rfc7519) - JSON Web Token (JWT)
- [JWT.io](https://jwt.io/) - JWT Debugger and Documentation";
        }

        // POST /oauth/refresh
        else if (path.Contains("/oauth/refresh") && methodName == "Refresh")
        {
            operation.Summary = "OAuth2 Refresh Token Endpoint";
            operation.Description =
                @"Issues a new access token using a valid refresh token, allowing applications to maintain user sessions without requiring credentials.

**⚠️ Status:** Currently not implemented. This endpoint requires database storage and validation of refresh tokens.

**When to use (when implemented):**
- When your access token has expired
- To maintain user sessions without prompting for credentials
- Periodically refresh tokens to ensure continuous access

**Request Example:**
```json
{
  ""grant_type"": ""refresh_token"",
  ""refresh_token"": ""refresh-token-abc123..."",
  ""scope"": ""api.read api.write""
}
```

**Security Best Practices:**
- Store refresh tokens securely (encrypted at rest)
- Set appropriate expiration times (30-90 days, configurable per tenant)
- Refresh tokens are reusable until they expire or are revoked
- Revoke tokens on logout or password change

**References:**
- [RFC 6749 Section 6](https://datatracker.ietf.org/doc/html/rfc6749#section-6) - Refreshing an Access Token
- [OAuth.net: Refresh Tokens](https://oauth.net/2/refresh-tokens/)";
        }

        // GET /oauth/authorize
        else if (path.Contains("/oauth/authorize") && methodName == "Authorize")
        {
            operation.Summary = "OAuth2 Authorization Endpoint";
            operation.Description = @"Initiates the Authorization Code grant flow (RFC 6749 Section 4.1).

**✅ Status:** Fully implemented with PKCE support (RFC 7636).

**When to use:**
- Third-party applications (OAuth clients you don't own)
- Web applications (server-side)
- Mobile applications using PKCE
- Single Page Applications (SPAs) using PKCE
- Situations where you cannot securely store user credentials

**How it works:**
1. **Client redirects user** to this endpoint with authorization request parameters
2. **User authenticates** (if not already authenticated) via the consent page
3. **User grants consent** to the requested scopes
4. **Authorization server redirects** back to client with authorization code
5. **Client exchanges authorization code** for access token at `POST /oauth/token` with `grant_type=authorization_code`

**Request Parameters:**
- `response_type`: Must be ""code"" (required)
- `client_id`: The client identifier registered with the authorization server (required)
- `redirect_uri`: URI where the server redirects after authorization. Must match a registered redirect URI for the client (required)
- `scope`: Space-delimited list of requested permissions (optional)
- `state`: Opaque value for CSRF protection, returned unchanged in redirect (recommended)
- `code_challenge`: PKCE code challenge - Base64URL-encoded SHA256 hash of code_verifier for S256 method (recommended)
- `code_challenge_method`: PKCE code challenge method - ""S256"" (recommended) or ""plain"" (not recommended)

**Example Authorization Request:**
```
GET /oauth/authorize?response_type=code&client_id=my-web-app&redirect_uri=https://myapp.com/callback&scope=api.read&state=xyz123&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256
```

**Success Response (Redirect):**
```
HTTP 302 Found
Location: https://myapp.com/callback?code=abc123xyz789&state=xyz123
```

**Error Response (Redirect):**
```
HTTP 302 Found
Location: https://myapp.com/callback?error=access_denied&error_description=User%20denied%20access&state=xyz123
```

**Security Best Practices:**
- Always use PKCE (code_challenge and code_challenge_method) for all clients, especially public clients
- Use `code_challenge_method=S256` (SHA256) instead of ""plain""
- Always include a `state` parameter for CSRF protection
- Validate the `state` parameter when receiving the authorization code
- Authorization codes expire after 10 minutes and are single-use only

**References:**
- [RFC 6749 Section 4.1](https://datatracker.ietf.org/doc/html/rfc6749#section-4.1) - Authorization Code Grant
- [RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636) - Proof Key for Code Exchange (PKCE)
- [OAuth.net - Authorization Code Flow](https://oauth.net/2/grant-types/authorization-code/)
- [Auth0 - Authorization Code Flow](https://auth0.com/docs/get-started/authentication-and-authorization-flow/authorization-code-flow)";
        }

        // GET /.well-known/jwks.json
        else if (path.Contains("/.well-known/jwks.json") && methodName == "GetJwks")
        {
            operation.Summary = "JSON Web Key Set (JWKS) Endpoint";
            operation.Description = @"Returns public keys for JWT signature validation in JWKS format (RFC 7517).

**When to use:**
- When validating JWT tokens in your API gateway, middleware, or resource servers
- To retrieve public keys for JWT signature verification
- As part of implementing JWT validation in microservices architectures

**Response Example:**
```json
{
  ""keys"": [
    {
      ""kty"": ""RSA"",
      ""use"": ""sig"",
      ""kid"": ""auth-key-1"",
      ""alg"": ""RS256"",
      ""n"": ""0vx7agoebGcQSuuPiLJXZptN9..."",
      ""e"": ""AQAB""
    }
  ]
}
```

**JWT Validation Process:**
1. Retrieve the JWKS from this endpoint (cache for performance)
2. Extract the ""kid"" (Key ID) from the JWT header
3. Find the matching key in the JWKS using the ""kid""
4. Use the public key (n, e) to verify the JWT signature
5. Validate JWT claims (exp, iss, aud, etc.)

**Security Considerations:**
- This endpoint is public - do not expose sensitive information
- Cache JWKS responses (keys change infrequently)
- Validate token signatures server-side
- Use HTTPS only

**Integration Examples:**
- **.NET:** Use `Microsoft.IdentityModel.Protocols.OpenIdConnect` with JwksUri
- **Node.js:** Use `jwks-rsa` library or `jsonwebtoken` with JWKS
- **Python:** Use `PyJWT` with `cryptography` library and JWKS
- **Go:** Use `github.com/lestrrat-go/jwx` for JWKS support

**References:**
- [RFC 7517](https://datatracker.ietf.org/doc/html/rfc7517) - JSON Web Key (JWK)
- [RFC 7519 Section 10.1](https://datatracker.ietf.org/doc/html/rfc7519#section-10.1) - Validating a JWT
- [Auth0: JSON Web Key Sets](https://auth0.com/docs/secure/tokens/json-web-tokens/json-web-key-sets)";
        }

        // POST /oauth/introspect
        else if (path.Contains("/oauth/introspect") && methodName == "Introspect")
        {
            operation.Summary = "OAuth2 Token Introspection (RFC 7662)";
            operation.Description =
                @"Queries the authorization server about the active state of a token. Allows resource servers to validate tokens before processing requests.

**✅ Status:** Fully implemented per RFC 7662.

**When to use:**
- Resource servers need to validate access tokens before processing requests
- Checking if a token is still valid before using it
- Retrieving token metadata (scopes, expiration, subject, etc.)
- Validating refresh tokens before use

**Token Types Supported:**
- Access tokens (JWT format)
- Refresh tokens (encrypted format stored in database)

**Request Format (form-encoded - RFC 7662 standard):**
```
token=eyJhbGciOiJSUzI1NiIs...&token_type_hint=access_token
```

**Request Format (JSON - also supported):**
```json
{
  ""token"": ""eyJhbGciOiJSUzI1NiIs..."",
  ""token_type_hint"": ""access_token""
}
```

**Response Example (Active Token):**
```json
{
  ""active"": true,
  ""token_type"": ""Bearer"",
  ""exp"": 1704067200,
  ""iat"": 1704063600,
  ""scope"": ""api.read api.write"",
  ""sub"": ""user-id-uuid"",
  ""username"": ""user@example.com"",
  ""tenant_id"": ""tenant-id-uuid""
}
```

**Response Example (Inactive Token):**
```json
{
  ""active"": false
}
```

**Security:**
- This endpoint requires authentication (Bearer token)
- In production, consider restricting to specific client credentials
- Per RFC 7662 Section 2.1, the introspection endpoint must be protected

**References:**
- [RFC 7662](https://datatracker.ietf.org/doc/html/rfc7662) - OAuth 2.0 Token Introspection
- [RFC 7519 Section 10.1](https://datatracker.ietf.org/doc/html/rfc7519#section-10.1) - JWT Validation";
        }
    }
}
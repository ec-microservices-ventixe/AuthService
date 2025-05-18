using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Security.KeyVault.Keys;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using WebApi.Models;
using Azure.Security.KeyVault.Keys.Cryptography;
using System.Text;
using System.Text.Json;
using JsonWebKey = Azure.Security.KeyVault.Keys.JsonWebKey;
using System.Data;
using System;

namespace WebApi.Services;

public class TokenService(IConfiguration config)
{
    private readonly IConfiguration _config = config;
    public async Task<string> GenerateAccessToken(string userId, string email, string role)
    {
        var uri = _config["AzureKeyVault:KeyVaultUri"];
        var keyName = _config["AzureKeyVault:RSAKey"];
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];
        try
        {
            var credential = new DefaultAzureCredential();
            var client = new KeyClient(new Uri(uri!), credential);
            var key = await client.GetKeyAsync(keyName);
            var cryptoClient = new CryptographyClient(key.Value.Id, credential);
            
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Sub, userId),
                new(JwtRegisteredClaimNames.Email, email),
                new(ClaimTypes.Role, role)
            };
            var header = new JwtHeader
            {
                { "alg", "RS256" },
                { "typ", "JWT" },
                { "kid", cryptoClient.KeyId }
            };

            var payload = new JwtPayload(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(60),
                issuedAt: DateTime.UtcNow
            );

            var handler = new JwtSecurityTokenHandler();
            var token = new JwtSecurityToken(header, payload);

            string unsignedJwt = handler.WriteToken(token);
            unsignedJwt = unsignedJwt.Substring(0, unsignedJwt.LastIndexOf("."));

            using var sha256 = SHA256.Create();
            var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(unsignedJwt));

            var signResult = await cryptoClient.SignAsync(SignatureAlgorithm.RS256, digest);
            var signature = Base64UrlEncoder.Encode(signResult.Signature);

            return $"{unsignedJwt}.{signature}";

        } catch ( Exception ex )
        {
            Debug.WriteLine( ex );
            return "";
        }
    }
    public RefreshToken GenerateRefreshToken(int familyId)
    {
        return new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Expires = DateTime.UtcNow.AddMinutes(90),
            RefreshTokenFamilyId = familyId
        };
    }

    public async Task<string> GetJwks()
    {
        var uri = _config["AzureKeyVault:KeyVaultUri"];
        var keyName = _config["AzureKeyVault:RSAKey"];

        var credential = new DefaultAzureCredential();
        var client = new KeyClient(new Uri(uri!), credential);
        var key = await client.GetKeyAsync(keyName);

        JsonWebKey jwk = key.Value.Key;

        var jwkObj = new
        {
            kty = "RSA",
            use = "sig",
            kid = key.Value.Id,
            n = Base64UrlEncoder.Encode(jwk.N),
            e = Base64UrlEncoder.Encode(jwk.E),
            alg = "RS256"
        };

        var jwks = new { keys = new[] { jwkObj } };

        string json = JsonSerializer.Serialize(jwks, new JsonSerializerOptions { WriteIndented = true });
        return json;
    }
}


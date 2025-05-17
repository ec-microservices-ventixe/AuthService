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

namespace WebApi.Services;

public class TokenService(IConfiguration config)
{
    private readonly IConfiguration _config = config;

    public async Task<string> GenerateRsaToken(string userId, string email, string role)
    {
        var uri = _config["AzureKeyVault:KeyVaultUri"];
        var keyName = _config["AzureKeyVault:RSAKey"];

        var credential = new DefaultAzureCredential();
        var client = new KeyClient(new Uri(uri!), credential);
        var key = await client.GetKeyAsync(keyName);
        var cryptoClient = new CryptographyClient(key.Value.Id, credential);
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];
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
    }
    public async Task<string> GenerateRsaTokenFromSecrets(string userId, string email, string role)
    {
        var uri = _config["AzureKeyVault:KeyVaultUri"];
        var secretName = _config["AzureKeyVault:RSAPrivateKey"];
        var client = new SecretClient(new Uri(uri!), new DefaultAzureCredential());

        try
        {
            KeyVaultSecret secret = await client.GetSecretAsync(secretName);
            string privateKeyPem = secret.Value;
            if (privateKeyPem is null) return "";

            privateKeyPem = privateKeyPem.Replace("-----BEGIN PRIVATE KEY-----", "");
            privateKeyPem = privateKeyPem.Replace("-----END PRIVATE KEY-----", "");

            byte[] privateKeyRaw = Convert.FromBase64String(privateKeyPem);

            RSA rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(new ReadOnlySpan<byte>(privateKeyRaw), out _);
            RsaSecurityKey rsaSecurityKey = new(rsa);

            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Sub, userId),
                new(JwtRegisteredClaimNames.Email, email),
                new(ClaimTypes.Role, role)
            };
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(60),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256)
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);


        } catch (Exception ex) 
        {
            Debug.WriteLine(ex.Message);
            return "null";
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

        Azure.Security.KeyVault.Keys.JsonWebKey jwk = key.Value.Key;

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


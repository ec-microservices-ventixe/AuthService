using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using WebApi.Models;

namespace WebApi.Services;

public class TokenService(IConfiguration config)
{
    private readonly IConfiguration _config = config;
    public async Task<string> GenerateRsaToken(string userId, string email, string role)
    {
        // https://docs.hidglobal.com/dev/auth-service/buildingapps/csharp/create-and-sign-a-json-web-token--jwt--with-c--and--net.htm
        // https://www.cerberauth.com/blog/rsa-key-pairs-openssl-jwt-signature/
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
}


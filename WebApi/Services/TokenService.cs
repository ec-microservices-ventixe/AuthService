using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebApi.Models;

namespace WebApi.Services;

public class TokenService(IConfiguration config)
{
    private readonly IConfiguration _config = config;
    public string GenerateToken(string userId, string email, string role)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]);
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
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    public string GenerateRsaToken(string userId, string email, string role)
    {
        // https://docs.hidglobal.com/dev/auth-service/buildingapps/csharp/create-and-sign-a-json-web-token--jwt--with-c--and--net.htm
        // https://www.cerberauth.com/blog/rsa-key-pairs-openssl-jwt-signature/
        var tokenHandler = new JwtSecurityTokenHandler();

        string privateKeyPem = File.ReadAllText("/Users/Emanuel/private_key.pem");
        privateKeyPem = privateKeyPem.Replace("-----BEGIN PRIVATE KEY-----", "");
        privateKeyPem = privateKeyPem.Replace("-----END PRIVATE KEY-----", "");

        byte[] privateKeyRaw = Convert.FromBase64String(privateKeyPem);

        using RSA rsa = RSA.Create();
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
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    public RefreshToken GenerateRefreshToken()
    {
        return new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Expires = DateTime.UtcNow.AddDays(7)
        };
    }
}


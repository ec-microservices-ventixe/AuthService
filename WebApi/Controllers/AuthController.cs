using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebApi.Data.Context;
using WebApi.Data.Entities;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(ApplicationDbContext context, TokenService tokenService, UserManager<AppUserEntity> userManager) : ControllerBase
{
    private readonly TokenService _tokenService = tokenService;
    private readonly UserManager<AppUserEntity> _userManager = userManager;
    private readonly ApplicationDbContext _context = context;

    [HttpPost("/SignUp")]
    public async Task<IActionResult> SignUp([FromBody] SignUpModel form)
    {
        if(!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var existingUser = await _userManager.FindByEmailAsync(form.Email);
            if (existingUser != null)
                return Conflict("Email already exists");

            var user = new AppUserEntity { Email = form.Email, UserName = form.Email};

            var result = await _userManager.CreateAsync(user, form.Password);
            var thisIsTheFirstUser = _userManager.Users.Count() == 1;
            if (thisIsTheFirstUser)
                await _userManager.AddToRoleAsync(user, "Admin");
            else
                await _userManager.AddToRoleAsync(user, "User");

            return Ok("You Signed up successfully");

        } catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Something went wrong");
        }
    }
    [HttpPost("/SignIn")]
    public async Task<IActionResult> SignIn([FromBody] SignInModel form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var user = await _userManager.FindByEmailAsync(form.Email);
            if (user == null) return Unauthorized("Invalid Email or password");
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, form.Password);
            if (!isPasswordValid) return Unauthorized("Invalid Email or password");

            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateRsaToken(user.Id, form.Email, roles[0]);

            var refreshTokenFamily = new RefreshTokenFamilyEntity();
            _context.RefreshTokensFamilies.Add(refreshTokenFamily);
            await _context.SaveChangesAsync();

            var refreshToken = _tokenService.GenerateRefreshToken(refreshTokenFamily.Id);
            bool setRefreshToken = await SetRefreshToken(refreshToken, user.Id);
            Response.Headers.Append("Bearer-Token", token);
            return Ok("You Signed in successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Something went wrong");
        }
    }

    [HttpPost("/signout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Ok("Already logged out");

        var tokenEntity = await _context.AppUsersRefreshTokens
            .Include(t => t.RefreshTokenFamily)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (tokenEntity is not null)
            await LockRefreshTokenFamily(tokenEntity.RefreshTokenFamily);

        Response.Cookies.Delete("refreshToken");
        return Ok("Logged out");
    }

    [HttpPost("/refreshToken")]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized("Refresh token is missing.");

        var validTokenFromDb = await ValidateRefreshToken(refreshToken);

        if (validTokenFromDb is null)
            return Unauthorized("Refresh token is not valid or locked");

        var newRefreshToken = _tokenService.GenerateRefreshToken(validTokenFromDb.RefreshTokenFamilyId);

        var user = await _userManager.FindByIdAsync(validTokenFromDb.UserId);
        if (user is null) return StatusCode(500, "Something went wrong");
        var roles = await _userManager.GetRolesAsync(user);

        var token = _tokenService.GenerateRsaToken(user.Id, user.Email, roles[0]);

        bool setRefreshToken = await SetRefreshToken(newRefreshToken, user.Id);
        Response.Headers.Append("Bearer-Token", token);

        return Ok("Token refreshed successfully");
    }

    private async Task<AppUserRefreshTokenEntity?> ValidateRefreshToken(string tokenFromClient)
    {
        try
        {
            var foundToken = await _context.AppUsersRefreshTokens
            .Include(rt => rt.User)
            .Include(rt => rt.RefreshTokenFamily)
            .FirstOrDefaultAsync(rt => rt.Token == tokenFromClient);

            if (foundToken == null || foundToken.Expires < DateTime.UtcNow || foundToken.IsLocked)
                return null;

            if (foundToken.HasRotated)
            {
                await LockRefreshTokenFamily(foundToken.RefreshTokenFamily);
                return null;
            }

            foundToken.HasRotated = true;
            await _context.SaveChangesAsync();

            return foundToken;
        } catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);    
            return null;
        }
        
    }

    private async Task<bool> SetRefreshToken(RefreshToken newRefreshToken, string userId)
    {
        try
        {
            var refreshToken = new AppUserRefreshTokenEntity 
            { 
                Token = newRefreshToken.Token, 
                Created = newRefreshToken.Created, 
                Expires = newRefreshToken.Expires, 
                UserId = userId,
                RefreshTokenFamilyId = newRefreshToken.RefreshTokenFamilyId};
            _context.AppUsersRefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, 
                SameSite = SameSiteMode.None,
                Expires = newRefreshToken.Expires
            };
            Response.Cookies.Append("refreshToken", newRefreshToken.Token, cookieOptions);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return false;
        }
    }
    private async Task LockRefreshTokenFamily(RefreshTokenFamilyEntity family)
    {
        try
        {
            family.IsLocked = true;
            var refreshTokens = await _context.AppUsersRefreshTokens
            .Where(t => t.RefreshTokenFamilyId == family.Id)
            .ToListAsync();

            foreach (var token in refreshTokens)
            {
                token.IsLocked = true;
            }
            await _context.SaveChangesAsync();
        }          
        catch (Exception ex) 
        {
            Debug.WriteLine(ex.Message);
        }
    }
}

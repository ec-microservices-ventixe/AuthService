using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using WebApi.Data.Context;
using WebApi.Data.Entities;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(ApplicationDbContext context, TokenService tokenService, UserManager<AppUserEntitiy> userManager) : ControllerBase
{
    private readonly TokenService _tokenService = tokenService;
    private readonly UserManager<AppUserEntitiy> _userManager = userManager;
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

            var user = new AppUserEntitiy { Email = form.Email, UserName = form.Email};

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
            var refreshToken = _tokenService.GenerateRefreshToken();
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

    [Authorize]
    [HttpPost("/SignOut")]
    public new IActionResult SignOut()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Invalid user");
            RemoveAllUsersRefreshToken(userId);

            return Ok(new { message = "Signed out successfully" });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Sign out failed");
        }
    }

    [HttpPost("/refreshToken")]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized("Refresh token is missing.");

        var foundToken = await _context.AppUsersRefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (foundToken == null || foundToken.Expires < DateTime.UtcNow)
            return Unauthorized("Refresh token is invalid or expired.");

        var user = _userManager.Users.FirstOrDefault(x => x.Id == foundToken.UserId);
        if (user is null) return StatusCode(500, "Something went wrong");
        var roles = await _userManager.GetRolesAsync(user);

        _context.AppUsersRefreshTokens.Remove(foundToken);

        var token = _tokenService.GenerateRsaToken(user.Id, user.Email, roles[0]);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        bool setRefreshToken = await SetRefreshToken(newRefreshToken, user.Id);
        Response.Headers.Append("Bearer-Token", token);
        return Ok("Token refreshed successfully");
    }


    private async Task<bool> SetRefreshToken(RefreshToken newRefreshToken, string userId)
    {
        try
        {
            var refreshToken = new AppUserRefreshTokenEntity { Token = newRefreshToken.Token, Created = newRefreshToken.Created, Expires = newRefreshToken.Expires, UserId = userId };
            _context.AppUsersRefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
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

    private void RemoveAllUsersRefreshToken(string userId)
    {
        var tokens = _context.AppUsersRefreshTokens.Where(x => x.UserId == userId);
        foreach (var token in tokens)
            _context.AppUsersRefreshTokens.Remove(token);
        _context.SaveChanges();
    }
}

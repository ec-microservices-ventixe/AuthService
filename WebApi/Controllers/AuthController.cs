using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(TokenService tokenService, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager) : ControllerBase
{
    private readonly TokenService _tokenService = tokenService;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly SignInManager<IdentityUser> _signInManager = signInManager;

    [HttpPost("/SignUp")]
    public async Task<IActionResult> SignUp([FromBody] SignUpModel form)
    {
        if(!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var existingUser = await _userManager.FindByEmailAsync(form.Email);
            if (existingUser != null)
                return Conflict("Email already exists");

            var user = new IdentityUser { Email = form.Email, UserName = form.Email};

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
            var result = await _signInManager.PasswordSignInAsync(form.Email, form.Password, false, false);
            if(!result.Succeeded) return StatusCode(500, "Failed to sign in");
            var user = _userManager.Users.FirstOrDefault(x => x.Email == form.Email);
            if(user is null) return StatusCode(500, "Something went wrong");

            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateToken(user.Id, form.Email, roles[0]);
            Response.Headers.Append("Bearer-Token", token);
            return Ok("You Signed in successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Something went wrong");
        }
    }
}

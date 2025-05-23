﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Amqp.Framing;
using System.ComponentModel;
using System.Diagnostics;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(AuthService authService, TokenService tokenService) : ControllerBase
{
    private readonly AuthService _authService = authService;
    private readonly TokenService _tokenService = tokenService;

    [HttpPost("/signup")]
    public async Task<IActionResult> SignUp([FromBody] SignUpModel form)
    {
        if(!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _authService.SignUp(form);
            if(result.Success == true)
            {
                return Ok(new {success = true, message = "You have successfully registerd your account, Check your email inbox we have sent you a link to confirm your account"});
            }
            return StatusCode(result.StatusCode, result.ErrorMessage);

        } catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Something went wrong");
        }
    }
    [HttpPost("/confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string email, [FromQuery] string token)
    {
        try
        {
            var result = await _authService.ConfirmEmail(email, token);
            if (result.Success == true)
            {
                return Ok(new {success = true, message = "Ýour email is no confirmed and"});
            }
            return StatusCode(result.StatusCode, result.ErrorMessage);

        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Something went wrong");
        }
    }

    [HttpPost("/resend-email-confirmation")]
    public async Task<IActionResult> ResendEmailConfirmation([FromQuery] string email)
    {
        try
        {
            var result = await _authService.ResendConfirmationLink(email);
            if (result.Success == true)
            {
                return Ok("Confirmation link is sent to your email. Please check your inbox");
            }
            return StatusCode(result.StatusCode, result.ErrorMessage);

        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Something went wrong");
        }
    }

    [HttpPost("/signin")]
    public async Task<IActionResult> SignIn([FromBody] SignInModel form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var CheckCredentialsResult = await _authService.CheckCredentials(form);
            if (CheckCredentialsResult.Data is null) return StatusCode(CheckCredentialsResult.StatusCode, CheckCredentialsResult.ErrorMessage!);
            var user = CheckCredentialsResult.Data;

            var RefreshTokenResult = await _authService.GetGeneratedRefreshToken(user.Id);
            if(RefreshTokenResult.Data is null)
                return StatusCode(500, "Failed To create refresh token");
            SetRefreshTokenCookie(RefreshTokenResult.Data);

            var token = await _authService.GetGeneratedAccessToken(user);
            if (string.IsNullOrEmpty(token))
                return StatusCode(500, "Failed To create access token");
            Response.Headers.Append("Bearer-Token", token);

            return Ok(new { success = true, message = "You Signed in successfully" });
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

        await _authService.RemoveRefreshTokenFamilyByToken(refreshToken);

        Response.Cookies.Delete("refreshToken");
        return Ok();
    }

    [HttpPost("/refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { success = false, message = "Refresh token is missing." });

        var newRefreshTokenResult = await _authService.RotateRefreshToken(refreshToken);
        var ( newToken, user ) = newRefreshTokenResult.Data.ToTuple();
        if (!newRefreshTokenResult.Success || newToken is null)
            return Unauthorized(new { success = false, message = "Access Denied" });
        SetRefreshTokenCookie(newToken);

        var token = await _authService.GetGeneratedAccessToken(user);
        if (string.IsNullOrEmpty(token))
            return StatusCode(500, "Failed To create access token");

        Response.Headers.Append("Bearer-Token", token);

        return Ok();
    }

    [HttpPost("/forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromQuery] string email)
    {
        try
        {
            var result = await _authService.SendForgotPasswordLink(email);
            if (!result.Success) return StatusCode(result.StatusCode, $"{result.ErrorMessage}");
            return Ok(new { success = true, message = "A password reset link is sent to your email, please check your inbox" });

        } catch(Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Something went wrong");
        }
    }

    [HttpPost("/reset-password")]
    public async Task<IActionResult> ResetPassword ([FromBody] PasswordResetModel form, [FromQuery] string email, [FromQuery] string token)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _authService.ResetPassword(email, token, form.Password);
            if (!result.Success) return StatusCode(result.StatusCode, $"{result.ErrorMessage}");
            return Ok(new { success = true, message = "A password reset link is sent to your email, please check your inbox" });

        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return StatusCode(500, "Something went wrong");
        }
    }

    [HttpGet("/.well-known/jwks.json")]
    public async Task<IActionResult> Jwks()
    {
        string jwks = await _tokenService.GetJwks();
        if (jwks is not null)
        {
            return Ok(jwks);
        }
        return StatusCode(500, "Could not get keys");
    }

    private bool SetRefreshTokenCookie(RefreshToken newRefreshToken)
    {
        try
        {
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
}

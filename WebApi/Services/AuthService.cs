using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Amqp.Framing;
using System.Diagnostics;
using System.Net;
using WebApi.Data.Entities;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

public class AuthService(IRefreshTokenRepository refreshTokenRepository, IRefreshTokenFamilyRepository refreshTokenFamilyRepository, UserManager<AppUserEntity> userManager, TokenService tokenService, ServiceBusService serviceBusService)
{
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IRefreshTokenFamilyRepository _refreshTokenFamilyRepository = refreshTokenFamilyRepository;
    private readonly UserManager<AppUserEntity> _userManager = userManager;
    private readonly TokenService _tokenService = tokenService;
    private readonly ServiceBusService _serviceBusService = serviceBusService;

    public async Task<ServiceResult<bool>> SignUp(SignUpModel form)
    {
        try
        {
            var existingUser = await _userManager.FindByEmailAsync(form.Email);
            if (existingUser != null)
                return ServiceResult<bool>.Conflict("Email already exists");

            var user = new AppUserEntity { Email = form.Email, UserName = form.Email };

            var result = await _userManager.CreateAsync(user, form.Password);
            var thisIsTheFirstUser = _userManager.Users.Count() == 1;
            if (thisIsTheFirstUser)
                await _userManager.AddToRoleAsync(user, "Admin");
            else
                await _userManager.AddToRoleAsync(user, "User");

            var confirmEmailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(confirmEmailToken);

            var msg = new ValidateEmailMessage { Email = user.Email, Token = encodedToken };
            bool msgSent = await _serviceBusService.AddToQueue("validate-email-queue", msg);
            if (!msgSent) return ServiceResult<bool>.Error("Could not add to email validation queue");

            return ServiceResult<bool>.Ok("Successfully signed up, please confirm your email.");

        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return ServiceResult<bool>.Error("Something went wrong");
        }
    }

    public async Task<ServiceResult<bool>> ResendConfirmationLink(string email)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null) return ServiceResult<bool>.BadRequest("User does not exist");

            var confirmEmailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(confirmEmailToken);

            var msg = new ValidateEmailMessage { Email = user.Email, Token = encodedToken };
            bool msgSent = await _serviceBusService.AddToQueue("validate-email-queue", msg);
            if (!msgSent) return ServiceResult<bool>.Error("Could not add to email validation queue");

            return ServiceResult<bool>.Ok("Confirmation email has been resent. Please check your inbox.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return ServiceResult<bool>.Error("Could not send email confirmation link");
        }
    }

    public async Task<ServiceResult<bool>> ConfirmEmail(string email, string token)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null) return ServiceResult<bool>.BadRequest("User does not exist");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded) return ServiceResult<bool>.BadRequest("Token is invalid");

            return ServiceResult<bool>.Ok("Successfully confirmed email");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return ServiceResult<bool>.Error("Could not confirm email");
        }
    }


    public async Task<ServiceResult<AppUserEntity>> CheckCredentials(SignInModel form)
    {
        var user = await _userManager.FindByEmailAsync(form.Email);
        if (user is null) return ServiceResult<AppUserEntity>.Unauthorized("Invalid password or email");

        bool emailIsConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        if (!emailIsConfirmed) return ServiceResult<AppUserEntity>.Unauthorized("You must confirm your email");


        var isPasswordValid = await _userManager.CheckPasswordAsync(user, form.Password);
        if (!isPasswordValid) return ServiceResult<AppUserEntity>.Unauthorized("Invalid password or email");

        return ServiceResult<AppUserEntity>.Ok(user);
    }

    public async Task<string> GetGeneratedAccessToken(AppUserEntity user)
    {
        try
        {
            var roles = await _userManager.GetRolesAsync(user);
            var token = await _tokenService.GenerateRsaToken(user.Id, user.Email!, roles[0]);
            return token;
        }
        catch (Exception ex) 
        {
            Debug.WriteLine(ex.Message);
            return "";
        }
    }
    public async Task<ServiceResult<RefreshToken>> GetGeneratedRefreshToken(string userId)
    {
        try
        {
            var refreshTokenFamily = new RefreshTokenFamilyEntity();
            var refreshFamilyResult = await _refreshTokenFamilyRepository.CreateAsync(refreshTokenFamily);
            if (refreshFamilyResult is null) return ServiceResult<RefreshToken>.Error("Could Not create refresh token family");

            var refreshToken = _tokenService.GenerateRefreshToken(refreshTokenFamily.Id);
            var refreshTokenResult = await _refreshTokenRepository.CreateAsync(new AppUserRefreshTokenEntity { Token = refreshToken.Token, Expires = refreshToken.Expires, UserId = userId, RefreshTokenFamilyId = refreshToken.RefreshTokenFamilyId });
            if(refreshTokenResult is null) return ServiceResult<RefreshToken>.Error("Could Not create refresh token");

            return ServiceResult<RefreshToken>.Ok(refreshToken);

        } catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return ServiceResult<RefreshToken>.Error("Could Not create refresh token");
        }  
    }

    public async Task<ServiceResult<(RefreshToken, AppUserEntity)>> RotateRefreshToken(string oldToken)
    {
        try
        {
            var foundToken = await _refreshTokenRepository.GetAsync(x => x.Token == oldToken);

            if (foundToken == null || foundToken.Expires < DateTime.UtcNow || foundToken.IsLocked)
                return ServiceResult<(RefreshToken, AppUserEntity)>.Error("The token is not valid");

            if (foundToken.HasRotated)
            {
                await RemoveRefreshTokenFamily(foundToken.RefreshTokenFamilyId);
                return ServiceResult<(RefreshToken, AppUserEntity)>.Error("The Token is potentially compremised");
            }

            foundToken.HasRotated = true;
            await _refreshTokenRepository.UpdateAsync(foundToken);

            
            var newToken = _tokenService.GenerateRefreshToken(foundToken.RefreshTokenFamilyId);
            await _refreshTokenRepository.CreateAsync(new AppUserRefreshTokenEntity
            {
                Token = foundToken.Token,
                Created = foundToken.Created,
                Expires = foundToken.Expires,
                UserId = foundToken.UserId,
                RefreshTokenFamilyId = foundToken.RefreshTokenFamilyId
            });

            return ServiceResult<(RefreshToken, AppUserEntity)>.Ok((newToken, foundToken.User));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return ServiceResult<(RefreshToken, AppUserEntity)>.Error("Could Not Rotate refresh token");
        }
    }
    public async Task RemoveRefreshTokenFamily(int familyId)
    {
        try
        {
            await _refreshTokenFamilyRepository.DeleteAsync(x => x.Id == familyId);
            var refreshTokens = await _refreshTokenRepository.GetAllAsync();
            var refreshtokensToRemove = refreshTokens.Where(x => x.RefreshTokenFamilyId == familyId);

            foreach (var token in refreshtokensToRemove)
            {
                await _refreshTokenRepository.DeleteAsync(token);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }
    public async Task RemoveRefreshTokenFamilyByToken(string refreshToken)
    {
        try
        {
            var foundToken = await _refreshTokenRepository.GetAsync(x => x.Token == refreshToken);
            await _refreshTokenFamilyRepository.DeleteAsync(foundToken.RefreshTokenFamily);

            var refreshtokensToRemove = (await _refreshTokenRepository.GetAllAsync()).Where(x => x.RefreshTokenFamilyId == foundToken.RefreshTokenFamilyId);

            foreach (var token in refreshtokensToRemove)
            {
                await _refreshTokenRepository.DeleteAsync(token);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }
}

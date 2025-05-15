using Azure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using System.Diagnostics;
using WebApi.Data.Entities;
using WebApi.Interfaces;
using WebApi.Models;
using WebApi.Repositories;

namespace WebApi.Services;

public class AuthService(IAppUserRepository userRepository, IRefreshTokenRepository refreshTokenRepository, IRefreshTokenFamilyRepository refreshTokenFamilyRepository, UserManager<AppUserEntity> userManager, TokenService tokenService)
{
    private readonly IAppUserRepository _userRepository = userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IRefreshTokenFamilyRepository _refreshTokenFamilyRepository = refreshTokenFamilyRepository;
    private readonly UserManager<AppUserEntity> _userManager = userManager;
    private readonly TokenService _tokenService = tokenService;

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

            return ServiceResult<bool>.Ok("You Signed up successfully");

        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return ServiceResult<bool>.Error("Something went wrong");
        }
    }

    public async Task<AppUserEntity?> CheckCredentials(SignInModel form)
    {
        var user = await _userManager.FindByEmailAsync(form.Email);
        if (user is null) return null;

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, form.Password);
        if (!isPasswordValid) return null;

        return user;
    }

    public async Task<string> GetGeneratedAccessToken(AppUserEntity user)
    {
        try
        {
            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateRsaToken(user.Id, user.Email, roles[0]);
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
            var user = await _userManager.FindByIdAsync(foundToken.UserId);
            if (user is null) return ServiceResult<(RefreshToken, AppUserEntity)>.NotFound("User not found");

            if (foundToken == null || foundToken.Expires < DateTime.UtcNow || foundToken.IsLocked)
                return ServiceResult<(RefreshToken, AppUserEntity)>.Error("The token is not valid");

            if (foundToken.HasRotated)
            {
                await RemoveRefreshTokenFamily(foundToken.RefreshTokenFamilyId);
                return ServiceResult<(RefreshToken, AppUserEntity)>.Error("The Token is potentially compremised");
            }

            foundToken.HasRotated = true;
            await _refreshTokenRepository.UpdateAsync(x => x.Id == foundToken.Id, foundToken);

            
            var newToken = _tokenService.GenerateRefreshToken(foundToken.RefreshTokenFamilyId);
            await _refreshTokenRepository.CreateAsync(new AppUserRefreshTokenEntity
            {
                Token = foundToken.Token,
                Created = foundToken.Created,
                Expires = foundToken.Expires,
                UserId = foundToken.UserId,
                RefreshTokenFamilyId = foundToken.RefreshTokenFamilyId
            });

            return ServiceResult<(RefreshToken, AppUserEntity)>.Ok((newToken, user));
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
            var familyId = (await _refreshTokenRepository.GetAsync(x => x.Token == refreshToken)).RefreshTokenFamilyId;
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
}

using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;
using FactoryPulse.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace FactoryPulse.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IJwtTokenGenerator tokenGenerator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            return Result.Failure<AuthResponse>(Errors.Auth.InvalidRole);
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Result.Failure<AuthResponse>(Errors.Auth.EmailAlreadyExists);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors
                .Select(identityError => Error.Validation(identityError.Code, identityError.Description))
                .ToList();
            return Result.Failure<AuthResponse>(errors);
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        return await BuildAuthResponseAsync(user);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Result.Failure<AuthResponse>(Errors.Auth.InvalidCredentials);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return Result.Failure<AuthResponse>(Errors.Auth.InvalidCredentials);
        }

        return await BuildAuthResponseAsync(user);
    }

    private async Task<Result<AuthResponse>> BuildAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenGenerator.GenerateAccessToken(user.Id, user.Email!, roles);

        return new AuthResponse
        {
            AccessToken = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            Email = user.Email!,
            Roles = roles.ToList()
        };
    }
}

using FactoryPulse.Application.Common;
using FactoryPulse.Application.DTOs;
using FactoryPulse.Application.Interfaces;
using FactoryPulse.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace FactoryPulse.Tests.Infrastructure;

public class AuthServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager = CreateUserManager();
    private readonly RoleManager<IdentityRole> _roleManager = CreateRoleManager();
    private readonly IJwtTokenGenerator _tokenGenerator = Substitute.For<IJwtTokenGenerator>();

    private AuthService CreateService()
    {
        return new AuthService(_userManager, _roleManager, _tokenGenerator);
    }

    private static UserManager<ApplicationUser> CreateUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);
    }

    private static RoleManager<IdentityRole> CreateRoleManager()
    {
        var store = Substitute.For<IRoleStore<IdentityRole>>();
        return Substitute.For<RoleManager<IdentityRole>>(store, null, null, null, null);
    }

    private static RegisterRequest ValidRegisterRequest()
    {
        return new RegisterRequest
        {
            Email = "new@test.com",
            Password = "Passw0rd!",
            FullName = "New User",
            Role = "Manager"
        };
    }

    [Fact]
    public async Task RegisterAsync_WhenRoleDoesNotExist_ReturnsInvalidRole()
    {
        _roleManager.RoleExistsAsync("Manager").Returns(false);

        var result = await CreateService().RegisterAsync(ValidRegisterRequest());

        result.FirstError.ShouldBe(Errors.Auth.InvalidRole);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailExists_ReturnsEmailAlreadyExists()
    {
        _roleManager.RoleExistsAsync("Manager").Returns(true);
        _userManager.FindByEmailAsync("new@test.com").Returns(new ApplicationUser { Email = "new@test.com" });

        var result = await CreateService().RegisterAsync(ValidRegisterRequest());

        result.FirstError.ShouldBe(Errors.Auth.EmailAlreadyExists);
    }

    [Fact]
    public async Task RegisterAsync_WhenValid_CreatesUserAssignsRoleAndReturnsToken()
    {
        _roleManager.RoleExistsAsync("Manager").Returns(true);
        _userManager.FindByEmailAsync("new@test.com").Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), "Passw0rd!").Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), "Manager").Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string> { "Manager" });
        _tokenGenerator.GenerateAccessToken(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new AccessToken("token-123", DateTime.UtcNow.AddMinutes(30)));

        var result = await CreateService().RegisterAsync(ValidRegisterRequest());

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("token-123");
        result.Value.Roles.ShouldContain("Manager");
        await _userManager.Received(1).AddToRoleAsync(Arg.Any<ApplicationUser>(), "Manager");
    }

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsInvalidCredentials()
    {
        _userManager.FindByEmailAsync("missing@test.com").Returns((ApplicationUser?)null);

        var result = await CreateService().LoginAsync(new LoginRequest { Email = "missing@test.com", Password = "x" });

        result.FirstError.ShouldBe(Errors.Auth.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordInvalid_ReturnsInvalidCredentials()
    {
        var user = new ApplicationUser { Email = "user@test.com" };
        _userManager.FindByEmailAsync("user@test.com").Returns(user);
        _userManager.CheckPasswordAsync(user, "wrong").Returns(false);

        var result = await CreateService().LoginAsync(new LoginRequest { Email = "user@test.com", Password = "wrong" });

        result.FirstError.ShouldBe(Errors.Auth.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_WhenValid_ReturnsAuthResponseWithToken()
    {
        var user = new ApplicationUser { Email = "user@test.com" };
        _userManager.FindByEmailAsync("user@test.com").Returns(user);
        _userManager.CheckPasswordAsync(user, "Passw0rd!").Returns(true);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });
        _tokenGenerator.GenerateAccessToken(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new AccessToken("token-abc", DateTime.UtcNow.AddMinutes(30)));

        var result = await CreateService().LoginAsync(new LoginRequest { Email = "user@test.com", Password = "Passw0rd!" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("token-abc");
        result.Value.Roles.ShouldContain("Admin");
    }

    [Fact]
    public async Task LoginAsync_WhenAccountIsLockedOut_ReturnsAccountLockedWithoutCheckingThePassword()
    {
        var user = new ApplicationUser { Email = "user@test.com" };
        _userManager.FindByEmailAsync("user@test.com").Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(true);

        var result = await CreateService().LoginAsync(new LoginRequest { Email = "user@test.com", Password = "Passw0rd!" });

        result.FirstError.ShouldBe(Errors.Auth.AccountLocked);
        await _userManager.DidNotReceive().CheckPasswordAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordInvalid_RecordsTheFailedAttempt()
    {
        var user = new ApplicationUser { Email = "user@test.com" };
        _userManager.FindByEmailAsync("user@test.com").Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(false);
        _userManager.CheckPasswordAsync(user, "wrong").Returns(false);

        var result = await CreateService().LoginAsync(new LoginRequest { Email = "user@test.com", Password = "wrong" });

        result.FirstError.ShouldBe(Errors.Auth.InvalidCredentials);
        await _userManager.Received(1).AccessFailedAsync(user);
    }

    [Fact]
    public async Task LoginAsync_WhenTheFailedAttemptTripsTheLockout_ReturnsAccountLocked()
    {
        var user = new ApplicationUser { Email = "user@test.com" };
        _userManager.FindByEmailAsync("user@test.com").Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(false, true);
        _userManager.CheckPasswordAsync(user, "wrong").Returns(false);

        var result = await CreateService().LoginAsync(new LoginRequest { Email = "user@test.com", Password = "wrong" });

        result.FirstError.ShouldBe(Errors.Auth.AccountLocked);
    }

    [Fact]
    public async Task LoginAsync_WhenValid_ResetsTheFailedAttemptCount()
    {
        var user = new ApplicationUser { Email = "user@test.com" };
        _userManager.FindByEmailAsync("user@test.com").Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(false);
        _userManager.CheckPasswordAsync(user, "Passw0rd!").Returns(true);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });
        _tokenGenerator.GenerateAccessToken(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new AccessToken("token-abc", DateTime.UtcNow.AddMinutes(30)));

        var result = await CreateService().LoginAsync(new LoginRequest { Email = "user@test.com", Password = "Passw0rd!" });

        result.IsSuccess.ShouldBeTrue();
        await _userManager.Received(1).ResetAccessFailedCountAsync(user);
    }
}

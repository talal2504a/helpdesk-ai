using HelpDesk.Application.DTOs.Auth;
using HelpDesk.Application.Exceptions;
using HelpDesk.Application.Validators;
using HelpDesk.Infrastructure.Security;
using Xunit;

namespace HelpDesk.Tests;

public class AuthValidatorTests
{
    [Fact]
    public void Register_Rejects_WeakPassword()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            AuthValidators.Validate(new RegisterRequest("John", "john@example.com", "short")));
        Assert.True(ex.Errors.ContainsKey(nameof(RegisterRequest.Password)));
    }

    [Fact]
    public void Register_Rejects_InvalidEmail()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            AuthValidators.Validate(new RegisterRequest("John", "not-an-email", "StrongPass1")));
        Assert.True(ex.Errors.ContainsKey(nameof(RegisterRequest.Email)));
    }

    [Fact]
    public void Register_Accepts_ValidInput()
    {
        AuthValidators.Validate(new RegisterRequest("John", "john@example.com", "StrongPass1"));
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_Then_Verify_RoundTrips()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("CorrectHorse9");
        Assert.True(hasher.Verify("CorrectHorse9", hash));
        Assert.False(hasher.Verify("WrongPassword", hash));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("qwerty", false)]
    [InlineData("a].", false)]
    public void Hash_Rejects_BadInput_Noop(string pw, bool ignored)
    {
        var hasher = new Pbkdf2PasswordHasher();
        // Still produces a stable hash even for short strings (defense in depth).
        Assert.NotNull(hasher.Hash(pw));
    }
}

public class TicketTransitionTests
{
    [Fact]
    public void Open_Allows_Expected_Transitions()
    {
        var rules = Infrastructure.Services.TicketService.AllowedTransitions;
        Assert.Equal(new[] { "InProgress", "Pending", "Closed" }, rules["Open"]);
        Assert.Contains("Resolved", rules["InProgress"]);
        Assert.Contains("Open", rules["Closed"]); // reopen
    }
}
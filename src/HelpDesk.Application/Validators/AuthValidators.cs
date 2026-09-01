using HelpDesk.Application.DTOs.Auth;
using HelpDesk.Application.Exceptions;

namespace HelpDesk.Application.Validators;

/// <summary>Lightweight FluentValidation-style validators (no external dependency).</summary>
public static class AuthValidators
{
    public static void Validate(RegisterRequest r)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length < 2 || r.Name.Length > 100)
            Add(errors, nameof(r.Name), "Name must be between 2 and 100 characters.");

        if (string.IsNullOrWhiteSpace(r.Email) || !IsValidEmail(r.Email))
            Add(errors, nameof(r.Email), "A valid email address is required.");

        if (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 8)
            Add(errors, nameof(r.Password), "Password must be at least 8 characters long.");
        else if (!r.Password.Any(char.IsDigit))
            Add(errors, nameof(r.Password), "Password must contain at least one digit.");
        else if (!r.Password.Any(char.IsLetter))
            Add(errors, nameof(r.Password), "Password must contain at least one letter.");

        if (errors.Count > 0) throw new ValidationException(errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
    }

    public static void Validate(LoginRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Email)) throw new ValidationException(nameof(r.Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(r.Password)) throw new ValidationException(nameof(r.Password), "Password is required.");
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list)) errors[key] = list = new();
        list.Add(message);
    }

    public static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch { return false; }
    }
}
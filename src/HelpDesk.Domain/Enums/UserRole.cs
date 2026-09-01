using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Domain.Enums;

public enum UserRole
{
    [Display(Name = "Customer")] Customer = 1,
    [Display(Name = "Agent")] Agent = 2,
    [Display(Name = "Admin")] Admin = 3
}

public static class UserRoleExtensions
{
    public const string CustomerRole = "Customer";
    public const string AgentRole = "Agent";
    public const string AdminRole = "Admin";

    public static string ToRoleName(this UserRole role) => role switch
    {
        UserRole.Agent => AgentRole,
        UserRole.Admin => AdminRole,
        _ => CustomerRole
    };
}
using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Application.DTOs.Tickets;
using HelpDesk.Application.Exceptions;

namespace HelpDesk.Application.Validators;

public static class TicketValidators
{
    public static void Validate(CreateTicketRequest r)
    {
        var errors = new Dictionary<string, List<string>>();
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length < 5 || r.Title.Length > 200)
            errors[nameof(r.Title)] = new() { "Title must be between 5 and 200 characters." };
        if (string.IsNullOrWhiteSpace(r.Description) || r.Description.Trim().Length < 10 || r.Description.Length > 8000)
            errors[nameof(r.Description)] = new() { "Description must be between 10 and 8000 characters." };
        if (r.CategoryId is <= 0) errors[nameof(r.CategoryId)] = new() { "CategoryId must be a positive id or omitted for AI detection." };
        if (r.PriorityId is <= 0) errors[nameof(r.PriorityId)] = new() { "PriorityId must be a positive id or omitted for AI detection." };
        if (r.DepartmentId is <= 0) errors[nameof(r.DepartmentId)] = new() { "DepartmentId must be a positive id or omitted." };
        if (errors.Count > 0) throw new ValidationException(errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
    }

    public static void Validate(SendMessageRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Body) || r.Body.Trim().Length < 1 || r.Body.Length > 5000)
            throw new ValidationException(nameof(r.Body), "Message body must be between 1 and 5000 characters.");
    }
}
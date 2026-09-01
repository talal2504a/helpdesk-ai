namespace HelpDesk.Application.DTOs.Admin;

public record CategoryLookupDto(int Id, string Name);
public record PriorityLookupDto(int Id, string Name, int Level);
public record StatusLookupDto(int Id, string Name);
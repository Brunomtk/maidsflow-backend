using System.Collections.Generic;
﻿using Core.Enums;

public class AuthUserResponse
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Role { get; set; }
    public string? Avatar { get; set; }
    public StatusEnum Status { get; set; }

    public string Token { get; set; } = string.Empty;

    public int? CompanyId { get; set; }
    public int? ProfessionalId { get; set; }

    public string? Language { get; set; }
    public string? Theme { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    public List<string> Permissions { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;

namespace SmartField.Api.Authentication;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

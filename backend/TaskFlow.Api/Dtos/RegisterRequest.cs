using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// Model binding turns the incoming JSON body into this DTO, and [ApiController] (see
// AuthController) runs these data annotations automatically before the action method
// even executes — an invalid request short-circuits to a 400 ValidationProblemDetails.
public class RegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public required string FullName { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [RegularExpression(
        @"^(?=.*[A-Za-z])(?=.*\d).{8,}$",
        ErrorMessage = "Password must be at least 8 characters and include at least one letter and one number.")]
    public required string Password { get; set; }
}

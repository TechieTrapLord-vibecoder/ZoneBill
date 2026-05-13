using System.ComponentModel.DataAnnotations;

namespace ZoneBill_Lloren.Models
{
    public class RegisterBusinessViewModel
    {
        [Required, Display(Name = "Business Name")]
        public string BusinessName { get; set; } = string.Empty;

        [Required]
        public int PlanId { get; set; }

        [Required, Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, Display(Name = "Email Address")]
        public string EmailAddress { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [MinLength(12, ErrorMessage = "The {0} must be at least {1} characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{12,}$", ErrorMessage = "Password must be at least 12 characters and contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
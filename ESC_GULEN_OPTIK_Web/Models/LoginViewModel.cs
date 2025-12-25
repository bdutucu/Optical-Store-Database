using System.ComponentModel.DataAnnotations;

namespace ESC_GULEN_OPTIK_Web.Models
{
    /// <summary>
    /// Login form model
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// Logged in user session data
    /// </summary>
    public class UserSession
    {
        public int StaffID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Position { get; set; } = "";
        public string Email { get; set; } = "";

        public string FullName => $"{FirstName} {LastName}";
        
        /// <summary>
        /// Check if user is Admin (Manager position)
        /// </summary>
        public bool IsAdmin => Position?.ToUpper() == "MANAGER" || Position?.ToUpper() == "ADMIN";
    }
}

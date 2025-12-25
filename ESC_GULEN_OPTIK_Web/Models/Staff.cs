using System.ComponentModel.DataAnnotations;

namespace ESC_GULEN_OPTIK_Web.Models
{
    /// <summary>
    /// Staff entity model - Maps to Staff table
    /// </summary>
    public class Staff
    {
        public int StaffID { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = "";

        [Required]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public decimal Salary { get; set; }

        public string? Position { get; set; }

        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        public int? Age { get; set; }  // Computed column

        [Display(Name = "Phone")]
        public string? PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Job Start Date")]
        public DateTime JobStartDate { get; set; }

        [Display(Name = "Years of Experience")]
        public int? YearsOfExperience { get; set; }  // Computed column

        // Helper
        public string FullName => $"{FirstName} {LastName}";

        // For login credentials (not stored in Staff table)
        [Display(Name = "Password")]
        [StringLength(100, MinimumLength = 4, ErrorMessage = "Password must be at least 4 characters")]
        public string? Password { get; set; }

        // For display - whether user has credentials
        public bool HasCredentials { get; set; }
    }
}


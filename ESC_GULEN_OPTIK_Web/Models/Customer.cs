using System.ComponentModel.DataAnnotations;

namespace ESC_GULEN_OPTIK_Web.Models
{
    /// <summary>
    /// Customer entity model - Maps to Customer table
    /// </summary>
    public class Customer
    {
        public int CustomerID { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "National ID")]
        public string NationalID { get; set; } = "";

        [Required]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = "";

        [Required]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = "";

        [EmailAddress]
        [Display(Name = "Email")]
        public string? MailAddress { get; set; }

        [Display(Name = "Insurance Info")]
        public string? InsuranceInfo { get; set; }

        [Required]
        [Display(Name = "Registered By")]
        public int RegisteredByStaffID { get; set; }

        // Navigation property for display
        public string? StaffName { get; set; }

        // Helper
        public string FullName => $"{FirstName} {LastName}";
    }
}


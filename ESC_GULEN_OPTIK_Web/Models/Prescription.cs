using System.ComponentModel.DataAnnotations;

namespace ESC_GULEN_OPTIK_Web.Models
{
    /// <summary>
    /// Prescription (Reçete) entity model - Maps to Prescription table
    /// </summary>
    public class Prescription
    {
        public int PrescriptionID { get; set; }

        [Required]
        [Display(Name = "Prescription Date")]
        [DataType(DataType.Date)]
        public DateTime DateOfPrescription { get; set; } = DateTime.Today;

        [StringLength(100)]
        [Display(Name = "Doctor Name")]
        public string? DoctorName { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int CustomerID { get; set; }

        [Display(Name = "Examined By Staff")]
        public int? StaffID { get; set; }

        // ===== RIGHT EYE MEASUREMENTS =====
        [Display(Name = "Right SPH")]
        [Range(-20.00, 20.00)]
        public decimal? Right_SPH { get; set; }

        [Display(Name = "Right CYL")]
        [Range(-10.00, 10.00)]
        public decimal? Right_CYL { get; set; }

        [Display(Name = "Right Axis")]
        [Range(0, 180)]
        public int? Right_AX { get; set; }

        // ===== LEFT EYE MEASUREMENTS =====
        [Display(Name = "Left SPH")]
        [Range(-20.00, 20.00)]
        public decimal? Left_SPH { get; set; }

        [Display(Name = "Left CYL")]
        [Range(-10.00, 10.00)]
        public decimal? Left_CYL { get; set; }

        [Display(Name = "Left Axis")]
        [Range(0, 180)]
        public int? Left_AX { get; set; }

        // Navigation properties for display
        public string? CustomerName { get; set; }
        public string? StaffName { get; set; }

        // Helper for display
        public string RightEyeDisplay => Right_SPH.HasValue 
            ? $"SPH: {Right_SPH:+0.00;-0.00} | CYL: {Right_CYL:+0.00;-0.00} | AX: {Right_AX}°" 
            : "Not specified";
        
        public string LeftEyeDisplay => Left_SPH.HasValue 
            ? $"SPH: {Left_SPH:+0.00;-0.00} | CYL: {Left_CYL:+0.00;-0.00} | AX: {Left_AX}°" 
            : "Not specified";
    }
}


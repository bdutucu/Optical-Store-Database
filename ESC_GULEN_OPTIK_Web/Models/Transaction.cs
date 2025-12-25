using System.ComponentModel.DataAnnotations;

namespace ESC_GULEN_OPTIK_Web.Models
{
    /// <summary>
    /// Transaction model (Sale or Repair)
    /// </summary>
    public class Transaction
    {
        public int TransactionID { get; set; }

        [Required(ErrorMessage = "Customer is required")]
        [Display(Name = "Customer")]
        public int CustomerID { get; set; }

        [Required(ErrorMessage = "Staff is required")]
        [Display(Name = "Staff")]
        public int StaffID { get; set; }

        [Display(Name = "Transaction Date")]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Remaining Balance")]
        [DataType(DataType.Currency)]
        public decimal RemainingBalance { get; set; }

        [Required(ErrorMessage = "Transaction type is required")]
        [Display(Name = "Transaction Type")]
        public int TransactionTypeID { get; set; }

        // Navigation properties for display
        public string? CustomerName { get; set; }
        public string? StaffName { get; set; }
        public string? TransactionTypeName { get; set; }
    }

    /// <summary>
    /// Sale Item model (products in a sale)
    /// </summary>
    public class SaleItem
    {
        public int TransactionID { get; set; }

        [Required(ErrorMessage = "Product is required")]
        [Display(Name = "Product")]
        public int ProductID { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Tax Rate")]
        public decimal TaxRate { get; set; } = 20.00m;

        [Display(Name = "Subtotal")]
        [DataType(DataType.Currency)]
        public decimal SubTotal { get; set; }

        [Display(Name = "Tax Amount")]
        [DataType(DataType.Currency)]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Line Total")]
        [DataType(DataType.Currency)]
        public decimal LineTotal { get; set; }

        public int? PrescriptionID { get; set; }

        // Navigation for display
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
    }

    /// <summary>
    /// Payment model with Cash/CreditCard subtype support
    /// </summary>
    public class Payment
    {
        public int PaymentID { get; set; }

        [Required(ErrorMessage = "Transaction is required")]
        public int TransactionID { get; set; }

        [Display(Name = "Payment Date")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, 1000000, ErrorMessage = "Amount must be greater than 0")]
        [Display(Name = "Amount Paid")]
        [DataType(DataType.Currency)]
        public decimal AmountPaid { get; set; }

        [Required(ErrorMessage = "Payment type is required")]
        [Display(Name = "Payment Type")]
        public string PaymentType { get; set; } = "Cash";
        
        // ===== SUBTYPE FIELDS =====
        
        // Cash specific
        [Display(Name = "Received By")]
        [StringLength(50)]
        public string? ReceivedBy { get; set; }
        
        // CreditCard specific
        [Display(Name = "Card Owner Name")]
        [StringLength(100)]
        public string? CardOwner { get; set; }
    }

    /// <summary>
    /// Repair Transaction model
    /// </summary>
    public class RepairTransaction
    {
        public int TransactionID { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Estimated Completion")]
        public DateTime? EstimatedCompletion { get; set; }
    }

    /// <summary>
    /// View model for creating a new sale
    /// </summary>
    public class CreateSaleViewModel
    {
        [Required(ErrorMessage = "Customer is required")]
        [Display(Name = "Customer")]
        public int CustomerID { get; set; }

        public List<SaleItemInput> Items { get; set; } = new();
    }

    /// <summary>
    /// Input model for adding items to sale
    /// </summary>
    public class SaleItemInput
    {
        [Required]
        public int ProductID { get; set; }
        
        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; } = 1;
        
        public int? PrescriptionID { get; set; }
    }

    /// <summary>
    /// View model for creating a repair
    /// </summary>
    public class CreateRepairViewModel
    {
        [Required(ErrorMessage = "Customer is required")]
        [Display(Name = "Customer")]
        public int CustomerID { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [Display(Name = "Repair Description")]
        public string Description { get; set; } = "";

        [Required(ErrorMessage = "Repair cost is required")]
        [Range(0.01, 100000, ErrorMessage = "Cost must be greater than 0")]
        [Display(Name = "Repair Cost")]
        [DataType(DataType.Currency)]
        public decimal RepairCost { get; set; }

        [Display(Name = "Estimated Completion Date")]
        [DataType(DataType.Date)]
        public DateTime? EstimatedCompletion { get; set; }
    }
}


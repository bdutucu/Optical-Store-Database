using System.ComponentModel.DataAnnotations;

namespace ESC_GULEN_OPTIK_Web.Models
{
    /// <summary>
    /// Product entity model - Maps to Product table
    /// Database schema: ProductID, Brand, StockQuantity, Price, ProductTypeID
    /// Note: ProductType string comes from view_ProductCatalog (JOIN with ProductTypes)
    /// </summary>
    public class Product
    {
        public int ProductID { get; set; }

        [Required]
        [StringLength(50)]
        public string Brand { get; set; } = "";

        [Display(Name = "Stock")]
        public int StockQuantity { get; set; }

        public decimal? Price { get; set; }

        [Required]
        [Display(Name = "Product Type")]
        public int ProductTypeID { get; set; }

        // From view_ProductCatalog (read-only, not in Product table)
        [Display(Name = "Type")]
        public string? ProductType { get; set; }
        
        public string? ModelOrSerial { get; set; }
        public string? ColorInfo { get; set; }
    }
}


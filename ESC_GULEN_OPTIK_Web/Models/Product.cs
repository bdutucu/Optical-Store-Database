using System.ComponentModel.DataAnnotations;

namespace ESC_GULEN_OPTIK_Web.Models
{
    /// <summary>
    /// Product entity model - Maps to Product table with Subtype support
    /// Database schema: ProductID, Brand, StockQuantity, Price, ProductTypeID
    /// Subtypes: Frames, Sunglasses, ContactLenses, Lenses
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
        
        // ===== SUBTYPE FIELDS =====
        
        [Display(Name = "Model/Serial No")]
        [StringLength(100)]
        public string? ModelOrSerial { get; set; }
        
        [Display(Name = "Color")]
        [StringLength(50)]
        public string? ColorInfo { get; set; }
        
        // Sunglasses specific
        [Display(Name = "Size")]
        [StringLength(20)]
        public string? Size { get; set; }
        
        // ContactLens/Lens specific
        [Display(Name = "Lens Type")]
        [StringLength(50)]
        public string? LensType { get; set; }
        
        // Eye Measurements (ContactLens & Lens)
        [Display(Name = "Right SPH")]
        public decimal? Right_SPH { get; set; }
        
        [Display(Name = "Right CYL")]
        public decimal? Right_CYL { get; set; }
        
        [Display(Name = "Right Axis")]
        public int? Right_AX { get; set; }
        
        [Display(Name = "Left SPH")]
        public decimal? Left_SPH { get; set; }
        
        [Display(Name = "Left CYL")]
        public decimal? Left_CYL { get; set; }
        
        [Display(Name = "Left Axis")]
        public int? Left_AX { get; set; }
        
        // ===== MATERIALS (Çoklu - sadece FRAME ve SUNGLASSES için) =====
        [Display(Name = "Materials")]
        public List<int>? MaterialIDs { get; set; }
        
        [Display(Name = "Component Parts")]
        public List<string>? MaterialParts { get; set; }
        
        // Mevcut materials listesi (Edit için)
        public List<ProductMaterial>? CurrentMaterials { get; set; }
    }
    
    /// <summary>
    /// Product Material model for display
    /// </summary>
    public class ProductMaterial
    {
        public int MaterialID { get; set; }
        public string MaterialName { get; set; } = "";
        public string? ComponentPart { get; set; }
    }
}


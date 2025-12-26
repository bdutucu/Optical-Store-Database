using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using ESC_GULEN_OPTIK_Web.Filters;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Product Controller - CRUD operations for Product
    /// Includes Stored Procedure usage examples
    /// Accessible by all logged-in staff
    /// </summary>
    [ServiceFilter(typeof(AuthenticationFilter))]
    public class ProductController : Controller
    {
        private readonly DBConnection _dbcon;

        public ProductController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// List all products using view with filtering and sorting
        /// GET: /Product
        /// </summary>
        public IActionResult Index(string? type, string? brand, string? sortBy, string? sortOrder)
        {
            string baseQuery = @"SELECT ProductID, Brand, ProductType, Price, StockQuantity, 
                                ModelOrSerial, ColorInfo FROM view_ProductCatalog WHERE 1=1";
            
            var parameters = new List<(string, object)>();
            
            // Filter by type
            if (!string.IsNullOrEmpty(type))
            {
                baseQuery += " AND ProductType = @type";
                parameters.Add(("@type", type));
                ViewBag.SelectedType = type;
            }
            
            // Filter by brand
            if (!string.IsNullOrEmpty(brand))
            {
                baseQuery += " AND Brand LIKE @brand";
                parameters.Add(("@brand", $"%{brand}%"));
                ViewBag.SelectedBrand = brand;
            }
            
            // Sorting
            string orderBy = sortBy?.ToLower() switch
            {
                "brand" => "Brand",
                "price" => "Price",
                "stock" => "StockQuantity",
                "type" => "ProductType",
                _ => "Brand"
            };
            
            string order = sortOrder?.ToUpper() == "DESC" ? "DESC" : "ASC";
            baseQuery += $" ORDER BY {orderBy} {order}";
            
            ViewBag.CurrentSort = sortBy;
            ViewBag.CurrentOrder = sortOrder;

            DataSet ds;
            if (parameters.Count > 0)
            {
                ds = _dbcon.getSelectWithParams(baseQuery, parameters.ToArray());
            }
            else
            {
                ds = _dbcon.getSelect(baseQuery);
            }

            var products = new List<Product>();
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                products.Add(MapRowToProduct(row));
            }

            ViewBag.ProductTypes = new[] { "FRAME", "SUNGLASSES", "CONTACTLENS", "LENS" };
            return View(products);
        }

        /// <summary>
        /// Search products using STORED PROCEDURE
        /// GET: /Product/Search
        /// This demonstrates stored procedure usage like instructor's Button3_Click
        /// </summary>
        public IActionResult Search(string? category, string? color, string? material, decimal? minPrice, decimal? maxPrice)
        {
            // Using stored procedure (like instructor's pattern with CommandType.StoredProcedure)
            DataSet ds = _dbcon.getStoredProcedure("proc_SearchProducts",
                ("@ProductCategory", category ?? (object)DBNull.Value),
                ("@ColorCode", color ?? (object)DBNull.Value),
                ("@MaterialName", material ?? (object)DBNull.Value),
                ("@MinPrice", minPrice ?? (object)DBNull.Value),
                ("@MaxPrice", maxPrice ?? (object)DBNull.Value)
            );

            var products = new List<Product>();
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                products.Add(new Product
                {
                    ProductID = Convert.ToInt32(row["ProductID"]),
                    Brand = row["Brand"].ToString() ?? "",
                    ProductType = row["ProductType"].ToString() ?? "",
                    StockQuantity = Convert.ToInt32(row["StockQuantity"]),
                    Price = row["Price"] == DBNull.Value ? null : Convert.ToDecimal(row["Price"]),
                    ModelOrSerial = row["ModelOrSerial"] == DBNull.Value ? null : row["ModelOrSerial"].ToString(),
                    ColorInfo = row["ColorInfo"] == DBNull.Value ? null : row["ColorInfo"].ToString()
                });
            }

            ViewBag.ProductTypes = new[] { "FRAME", "SUNGLASSES", "CONTACTLENS", "LENS" };
            ViewBag.SearchPerformed = true;
            ViewBag.Category = category;
            ViewBag.Color = color;
            ViewBag.Material = material;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            return View("Index", products);
        }

        /// <summary>
        /// Show details
        /// GET: /Product/Details/5
        /// </summary>
        public IActionResult Details(int id)
        {
            string sqlstr = "SELECT * FROM Product WHERE ProductID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(MapRowToProductBasic(ds.Tables[0].Rows[0]));
        }

        /// <summary>
        /// Show create form
        /// GET: /Product/Create
        /// </summary>
        public IActionResult Create()
        {
            LoadDropdowns();
            return View(new Product());
        }

        /// <summary>
        /// Handle create
        /// POST: /Product/Create
        /// Uses proc_AddProduct stored procedure with subtype support
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product, int[]? SelectedMaterials, string[]? MaterialPartNames)
        {
            // Remove validation for subtype fields that may not apply
            ModelState.Remove("ModelOrSerial");
            ModelState.Remove("ColorInfo");
            ModelState.Remove("Size");
            ModelState.Remove("LensType");
            ModelState.Remove("SelectedMaterials");
            ModelState.Remove("MaterialPartNames");
            
            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(product);
            }

            try
            {
                // Use proc_AddProduct stored procedure with OUTPUT parameter
                // Material is added separately for multiple materials
                // Note: Eye measurements removed - now stored in Prescription table
                int newId = _dbcon.executeStoredProcedureWithOutput(
                    "proc_AddProduct",
                    "@NewProductID",
                    ("@Brand", product.Brand),
                    ("@StockQuantity", product.StockQuantity),
                    ("@Price", product.Price ?? (object)DBNull.Value),
                    ("@ProductTypeID", product.ProductTypeID),
                    ("@ModelOrSerial", (object?)product.ModelOrSerial ?? DBNull.Value),
                    ("@ColourCode", (object?)product.ColorInfo ?? DBNull.Value),
                    ("@Size", (object?)product.Size ?? DBNull.Value),
                    ("@LensType", (object?)product.LensType ?? DBNull.Value),
                    ("@MaterialID", DBNull.Value),  // Will add materials separately
                    ("@MaterialPart", DBNull.Value)
                );

                if (newId > 0)
                {
                    // Add multiple materials (only for FRAME=1 and SUNGLASSES=2)
                    if (SelectedMaterials != null && SelectedMaterials.Length > 0 && product.ProductTypeID <= 2)
                    {
                        string materialIds = string.Join(",", SelectedMaterials);
                        string? partNames = MaterialPartNames != null ? string.Join(",", MaterialPartNames) : null;
                        
                        _dbcon.getStoredProcedure("proc_AddMultipleProductMaterials",
                            ("@ProductID", newId),
                            ("@MaterialIDs", materialIds),
                            ("@ComponentParts", (object?)partNames ?? DBNull.Value)
                        );
                    }
                    
                    TempData["Success"] = $"Product created with ID: {newId}";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Error"] = "Failed to create product.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating product: " + ex.Message;
            }

            LoadDropdowns();
            return View(product);
        }

        /// <summary>
        /// Show edit form
        /// GET: /Product/Edit/5
        /// Uses proc_GetProductDetails to get full product with subtype
        /// </summary>
        public IActionResult Edit(int id)
        {
            // Use stored procedure to get full product details including subtype
            DataSet ds = _dbcon.getStoredProcedure("proc_GetProductDetails", ("@ProductID", id));

            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            DataRow row = ds.Tables[0].Rows[0];
            var product = new Product
            {
                ProductID = Convert.ToInt32(row["ProductID"]),
                Brand = row["Brand"].ToString() ?? "",
                StockQuantity = Convert.ToInt32(row["StockQuantity"]),
                Price = row["Price"] == DBNull.Value ? null : Convert.ToDecimal(row["Price"]),
                ProductTypeID = Convert.ToInt32(row["ProductTypeID"]),
                ProductType = row["ProductType"].ToString(),
                ModelOrSerial = row["ModelOrSerial"] == DBNull.Value ? null : row["ModelOrSerial"].ToString(),
                ColorInfo = row["ColourCode"] == DBNull.Value ? null : row["ColourCode"].ToString(),
                Size = row["Size"] == DBNull.Value ? null : row["Size"].ToString(),
                LensType = row["LensType"] == DBNull.Value ? null : row["LensType"].ToString(),
                // Note: Eye measurements removed from Product - now in Prescription table
                CurrentMaterials = new List<ProductMaterial>()
            };

            // Get ALL materials if available (second result set) - multiple materials support
            if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
            {
                product.MaterialIDs = new List<int>();
                product.MaterialParts = new List<string>();
                
                foreach (DataRow matRow in ds.Tables[1].Rows)
                {
                    int matId = Convert.ToInt32(matRow["MaterialID"]);
                    product.MaterialIDs.Add(matId);
                    product.MaterialParts.Add(matRow["ComponentPart"] == DBNull.Value ? "" : matRow["ComponentPart"].ToString() ?? "");
                    
                    product.CurrentMaterials.Add(new ProductMaterial
                    {
                        MaterialID = matId,
                        MaterialName = matRow["MaterialName"].ToString() ?? "",
                        ComponentPart = matRow["ComponentPart"] == DBNull.Value ? null : matRow["ComponentPart"].ToString()
                    });
                }
            }

            LoadDropdowns();
            return View(product);
        }

        /// <summary>
        /// Handle edit
        /// POST: /Product/Edit/5
        /// Uses proc_UpdateProduct stored procedure with subtype support
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Product product, int[]? SelectedMaterials, string[]? MaterialPartNames)
        {
            if (id != product.ProductID)
                return BadRequest();

            // Remove validation for subtype fields that may not apply
            ModelState.Remove("ModelOrSerial");
            ModelState.Remove("ColorInfo");
            ModelState.Remove("Size");
            ModelState.Remove("LensType");
            ModelState.Remove("SelectedMaterials");
            ModelState.Remove("MaterialPartNames");

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(product);
            }

            try
            {
                // Use proc_UpdateProduct stored procedure
                // Note: Eye measurements removed - now stored in Prescription table
                _dbcon.getStoredProcedure("proc_UpdateProduct",
                    ("@ProductID", product.ProductID),
                    ("@Brand", product.Brand),
                    ("@StockQuantity", product.StockQuantity),
                    ("@Price", product.Price ?? (object)DBNull.Value),
                    ("@ProductTypeID", product.ProductTypeID),
                    ("@ModelOrSerial", (object?)product.ModelOrSerial ?? DBNull.Value),
                    ("@ColourCode", (object?)product.ColorInfo ?? DBNull.Value),
                    ("@Size", (object?)product.Size ?? DBNull.Value),
                    ("@LensType", (object?)product.LensType ?? DBNull.Value)
                );

                // Update materials (only for FRAME=1 and SUNGLASSES=2)
                if (product.ProductTypeID <= 2)
                {
                    if (SelectedMaterials != null && SelectedMaterials.Length > 0)
                    {
                        string materialIds = string.Join(",", SelectedMaterials);
                        string? partNames = MaterialPartNames != null ? string.Join(",", MaterialPartNames) : null;
                        
                        _dbcon.getStoredProcedure("proc_AddMultipleProductMaterials",
                            ("@ProductID", product.ProductID),
                            ("@MaterialIDs", materialIds),
                            ("@ComponentParts", (object?)partNames ?? DBNull.Value)
                        );
                    }
                    else
                    {
                        // No materials selected - clear existing
                        _dbcon.executeWithParams("DELETE FROM ProductMaterials WHERE ProductID = @id", ("@id", product.ProductID));
                    }
                }

                TempData["Success"] = "Product updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating product: " + ex.Message;
            }

            LoadDropdowns();
            return View(product);
        }

        /// <summary>
        /// Show delete confirmation
        /// GET: /Product/Delete/5
        /// </summary>
        public IActionResult Delete(int id)
        {
            string sqlstr = "SELECT * FROM Product WHERE ProductID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(MapRowToProductBasic(ds.Tables[0].Rows[0]));
        }

        /// <summary>
        /// Handle delete
        /// POST: /Product/Delete/5
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            string sqlstr = "DELETE FROM Product WHERE ProductID = @id";
            bool success = _dbcon.executeWithParams(sqlstr, ("@id", id));

            if (success)
                TempData["Success"] = "Product deleted successfully.";
            else
                TempData["Error"] = "Failed to delete product.";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Load ProductTypes and Materials from database for dropdowns
        /// </summary>
        private void LoadDropdowns()
        {
            // Get ProductTypes from database
            string sqlstr = "SELECT ProductTypeID, TypeName FROM ProductTypes ORDER BY ProductTypeID";
            DataSet ds = _dbcon.getSelect(sqlstr);

            var typeList = new List<SelectListItem>();
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                typeList.Add(new SelectListItem
                {
                    Value = row["ProductTypeID"].ToString(),
                    Text = row["TypeName"].ToString()
                });
            }

            ViewBag.ProductTypeIds = typeList;
            
            // Get Materials from database
            string matSql = "SELECT MaterialID, MaterialName FROM Materials ORDER BY MaterialName";
            DataSet dsMat = _dbcon.getSelect(matSql);

            var matList = new List<SelectListItem>();
            foreach (DataRow row in dsMat.Tables[0].Rows)
            {
                matList.Add(new SelectListItem
                {
                    Value = row["MaterialID"].ToString(),
                    Text = row["MaterialName"].ToString()
                });
            }

            ViewBag.Materials = matList;
        }

        private Product MapRowToProduct(DataRow row)
        {
            return new Product
            {
                ProductID = Convert.ToInt32(row["ProductID"]),
                Brand = row["Brand"].ToString() ?? "",
                ProductType = row["ProductType"].ToString() ?? "",
                StockQuantity = Convert.ToInt32(row["StockQuantity"]),
                Price = row["Price"] == DBNull.Value ? null : Convert.ToDecimal(row["Price"]),
                ModelOrSerial = row.Table.Columns.Contains("ModelOrSerial") && row["ModelOrSerial"] != DBNull.Value 
                    ? row["ModelOrSerial"].ToString() : null,
                ColorInfo = row.Table.Columns.Contains("ColorInfo") && row["ColorInfo"] != DBNull.Value 
                    ? row["ColorInfo"].ToString() : null
            };
        }

        /// <summary>
        /// Maps DataRow from Product table (without view columns)
        /// </summary>
        private Product MapRowToProductBasic(DataRow row)
        {
            return new Product
            {
                ProductID = Convert.ToInt32(row["ProductID"]),
                Brand = row["Brand"].ToString() ?? "",
                StockQuantity = Convert.ToInt32(row["StockQuantity"]),
                Price = row["Price"] == DBNull.Value ? null : Convert.ToDecimal(row["Price"]),
                ProductTypeID = Convert.ToInt32(row["ProductTypeID"])
            };
        }
    }
}


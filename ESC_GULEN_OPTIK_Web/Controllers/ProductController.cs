using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Product Controller - CRUD operations for Product
    /// Includes Stored Procedure usage examples
    /// </summary>
    public class ProductController : Controller
    {
        private readonly DBConnection _dbcon;

        public ProductController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// List all products using view
        /// GET: /Product
        /// </summary>
        public IActionResult Index(string? type)
        {
            string sqlstr;
            DataSet ds;

            if (!string.IsNullOrEmpty(type))
            {
                // Filtered by type
                sqlstr = "SELECT ProductID, Brand, ProductType, Price, StockQuantity, " +
                        "ModelOrSerial, ColorInfo FROM view_ProductCatalog " +
                        "WHERE ProductType = @type ORDER BY Brand";
                ds = _dbcon.getSelectWithParams(sqlstr, ("@type", type));
                ViewBag.SelectedType = type;
            }
            else
            {
                // All products
                sqlstr = "SELECT ProductID, Brand, ProductType, Price, StockQuantity, " +
                        "ModelOrSerial, ColorInfo FROM view_ProductCatalog ORDER BY Brand";
                ds = _dbcon.getSelect(sqlstr);
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
        public IActionResult Search(string? category, decimal? minPrice, decimal? maxPrice)
        {
            // Using stored procedure (like instructor's pattern with CommandType.StoredProcedure)
            DataSet ds = _dbcon.getStoredProcedure("proc_SearchProducts",
                ("@ProductCategory", category ?? (object)DBNull.Value),
                ("@ColorCode", DBNull.Value),
                ("@MaterialName", DBNull.Value),
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
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product)
        {
            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(product);
            }

            // INSERT - Product table only has: Brand, StockQuantity, Price, ProductTypeID
            string sqlstr = "INSERT INTO Product (Brand, StockQuantity, Price, ProductTypeID) " +
                           "VALUES (@Brand, @StockQuantity, @Price, @ProductTypeID); " +
                           "SELECT SCOPE_IDENTITY();";

            int newId = _dbcon.executeInsert(sqlstr,
                ("@Brand", product.Brand),
                ("@StockQuantity", product.StockQuantity),
                ("@Price", product.Price ?? (object)DBNull.Value),
                ("@ProductTypeID", product.ProductTypeID)
            );

            if (newId > 0)
            {
                TempData["Success"] = $"Product created with ID: {newId}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Failed to create product.";
            LoadDropdowns();
            return View(product);
        }

        /// <summary>
        /// Show edit form
        /// GET: /Product/Edit/5
        /// </summary>
        public IActionResult Edit(int id)
        {
            string sqlstr = "SELECT * FROM Product WHERE ProductID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            LoadDropdowns();
            return View(MapRowToProductBasic(ds.Tables[0].Rows[0]));
        }

        /// <summary>
        /// Handle edit
        /// POST: /Product/Edit/5
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Product product)
        {
            if (id != product.ProductID)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(product);
            }

            // UPDATE - Product table only has: Brand, StockQuantity, Price, ProductTypeID
            string sqlstr = "UPDATE Product SET " +
                           "Brand = @Brand, " +
                           "StockQuantity = @StockQuantity, " +
                           "Price = @Price, " +
                           "ProductTypeID = @ProductTypeID " +
                           "WHERE ProductID = @ProductID";

            bool success = _dbcon.executeWithParams(sqlstr,
                ("@ProductID", product.ProductID),
                ("@Brand", product.Brand),
                ("@StockQuantity", product.StockQuantity),
                ("@Price", product.Price ?? (object)DBNull.Value),
                ("@ProductTypeID", product.ProductTypeID)
            );

            if (success)
            {
                TempData["Success"] = "Product updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Failed to update product.";
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
        /// Load ProductTypes from database for dropdown
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


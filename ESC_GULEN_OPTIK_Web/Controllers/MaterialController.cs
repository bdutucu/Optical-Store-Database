using Microsoft.AspNetCore.Mvc;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Filters;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Material Controller - Manage product materials (Admin only)
    /// </summary>
    [ServiceFilter(typeof(AuthenticationFilter))]
    [ServiceFilter(typeof(AdminAuthorizationFilter))]
    public class MaterialController : Controller
    {
        private readonly DBConnection _dbcon;

        public MaterialController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// List all materials
        /// GET: /Material
        /// </summary>
        public IActionResult Index()
        {
            var materials = new List<dynamic>();
            
            string query = "SELECT MaterialID, MaterialName, Description FROM Materials ORDER BY MaterialName";
            
            DataSet ds = _dbcon.getSelect(query);
            
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                materials.Add(new
                {
                    MaterialID = Convert.ToInt32(row["MaterialID"]),
                    MaterialName = row["MaterialName"].ToString(),
                    Description = row["Description"] == DBNull.Value ? "" : row["Description"].ToString()
                });
            }
            
            return View(materials);
        }

        /// <summary>
        /// Create material form
        /// GET: /Material/Create
        /// </summary>
        public IActionResult Create()
        {
            return View();
        }

        /// <summary>
        /// Create material - submit
        /// POST: /Material/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(string MaterialName, string? Description)
        {
            if (string.IsNullOrWhiteSpace(MaterialName))
            {
                TempData["Error"] = "Material name is required.";
                return View();
            }

            try
            {
                string sql = "INSERT INTO Materials (MaterialName, Description) VALUES (@name, @desc)";
                bool success = _dbcon.executeWithParams(sql,
                    ("@name", MaterialName.Trim()),
                    ("@desc", (object?)Description ?? DBNull.Value));

                if (success)
                {
                    TempData["Success"] = $"Material '{MaterialName}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Error"] = "Failed to create material.";
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UNIQUE") || ex.Message.Contains("duplicate"))
                {
                    TempData["Error"] = $"Material '{MaterialName}' already exists.";
                }
                else
                {
                    TempData["Error"] = "Error: " + ex.Message;
                }
            }

            return View();
        }

        /// <summary>
        /// Edit material form
        /// GET: /Material/Edit/5
        /// </summary>
        public IActionResult Edit(int id)
        {
            string sql = "SELECT * FROM Materials WHERE MaterialID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sql, ("@id", id));
            
            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Material not found.";
                return RedirectToAction(nameof(Index));
            }
            
            DataRow row = ds.Tables[0].Rows[0];
            ViewBag.MaterialID = Convert.ToInt32(row["MaterialID"]);
            ViewBag.MaterialName = row["MaterialName"].ToString();
            ViewBag.Description = row["Description"] == DBNull.Value ? "" : row["Description"].ToString();
            
            return View();
        }

        /// <summary>
        /// Edit material - submit
        /// POST: /Material/Edit/5
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, string MaterialName, string? Description)
        {
            if (string.IsNullOrWhiteSpace(MaterialName))
            {
                TempData["Error"] = "Material name is required.";
                ViewBag.MaterialID = id;
                ViewBag.MaterialName = MaterialName;
                ViewBag.Description = Description;
                return View();
            }

            try
            {
                string sql = "UPDATE Materials SET MaterialName = @name, Description = @desc WHERE MaterialID = @id";
                bool success = _dbcon.executeWithParams(sql,
                    ("@id", id),
                    ("@name", MaterialName.Trim()),
                    ("@desc", (object?)Description ?? DBNull.Value));

                if (success)
                {
                    TempData["Success"] = $"Material '{MaterialName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Error"] = "Failed to update material.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }

            ViewBag.MaterialID = id;
            ViewBag.MaterialName = MaterialName;
            ViewBag.Description = Description;
            return View();
        }

        /// <summary>
        /// Delete material
        /// POST: /Material/Delete/5
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            try
            {
                // Check if material is in use
                DataSet dsCheck = _dbcon.getSelectWithParams(
                    "SELECT COUNT(*) AS Cnt FROM ProductMaterials WHERE MaterialID = @id",
                    ("@id", id));
                
                int count = Convert.ToInt32(dsCheck.Tables[0].Rows[0]["Cnt"]);
                
                if (count > 0)
                {
                    TempData["Error"] = $"Cannot delete - material is used by {count} product(s).";
                    return RedirectToAction(nameof(Index));
                }

                string sql = "DELETE FROM Materials WHERE MaterialID = @id";
                bool success = _dbcon.executeWithParams(sql, ("@id", id));

                if (success)
                    TempData["Success"] = "Material deleted successfully.";
                else
                    TempData["Error"] = "Failed to delete material.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}


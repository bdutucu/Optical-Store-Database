using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using ESC_GULEN_OPTIK_Web.Filters;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Prescription Controller - Manages eye prescriptions for customers
    /// Staff can create/edit prescriptions, link them to sales
    /// </summary>
    [ServiceFilter(typeof(AuthenticationFilter))]
    public class PrescriptionController : Controller
    {
        private readonly DBConnection _dbcon;

        public PrescriptionController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// List all prescriptions
        /// GET: /Prescription
        /// </summary>
        public IActionResult Index(int? customerId)
        {
            string sqlstr;
            DataSet ds;

            if (customerId.HasValue)
            {
                sqlstr = @"SELECT p.*, 
                           c.FirstName + ' ' + c.LastName AS CustomerName,
                           s.FirstName + ' ' + s.LastName AS StaffName
                           FROM Prescription p
                           INNER JOIN Customer c ON p.CustomerID = c.CustomerID
                           LEFT JOIN Staff s ON p.StaffID = s.StaffID
                           WHERE p.CustomerID = @customerId
                           ORDER BY p.DateOfPrescription DESC";
                ds = _dbcon.getSelectWithParams(sqlstr, ("@customerId", customerId.Value));
                
                // Get customer name for title
                var custDs = _dbcon.getSelectWithParams(
                    "SELECT FirstName + ' ' + LastName AS FullName FROM Customer WHERE CustomerID = @id",
                    ("@id", customerId.Value));
                if (custDs.Tables[0].Rows.Count > 0)
                {
                    ViewBag.CustomerName = custDs.Tables[0].Rows[0]["FullName"].ToString();
                }
                ViewBag.CustomerId = customerId.Value;
            }
            else
            {
                sqlstr = @"SELECT p.*, 
                           c.FirstName + ' ' + c.LastName AS CustomerName,
                           s.FirstName + ' ' + s.LastName AS StaffName
                           FROM Prescription p
                           INNER JOIN Customer c ON p.CustomerID = c.CustomerID
                           LEFT JOIN Staff s ON p.StaffID = s.StaffID
                           ORDER BY p.DateOfPrescription DESC";
                ds = _dbcon.getSelect(sqlstr);
            }

            var prescriptions = new List<Prescription>();
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                prescriptions.Add(MapRowToPrescription(row));
            }

            return View(prescriptions);
        }

        /// <summary>
        /// Show prescription details
        /// GET: /Prescription/Details/5
        /// </summary>
        public IActionResult Details(int id)
        {
            string sqlstr = @"SELECT p.*, 
                              c.FirstName + ' ' + c.LastName AS CustomerName,
                              s.FirstName + ' ' + s.LastName AS StaffName
                              FROM Prescription p
                              INNER JOIN Customer c ON p.CustomerID = c.CustomerID
                              LEFT JOIN Staff s ON p.StaffID = s.StaffID
                              WHERE p.PrescriptionID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Prescription not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(MapRowToPrescription(ds.Tables[0].Rows[0]));
        }

        /// <summary>
        /// Show create form
        /// GET: /Prescription/Create
        /// Only Optician can write prescriptions (internal eye exam)
        /// </summary>
        public IActionResult Create(int? customerId)
        {
            // Check if current user is Optician
            var currentUser = HttpContext.Session.GetCurrentUser();
            if (currentUser == null)
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Auth");
            }
            
            // Get user's position from database
            DataSet dsStaff = _dbcon.getSelectWithParams(
                "SELECT Position FROM Staff WHERE StaffID = @id",
                ("@id", currentUser.StaffID));
            
            string? position = null;
            if (dsStaff.Tables[0].Rows.Count > 0)
            {
                position = dsStaff.Tables[0].Rows[0]["Position"]?.ToString();
            }
            
            // Only Optician (or Admin) can write prescriptions
            bool isOptician = position?.ToLower().Contains("optician") == true || 
                              position?.ToLower().Contains("optometrist") == true ||
                              position?.ToLower().Contains("göz") == true;
            bool isAdmin = currentUser.IsAdmin;
            
            if (!isOptician && !isAdmin)
            {
                TempData["Error"] = "Sadece Optician (Göz Uzmanı) pozisyonundaki personel reçete yazabilir.";
                return RedirectToAction(nameof(Index));
            }
            
            LoadDropdowns();
            var prescription = new Prescription();
            
            if (customerId.HasValue)
            {
                prescription.CustomerID = customerId.Value;
            }
            
            // Default to current staff if logged in
            prescription.StaffID = currentUser.StaffID;
            
            // Pass current user name to view
            ViewBag.CurrentUserName = currentUser.FullName;
            
            return View(prescription);
        }

        /// <summary>
        /// Handle create
        /// POST: /Prescription/Create
        /// Only Optician can write prescriptions
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Prescription prescription)
        {
            // Check if current user is Optician
            var currentUser = HttpContext.Session.GetCurrentUser();
            if (currentUser == null)
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Auth");
            }
            
            // Get user's position from database
            DataSet dsStaff = _dbcon.getSelectWithParams(
                "SELECT Position FROM Staff WHERE StaffID = @id",
                ("@id", currentUser.StaffID));
            
            string? position = null;
            if (dsStaff.Tables[0].Rows.Count > 0)
            {
                position = dsStaff.Tables[0].Rows[0]["Position"]?.ToString();
            }
            
            bool isOptician = position?.ToLower().Contains("optician") == true || 
                              position?.ToLower().Contains("optometrist") == true ||
                              position?.ToLower().Contains("göz") == true;
            bool isAdmin = currentUser.IsAdmin;
            
            if (!isOptician && !isAdmin)
            {
                TempData["Error"] = "Sadece Optician (Göz Uzmanı) pozisyonundaki personel reçete yazabilir.";
                return RedirectToAction(nameof(Index));
            }
            
            // Remove navigation properties from validation
            ModelState.Remove("CustomerName");
            ModelState.Remove("StaffName");

            // Ensure StaffID is set to current user (hidden field in form)
            if (!prescription.StaffID.HasValue || prescription.StaffID == 0)
            {
                prescription.StaffID = currentUser.StaffID;
            }

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                ViewBag.CurrentUserName = currentUser.FullName;
                return View(prescription);
            }

            try
            {
                string sqlstr = @"INSERT INTO Prescription 
                    (DateOfPrescription, DoctorName, CustomerID, StaffID, 
                     Right_SPH, Right_CYL, Right_AX, Left_SPH, Left_CYL, Left_AX)
                    VALUES 
                    (@date, @doctor, @customerId, @staffId,
                     @rightSph, @rightCyl, @rightAx, @leftSph, @leftCyl, @leftAx)";

                bool success = _dbcon.executeWithParams(sqlstr,
                    ("@date", prescription.DateOfPrescription),
                    ("@doctor", (object?)prescription.DoctorName ?? DBNull.Value),
                    ("@customerId", prescription.CustomerID),
                    ("@staffId", currentUser.StaffID),  // Always use logged-in user
                    ("@rightSph", prescription.Right_SPH ?? (object)DBNull.Value),
                    ("@rightCyl", prescription.Right_CYL ?? (object)DBNull.Value),
                    ("@rightAx", prescription.Right_AX ?? (object)DBNull.Value),
                    ("@leftSph", prescription.Left_SPH ?? (object)DBNull.Value),
                    ("@leftCyl", prescription.Left_CYL ?? (object)DBNull.Value),
                    ("@leftAx", prescription.Left_AX ?? (object)DBNull.Value)
                );

                if (success)
                {
                    TempData["Success"] = "Prescription created successfully.";
                    return RedirectToAction(nameof(Index), new { customerId = prescription.CustomerID });
                }

                TempData["Error"] = "Failed to create prescription.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }

            LoadDropdowns();
            ViewBag.CurrentUserName = currentUser.FullName;
            return View(prescription);
        }

        /// <summary>
        /// Show edit form
        /// GET: /Prescription/Edit/5
        /// Only Optician or Admin can edit prescriptions
        /// </summary>
        public IActionResult Edit(int id)
        {
            // Check if current user is Optician or Admin
            var currentUser = HttpContext.Session.GetCurrentUser();
            if (currentUser == null)
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Auth");
            }
            
            // Get user's position from database
            DataSet dsStaff = _dbcon.getSelectWithParams(
                "SELECT Position FROM Staff WHERE StaffID = @id",
                ("@id", currentUser.StaffID));
            
            string? position = null;
            if (dsStaff.Tables[0].Rows.Count > 0)
            {
                position = dsStaff.Tables[0].Rows[0]["Position"]?.ToString();
            }
            
            bool isOptician = position?.ToLower().Contains("optician") == true || 
                              position?.ToLower().Contains("optometrist") == true ||
                              position?.ToLower().Contains("göz") == true;
            bool isAdmin = currentUser.IsAdmin;
            
            if (!isOptician && !isAdmin)
            {
                TempData["Error"] = "Sadece Optician veya Admin reçete düzenleyebilir.";
                return RedirectToAction(nameof(Index));
            }
            
            // Get prescription with customer and staff names
            string sqlstr = @"SELECT p.*, 
                              c.FirstName + ' ' + c.LastName AS CustomerName,
                              s.FirstName + ' ' + s.LastName AS StaffName
                              FROM Prescription p
                              INNER JOIN Customer c ON p.CustomerID = c.CustomerID
                              LEFT JOIN Staff s ON p.StaffID = s.StaffID
                              WHERE p.PrescriptionID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Prescription not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(MapRowToPrescription(ds.Tables[0].Rows[0]));
        }

        /// <summary>
        /// Handle edit
        /// POST: /Prescription/Edit/5
        /// Only Optician or Admin can edit - only eye measurements can be changed
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Prescription prescription)
        {
            if (id != prescription.PrescriptionID)
                return BadRequest();

            // Check if current user is Optician or Admin
            var currentUser = HttpContext.Session.GetCurrentUser();
            if (currentUser == null)
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Auth");
            }
            
            // Get user's position from database
            DataSet dsStaff = _dbcon.getSelectWithParams(
                "SELECT Position FROM Staff WHERE StaffID = @id",
                ("@id", currentUser.StaffID));
            
            string? position = null;
            if (dsStaff.Tables[0].Rows.Count > 0)
            {
                position = dsStaff.Tables[0].Rows[0]["Position"]?.ToString();
            }
            
            bool isOptician = position?.ToLower().Contains("optician") == true || 
                              position?.ToLower().Contains("optometrist") == true ||
                              position?.ToLower().Contains("göz") == true;
            bool isAdmin = currentUser.IsAdmin;
            
            if (!isOptician && !isAdmin)
            {
                TempData["Error"] = "Sadece Optician veya Admin reçete düzenleyebilir.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.Remove("CustomerName");
            ModelState.Remove("StaffName");

            if (!ModelState.IsValid)
            {
                return View(prescription);
            }

            try
            {
                // ONLY update eye measurements - CustomerID, StaffID, Date, DoctorName stay the same
                string sqlstr = @"UPDATE Prescription SET
                    Right_SPH = @rightSph,
                    Right_CYL = @rightCyl,
                    Right_AX = @rightAx,
                    Left_SPH = @leftSph,
                    Left_CYL = @leftCyl,
                    Left_AX = @leftAx
                    WHERE PrescriptionID = @id";

                bool success = _dbcon.executeWithParams(sqlstr,
                    ("@id", prescription.PrescriptionID),
                    ("@rightSph", prescription.Right_SPH ?? (object)DBNull.Value),
                    ("@rightCyl", prescription.Right_CYL ?? (object)DBNull.Value),
                    ("@rightAx", prescription.Right_AX ?? (object)DBNull.Value),
                    ("@leftSph", prescription.Left_SPH ?? (object)DBNull.Value),
                    ("@leftCyl", prescription.Left_CYL ?? (object)DBNull.Value),
                    ("@leftAx", prescription.Left_AX ?? (object)DBNull.Value)
                );

                if (success)
                {
                    TempData["Success"] = "Eye measurements updated successfully.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Error"] = "Failed to update prescription.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }

            return View(prescription);
        }

        /// <summary>
        /// Show delete confirmation
        /// GET: /Prescription/Delete/5
        /// </summary>
        public IActionResult Delete(int id)
        {
            string sqlstr = @"SELECT p.*, 
                              c.FirstName + ' ' + c.LastName AS CustomerName,
                              s.FirstName + ' ' + s.LastName AS StaffName
                              FROM Prescription p
                              INNER JOIN Customer c ON p.CustomerID = c.CustomerID
                              LEFT JOIN Staff s ON p.StaffID = s.StaffID
                              WHERE p.PrescriptionID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Prescription not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(MapRowToPrescription(ds.Tables[0].Rows[0]));
        }

        /// <summary>
        /// Handle delete
        /// POST: /Prescription/Delete/5
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            string sqlstr = "DELETE FROM Prescription WHERE PrescriptionID = @id";
            bool success = _dbcon.executeWithParams(sqlstr, ("@id", id));

            if (success)
                TempData["Success"] = "Prescription deleted successfully.";
            else
                TempData["Error"] = "Failed to delete prescription.";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// API: Get prescriptions for a specific customer (for AJAX calls)
        /// GET: /Prescription/GetByCustomer/5
        /// </summary>
        [HttpGet]
        public IActionResult GetByCustomer(int customerId)
        {
            string sqlstr = @"SELECT PrescriptionID, DateOfPrescription, DoctorName,
                              Right_SPH, Right_CYL, Right_AX, Left_SPH, Left_CYL, Left_AX
                              FROM Prescription 
                              WHERE CustomerID = @customerId
                              ORDER BY DateOfPrescription DESC";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@customerId", customerId));

            var prescriptions = new List<object>();
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                var date = Convert.ToDateTime(row["DateOfPrescription"]);
                var doctor = row["DoctorName"] == DBNull.Value ? "" : row["DoctorName"].ToString();
                var rightSph = row["Right_SPH"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(row["Right_SPH"]);
                
                prescriptions.Add(new
                {
                    id = Convert.ToInt32(row["PrescriptionID"]),
                    date = date.ToString("yyyy-MM-dd"),
                    doctor = doctor,
                    display = $"{date:dd.MM.yyyy} - {(string.IsNullOrEmpty(doctor) ? "Internal" : doctor)} (R: {(rightSph.HasValue ? rightSph.Value.ToString("+0.00;-0.00") : "N/A")})"
                });
            }

            return Json(prescriptions);
        }

        private void LoadDropdowns()
        {
            // Customers dropdown
            var custDs = _dbcon.getSelect("SELECT CustomerID, FirstName + ' ' + LastName AS FullName FROM Customer ORDER BY FirstName");
            var customers = new List<SelectListItem>();
            foreach (DataRow row in custDs.Tables[0].Rows)
            {
                customers.Add(new SelectListItem
                {
                    Value = row["CustomerID"].ToString(),
                    Text = row["FullName"].ToString()
                });
            }
            ViewBag.Customers = customers;

            // Staff dropdown (for internal examination)
            var staffDs = _dbcon.getSelect("SELECT StaffID, FirstName + ' ' + LastName AS FullName FROM Staff ORDER BY FirstName");
            var staff = new List<SelectListItem>();
            foreach (DataRow row in staffDs.Tables[0].Rows)
            {
                staff.Add(new SelectListItem
                {
                    Value = row["StaffID"].ToString(),
                    Text = row["FullName"].ToString()
                });
            }
            ViewBag.Staff = staff;
        }

        private Prescription MapRowToPrescription(DataRow row)
        {
            return new Prescription
            {
                PrescriptionID = Convert.ToInt32(row["PrescriptionID"]),
                DateOfPrescription = Convert.ToDateTime(row["DateOfPrescription"]),
                DoctorName = row["DoctorName"] == DBNull.Value ? null : row["DoctorName"].ToString(),
                CustomerID = Convert.ToInt32(row["CustomerID"]),
                StaffID = row["StaffID"] == DBNull.Value ? null : Convert.ToInt32(row["StaffID"]),
                Right_SPH = row["Right_SPH"] == DBNull.Value ? null : Convert.ToDecimal(row["Right_SPH"]),
                Right_CYL = row["Right_CYL"] == DBNull.Value ? null : Convert.ToDecimal(row["Right_CYL"]),
                Right_AX = row["Right_AX"] == DBNull.Value ? null : Convert.ToInt32(row["Right_AX"]),
                Left_SPH = row["Left_SPH"] == DBNull.Value ? null : Convert.ToDecimal(row["Left_SPH"]),
                Left_CYL = row["Left_CYL"] == DBNull.Value ? null : Convert.ToDecimal(row["Left_CYL"]),
                Left_AX = row["Left_AX"] == DBNull.Value ? null : Convert.ToInt32(row["Left_AX"]),
                CustomerName = row.Table.Columns.Contains("CustomerName") && row["CustomerName"] != DBNull.Value 
                    ? row["CustomerName"].ToString() : null,
                StaffName = row.Table.Columns.Contains("StaffName") && row["StaffName"] != DBNull.Value 
                    ? row["StaffName"].ToString() : null
            };
        }
    }
}


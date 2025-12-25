using Microsoft.AspNetCore.Mvc;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Staff Controller - CRUD operations for Staff
    /// Uses instructor's DBConnection pattern
    /// </summary>
    public class StaffController : Controller
    {
        private readonly DBConnection _dbcon;

        public StaffController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// List all staff members
        /// GET: /Staff
        /// </summary>
        public IActionResult Index()
        {
            // Simple SELECT query (like instructor's pattern)
            string sqlstr = "SELECT StaffID, FirstName, LastName, Email, Salary, Position, " +
                           "DateOfBirth, Age, PhoneNumber, JobStartDate, YearsOfExperience " +
                           "FROM Staff ORDER BY LastName, FirstName";

            DataSet ds = _dbcon.getSelect(sqlstr);
            
            var staffList = new List<Staff>();
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                staffList.Add(MapRowToStaff(row));
            }

            return View(staffList);
        }

        /// <summary>
        /// Show details of a staff member
        /// GET: /Staff/Details/5
        /// </summary>
        public IActionResult Details(int id)
        {
            // Parameterized query
            string sqlstr = "SELECT * FROM Staff WHERE StaffID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Staff not found.";
                return RedirectToAction(nameof(Index));
            }

            var staff = MapRowToStaff(ds.Tables[0].Rows[0]);
            return View(staff);
        }

        /// <summary>
        /// Show create form
        /// GET: /Staff/Create
        /// </summary>
        public IActionResult Create()
        {
            return View(new Staff { JobStartDate = DateTime.Today });
        }

        /// <summary>
        /// Handle create form submission
        /// POST: /Staff/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Staff staff)
        {
            if (!ModelState.IsValid)
                return View(staff);

            // INSERT with parameterized query (like instructor's pattern)
            string sqlstr = "INSERT INTO Staff (FirstName, LastName, Email, Salary, Position, " +
                           "DateOfBirth, PhoneNumber, JobStartDate) " +
                           "VALUES (@FirstName, @LastName, @Email, @Salary, @Position, " +
                           "@DateOfBirth, @PhoneNumber, @JobStartDate); " +
                           "SELECT SCOPE_IDENTITY();";

            int newId = _dbcon.executeInsert(sqlstr,
                ("@FirstName", staff.FirstName),
                ("@LastName", staff.LastName),
                ("@Email", staff.Email),
                ("@Salary", staff.Salary),
                ("@Position", staff.Position ?? (object)DBNull.Value),
                ("@DateOfBirth", staff.DateOfBirth ?? (object)DBNull.Value),
                ("@PhoneNumber", staff.PhoneNumber ?? (object)DBNull.Value),
                ("@JobStartDate", staff.JobStartDate)
            );

            if (newId > 0)
            {
                TempData["Success"] = $"Staff created with ID: {newId}";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["Error"] = "Failed to create staff.";
                return View(staff);
            }
        }

        /// <summary>
        /// Show edit form
        /// GET: /Staff/Edit/5
        /// </summary>
        public IActionResult Edit(int id)
        {
            string sqlstr = "SELECT * FROM Staff WHERE StaffID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Staff not found.";
                return RedirectToAction(nameof(Index));
            }

            var staff = MapRowToStaff(ds.Tables[0].Rows[0]);
            return View(staff);
        }

        /// <summary>
        /// Handle edit form submission
        /// POST: /Staff/Edit/5
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Staff staff)
        {
            if (id != staff.StaffID)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(staff);

            // UPDATE with parameterized query
            string sqlstr = "UPDATE Staff SET " +
                           "FirstName = @FirstName, " +
                           "LastName = @LastName, " +
                           "Email = @Email, " +
                           "Salary = @Salary, " +
                           "Position = @Position, " +
                           "DateOfBirth = @DateOfBirth, " +
                           "PhoneNumber = @PhoneNumber, " +
                           "JobStartDate = @JobStartDate " +
                           "WHERE StaffID = @StaffID";

            bool success = _dbcon.executeWithParams(sqlstr,
                ("@StaffID", staff.StaffID),
                ("@FirstName", staff.FirstName),
                ("@LastName", staff.LastName),
                ("@Email", staff.Email),
                ("@Salary", staff.Salary),
                ("@Position", staff.Position ?? (object)DBNull.Value),
                ("@DateOfBirth", staff.DateOfBirth ?? (object)DBNull.Value),
                ("@PhoneNumber", staff.PhoneNumber ?? (object)DBNull.Value),
                ("@JobStartDate", staff.JobStartDate)
            );

            if (success)
            {
                TempData["Success"] = "Staff updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["Error"] = "Failed to update staff.";
                return View(staff);
            }
        }

        /// <summary>
        /// Show delete confirmation
        /// GET: /Staff/Delete/5
        /// </summary>
        public IActionResult Delete(int id)
        {
            string sqlstr = "SELECT * FROM Staff WHERE StaffID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Staff not found.";
                return RedirectToAction(nameof(Index));
            }

            var staff = MapRowToStaff(ds.Tables[0].Rows[0]);
            return View(staff);
        }

        /// <summary>
        /// Handle delete confirmation
        /// POST: /Staff/Delete/5
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            // DELETE with parameterized query
            string sqlstr = "DELETE FROM Staff WHERE StaffID = @id";
            bool success = _dbcon.executeWithParams(sqlstr, ("@id", id));

            if (success)
            {
                TempData["Success"] = "Staff deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to delete staff.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Helper method to map DataRow to Staff object
        /// </summary>
        private Staff MapRowToStaff(DataRow row)
        {
            return new Staff
            {
                StaffID = Convert.ToInt32(row["StaffID"]),
                FirstName = row["FirstName"].ToString() ?? "",
                LastName = row["LastName"].ToString() ?? "",
                Email = row["Email"].ToString() ?? "",
                Salary = Convert.ToDecimal(row["Salary"]),
                Position = row["Position"] == DBNull.Value ? null : row["Position"].ToString(),
                DateOfBirth = row["DateOfBirth"] == DBNull.Value ? null : Convert.ToDateTime(row["DateOfBirth"]),
                Age = row["Age"] == DBNull.Value ? null : Convert.ToInt32(row["Age"]),
                PhoneNumber = row["PhoneNumber"] == DBNull.Value ? null : row["PhoneNumber"].ToString(),
                JobStartDate = Convert.ToDateTime(row["JobStartDate"]),
                YearsOfExperience = row["YearsOfExperience"] == DBNull.Value ? null : Convert.ToInt32(row["YearsOfExperience"])
            };
        }
    }
}


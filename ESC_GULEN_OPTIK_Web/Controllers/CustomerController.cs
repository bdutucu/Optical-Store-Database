using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using ESC_GULEN_OPTIK_Web.Filters;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Customer Controller - CRUD operations for Customer
    /// Accessible by all logged-in staff
    /// </summary>
    [ServiceFilter(typeof(AuthenticationFilter))]
    public class CustomerController : Controller
    {
        private readonly DBConnection _dbcon;

        public CustomerController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// List all customers
        /// GET: /Customer
        /// </summary>
        public IActionResult Index()
        {
            // JOIN query to get staff name (like instructor's pattern)
            string sqlstr = "SELECT c.CustomerID, c.NationalID, c.FirstName, c.LastName, " +
                           "c.MailAddress, c.InsuranceInfo, c.RegisteredByStaffID, " +
                           "s.FirstName + ' ' + s.LastName AS StaffName " +
                           "FROM Customer c " +
                           "INNER JOIN Staff s ON c.RegisteredByStaffID = s.StaffID " +
                           "ORDER BY c.LastName, c.FirstName";

            DataSet ds = _dbcon.getSelect(sqlstr);

            var customers = new List<Customer>();
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                customers.Add(MapRowToCustomer(row));
            }

            return View(customers);
        }

        /// <summary>
        /// Show details
        /// GET: /Customer/Details/5
        /// </summary>
        public IActionResult Details(int id)
        {
            string sqlstr = "SELECT c.*, s.FirstName + ' ' + s.LastName AS StaffName " +
                           "FROM Customer c " +
                           "INNER JOIN Staff s ON c.RegisteredByStaffID = s.StaffID " +
                           "WHERE c.CustomerID = @id";

            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }

            var customer = MapRowToCustomer(ds.Tables[0].Rows[0]);
            
            // Get customer's prescriptions
            string prescriptionSql = @"SELECT p.*, st.FirstName + ' ' + st.LastName AS StaffName 
                                       FROM Prescription p 
                                       LEFT JOIN Staff st ON p.StaffID = st.StaffID
                                       WHERE p.CustomerID = @id 
                                       ORDER BY p.DateOfPrescription DESC";
            DataSet dsPrescriptions = _dbcon.getSelectWithParams(prescriptionSql, ("@id", id));
            
            var prescriptions = new List<Prescription>();
            foreach (DataRow row in dsPrescriptions.Tables[0].Rows)
            {
                prescriptions.Add(new Prescription
                {
                    PrescriptionID = Convert.ToInt32(row["PrescriptionID"]),
                    DateOfPrescription = Convert.ToDateTime(row["DateOfPrescription"]),
                    DoctorName = row["DoctorName"] == DBNull.Value ? null : row["DoctorName"].ToString(),
                    StaffID = row["StaffID"] == DBNull.Value ? null : Convert.ToInt32(row["StaffID"]),
                    StaffName = row["StaffName"] == DBNull.Value ? null : row["StaffName"].ToString(),
                    Right_SPH = row["Right_SPH"] == DBNull.Value ? null : Convert.ToDecimal(row["Right_SPH"]),
                    Right_CYL = row["Right_CYL"] == DBNull.Value ? null : Convert.ToDecimal(row["Right_CYL"]),
                    Right_AX = row["Right_AX"] == DBNull.Value ? null : Convert.ToInt32(row["Right_AX"]),
                    Left_SPH = row["Left_SPH"] == DBNull.Value ? null : Convert.ToDecimal(row["Left_SPH"]),
                    Left_CYL = row["Left_CYL"] == DBNull.Value ? null : Convert.ToDecimal(row["Left_CYL"]),
                    Left_AX = row["Left_AX"] == DBNull.Value ? null : Convert.ToInt32(row["Left_AX"])
                });
            }
            ViewBag.Prescriptions = prescriptions;
            
            // Get customer's recent transactions
            string transactionSql = @"SELECT TOP 5 t.TransactionID, t.TransactionDate, t.TotalAmount, 
                                      t.RemainingBalance, tt.TypeName AS TransactionTypeName
                                      FROM Transactions t 
                                      INNER JOIN TransactionTypes tt ON t.TransactionTypeID = tt.TransactionTypeID
                                      WHERE t.CustomerID = @id 
                                      ORDER BY t.TransactionDate DESC";
            DataSet dsTransactions = _dbcon.getSelectWithParams(transactionSql, ("@id", id));
            ViewBag.RecentTransactions = dsTransactions.Tables[0];

            return View(customer);
        }

        /// <summary>
        /// Show create form
        /// GET: /Customer/Create
        /// </summary>
        public IActionResult Create()
        {
            LoadStaffDropdown();
            return View(new Customer());
        }

        /// <summary>
        /// Handle create
        /// POST: /Customer/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Customer customer)
        {
            if (!ModelState.IsValid)
            {
                LoadStaffDropdown();
                return View(customer);
            }

            string sqlstr = "INSERT INTO Customer (NationalID, FirstName, LastName, MailAddress, " +
                           "InsuranceInfo, RegisteredByStaffID) " +
                           "VALUES (@NationalID, @FirstName, @LastName, @MailAddress, " +
                           "@InsuranceInfo, @RegisteredByStaffID); " +
                           "SELECT SCOPE_IDENTITY();";

            int newId = _dbcon.executeInsert(sqlstr,
                ("@NationalID", customer.NationalID),
                ("@FirstName", customer.FirstName),
                ("@LastName", customer.LastName),
                ("@MailAddress", customer.MailAddress ?? (object)DBNull.Value),
                ("@InsuranceInfo", customer.InsuranceInfo ?? (object)DBNull.Value),
                ("@RegisteredByStaffID", customer.RegisteredByStaffID)
            );

            if (newId > 0)
            {
                TempData["Success"] = $"Customer registered with ID: {newId}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Failed to register customer.";
            LoadStaffDropdown();
            return View(customer);
        }

        /// <summary>
        /// Show edit form
        /// GET: /Customer/Edit/5
        /// </summary>
        public IActionResult Edit(int id)
        {
            string sqlstr = "SELECT * FROM Customer WHERE CustomerID = @id";
            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }

            LoadStaffDropdown();
            return View(MapRowToCustomer(ds.Tables[0].Rows[0]));
        }

        /// <summary>
        /// Handle edit
        /// POST: /Customer/Edit/5
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Customer customer)
        {
            if (id != customer.CustomerID)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                LoadStaffDropdown();
                return View(customer);
            }

            string sqlstr = "UPDATE Customer SET " +
                           "NationalID = @NationalID, " +
                           "FirstName = @FirstName, " +
                           "LastName = @LastName, " +
                           "MailAddress = @MailAddress, " +
                           "InsuranceInfo = @InsuranceInfo, " +
                           "RegisteredByStaffID = @RegisteredByStaffID " +
                           "WHERE CustomerID = @CustomerID";

            bool success = _dbcon.executeWithParams(sqlstr,
                ("@CustomerID", customer.CustomerID),
                ("@NationalID", customer.NationalID),
                ("@FirstName", customer.FirstName),
                ("@LastName", customer.LastName),
                ("@MailAddress", customer.MailAddress ?? (object)DBNull.Value),
                ("@InsuranceInfo", customer.InsuranceInfo ?? (object)DBNull.Value),
                ("@RegisteredByStaffID", customer.RegisteredByStaffID)
            );

            if (success)
            {
                TempData["Success"] = "Customer updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Failed to update customer.";
            LoadStaffDropdown();
            return View(customer);
        }

        /// <summary>
        /// Show delete confirmation
        /// GET: /Customer/Delete/5
        /// </summary>
        public IActionResult Delete(int id)
        {
            string sqlstr = "SELECT c.*, s.FirstName + ' ' + s.LastName AS StaffName " +
                           "FROM Customer c " +
                           "INNER JOIN Staff s ON c.RegisteredByStaffID = s.StaffID " +
                           "WHERE c.CustomerID = @id";

            DataSet ds = _dbcon.getSelectWithParams(sqlstr, ("@id", id));

            if (ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(MapRowToCustomer(ds.Tables[0].Rows[0]));
        }

        /// <summary>
        /// Handle delete
        /// POST: /Customer/Delete/5
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            string sqlstr = "DELETE FROM Customer WHERE CustomerID = @id";
            bool success = _dbcon.executeWithParams(sqlstr, ("@id", id));

            if (success)
                TempData["Success"] = "Customer deleted successfully.";
            else
                TempData["Error"] = "Failed to delete customer.";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Load staff dropdown (like instructor's DropDownList pattern)
        /// </summary>
        private void LoadStaffDropdown()
        {
            string sqlstr = "SELECT StaffID as id, FirstName + ' ' + LastName as name FROM Staff";
            DataSet ds = _dbcon.getSelect(sqlstr);

            var staffList = new List<SelectListItem>();
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                staffList.Add(new SelectListItem
                {
                    Value = row["id"].ToString(),
                    Text = row["name"].ToString()
                });
            }

            ViewBag.StaffList = staffList;
        }

        private Customer MapRowToCustomer(DataRow row)
        {
            return new Customer
            {
                CustomerID = Convert.ToInt32(row["CustomerID"]),
                NationalID = row["NationalID"].ToString() ?? "",
                FirstName = row["FirstName"].ToString() ?? "",
                LastName = row["LastName"].ToString() ?? "",
                MailAddress = row["MailAddress"] == DBNull.Value ? null : row["MailAddress"].ToString(),
                InsuranceInfo = row["InsuranceInfo"] == DBNull.Value ? null : row["InsuranceInfo"].ToString(),
                RegisteredByStaffID = Convert.ToInt32(row["RegisteredByStaffID"]),
                StaffName = row.Table.Columns.Contains("StaffName") && row["StaffName"] != DBNull.Value 
                    ? row["StaffName"].ToString() : null
            };
        }
    }
}


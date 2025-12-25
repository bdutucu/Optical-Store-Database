using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using ESC_GULEN_OPTIK_Web.Filters;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Sales Controller - Manage sales and repair transactions
    /// Accessible by all logged-in staff
    /// </summary>
    [ServiceFilter(typeof(AuthenticationFilter))]
    public class SalesController : Controller
    {
        private readonly DBConnection _dbcon;

        public SalesController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// List all transactions with customer filtering
        /// GET: /Sales
        /// </summary>
        public IActionResult Index(int? customerId, string? transactionType, DateTime? startDate, DateTime? endDate)
        {
            var transactions = new List<Transaction>();

            string query = @"
                SELECT T.TransactionID, T.CustomerID, T.StaffID, T.TransactionDate, 
                       T.TotalAmount, T.RemainingBalance, T.TransactionTypeID,
                       C.FirstName + ' ' + C.LastName AS CustomerName,
                       S.FirstName + ' ' + S.LastName AS StaffName,
                       TT.TypeName AS TransactionTypeName
                FROM Transactions T
                INNER JOIN Customer C ON T.CustomerID = C.CustomerID
                INNER JOIN Staff S ON T.StaffID = S.StaffID
                INNER JOIN TransactionTypes TT ON T.TransactionTypeID = TT.TransactionTypeID
                WHERE 1=1";
            
            var parameters = new List<(string, object)>();
            
            // Filter by customer
            if (customerId.HasValue && customerId.Value > 0)
            {
                query += " AND T.CustomerID = @CustomerID";
                parameters.Add(("@CustomerID", customerId.Value));
                ViewBag.SelectedCustomerId = customerId.Value;
            }
            
            // Filter by transaction type
            if (!string.IsNullOrEmpty(transactionType))
            {
                query += " AND TT.TypeName = @TransactionType";
                parameters.Add(("@TransactionType", transactionType));
                ViewBag.SelectedType = transactionType;
            }
            
            // Filter by date range
            if (startDate.HasValue)
            {
                query += " AND T.TransactionDate >= @StartDate";
                parameters.Add(("@StartDate", startDate.Value.Date));
                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            }
            
            if (endDate.HasValue)
            {
                query += " AND T.TransactionDate <= @EndDate";
                parameters.Add(("@EndDate", endDate.Value.Date.AddDays(1).AddSeconds(-1)));
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            }
            
            query += " ORDER BY T.TransactionDate DESC";

            DataSet ds;
            if (parameters.Count > 0)
            {
                ds = _dbcon.getSelectWithParams(query, parameters.ToArray());
            }
            else
            {
                ds = _dbcon.getSelect(query);
            }

            if (ds.Tables.Count > 0)
            {
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    transactions.Add(new Transaction
                    {
                        TransactionID = Convert.ToInt32(row["TransactionID"]),
                        CustomerID = Convert.ToInt32(row["CustomerID"]),
                        StaffID = Convert.ToInt32(row["StaffID"]),
                        TransactionDate = Convert.ToDateTime(row["TransactionDate"]),
                        TotalAmount = Convert.ToDecimal(row["TotalAmount"]),
                        RemainingBalance = Convert.ToDecimal(row["RemainingBalance"]),
                        TransactionTypeID = Convert.ToInt32(row["TransactionTypeID"]),
                        CustomerName = row["CustomerName"]?.ToString(),
                        StaffName = row["StaffName"]?.ToString(),
                        TransactionTypeName = row["TransactionTypeName"]?.ToString()
                    });
                }
            }

            // Load customers for filter dropdown
            LoadCustomerDropdown();
            ViewBag.TransactionTypes = new[] { "SALE", "REPAIR" };

            return View(transactions);
        }

        /// <summary>
        /// Show transaction details
        /// GET: /Sales/Details/5
        /// </summary>
        public IActionResult Details(int id)
        {
            // Get transaction info
            string query = @"
                SELECT T.TransactionID, T.CustomerID, T.StaffID, T.TransactionDate, 
                       T.TotalAmount, T.RemainingBalance, T.TransactionTypeID,
                       C.FirstName + ' ' + C.LastName AS CustomerName,
                       S.FirstName + ' ' + S.LastName AS StaffName,
                       TT.TypeName AS TransactionTypeName
                FROM Transactions T
                INNER JOIN Customer C ON T.CustomerID = C.CustomerID
                INNER JOIN Staff S ON T.StaffID = S.StaffID
                INNER JOIN TransactionTypes TT ON T.TransactionTypeID = TT.TransactionTypeID
                WHERE T.TransactionID = @id";

            DataSet ds = _dbcon.getSelectWithParams(query, ("@id", id));

            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Transaction not found.";
                return RedirectToAction(nameof(Index));
            }

            DataRow row = ds.Tables[0].Rows[0];
            var transaction = new Transaction
            {
                TransactionID = Convert.ToInt32(row["TransactionID"]),
                CustomerID = Convert.ToInt32(row["CustomerID"]),
                StaffID = Convert.ToInt32(row["StaffID"]),
                TransactionDate = Convert.ToDateTime(row["TransactionDate"]),
                TotalAmount = Convert.ToDecimal(row["TotalAmount"]),
                RemainingBalance = Convert.ToDecimal(row["RemainingBalance"]),
                TransactionTypeID = Convert.ToInt32(row["TransactionTypeID"]),
                CustomerName = row["CustomerName"]?.ToString(),
                StaffName = row["StaffName"]?.ToString(),
                TransactionTypeName = row["TransactionTypeName"]?.ToString()
            };

            // Get sale items if it's a sale
            var saleItems = new List<SaleItem>();
            if (transaction.TransactionTypeID == 1) // SALE
            {
                string itemsQuery = @"
                    SELECT SI.ProductID, SI.Quantity, SI.UnitPrice, SI.TaxRate,
                           SI.SubTotal, SI.TaxAmount, SI.LineTotal,
                           P.Brand AS ProductName, PT.TypeName AS ProductType
                    FROM SaleItem SI
                    INNER JOIN Product P ON SI.ProductID = P.ProductID
                    INNER JOIN ProductTypes PT ON P.ProductTypeID = PT.ProductTypeID
                    WHERE SI.TransactionID = @id";

                DataSet dsItems = _dbcon.getSelectWithParams(itemsQuery, ("@id", id));
                if (dsItems.Tables.Count > 0)
                {
                    foreach (DataRow itemRow in dsItems.Tables[0].Rows)
                    {
                        saleItems.Add(new SaleItem
                        {
                            TransactionID = id,
                            ProductID = Convert.ToInt32(itemRow["ProductID"]),
                            Quantity = Convert.ToInt32(itemRow["Quantity"]),
                            UnitPrice = Convert.ToDecimal(itemRow["UnitPrice"]),
                            TaxRate = Convert.ToDecimal(itemRow["TaxRate"]),
                            SubTotal = Convert.ToDecimal(itemRow["SubTotal"]),
                            TaxAmount = Convert.ToDecimal(itemRow["TaxAmount"]),
                            LineTotal = Convert.ToDecimal(itemRow["LineTotal"]),
                            ProductName = itemRow["ProductName"]?.ToString(),
                            ProductType = itemRow["ProductType"]?.ToString()
                        });
                    }
                }
            }

            // Get payments
            var payments = new List<Payment>();
            string paymentsQuery = @"
                SELECT PaymentID, TransactionID, PaymentDate, AmountPaid, PaymentType
                FROM Payment
                WHERE TransactionID = @id
                ORDER BY PaymentDate DESC";

            DataSet dsPayments = _dbcon.getSelectWithParams(paymentsQuery, ("@id", id));
            if (dsPayments.Tables.Count > 0)
            {
                foreach (DataRow payRow in dsPayments.Tables[0].Rows)
                {
                    payments.Add(new Payment
                    {
                        PaymentID = Convert.ToInt32(payRow["PaymentID"]),
                        TransactionID = Convert.ToInt32(payRow["TransactionID"]),
                        PaymentDate = Convert.ToDateTime(payRow["PaymentDate"]),
                        AmountPaid = Convert.ToDecimal(payRow["AmountPaid"]),
                        PaymentType = payRow["PaymentType"]?.ToString() ?? "Cash"
                    });
                }
            }

            ViewBag.SaleItems = saleItems;
            ViewBag.Payments = payments;

            return View(transaction);
        }

        /// <summary>
        /// Create new sale - form
        /// GET: /Sales/CreateSale
        /// </summary>
        public IActionResult CreateSale()
        {
            LoadDropdowns();
            return View();
        }

        /// <summary>
        /// Create new sale - submit
        /// POST: /Sales/CreateSale
        /// Uses proc_CreateSale stored procedure
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSale(int CustomerID, int[] ProductIDs, int[] Quantities)
        {
            if (CustomerID <= 0)
            {
                TempData["Error"] = "Please select a customer.";
                LoadDropdowns();
                return View();
            }

            if (ProductIDs == null || ProductIDs.Length == 0)
            {
                TempData["Error"] = "Please add at least one product.";
                LoadDropdowns();
                return View();
            }

            try
            {
                // Get current staff ID from session
                var currentUser = HttpContext.Session.GetCurrentUser();
                int staffID = currentUser?.StaffID ?? 1;

                // Create sale using stored procedure with OUTPUT parameter
                // proc_CreateSale has @NewTransactionID OUTPUT parameter
                int newTransactionID = _dbcon.executeStoredProcedureWithOutput(
                    "proc_CreateSale",
                    "@NewTransactionID",
                    ("@CustomerID", CustomerID),
                    ("@StaffID", staffID)
                );

                if (newTransactionID <= 0)
                {
                    TempData["Error"] = "Failed to create sale.";
                    LoadDropdowns();
                    return View();
                }

                // Add items to sale using stored procedure
                for (int i = 0; i < ProductIDs.Length; i++)
                {
                    int productID = ProductIDs[i];
                    int quantity = (Quantities != null && i < Quantities.Length) ? Quantities[i] : 1;

                    if (productID > 0 && quantity > 0)
                    {
                        _dbcon.getStoredProcedure("proc_AddSaleItem",
                            ("@TransactionID", newTransactionID),
                            ("@ProductID", productID),
                            ("@Quantity", quantity),
                            ("@PrescriptionID", DBNull.Value)
                        );
                    }
                }

                TempData["Success"] = "Sale created successfully!";
                return RedirectToAction(nameof(Details), new { id = newTransactionID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating sale: " + ex.Message;
                LoadDropdowns();
                return View();
            }
        }

        /// <summary>
        /// Create new repair - form
        /// GET: /Sales/CreateRepair
        /// </summary>
        public IActionResult CreateRepair()
        {
            LoadCustomerDropdown();
            return View(new CreateRepairViewModel());
        }

        /// <summary>
        /// Create new repair - submit
        /// POST: /Sales/CreateRepair
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateRepair(CreateRepairViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadCustomerDropdown();
                return View(model);
            }

            try
            {
                var currentUser = HttpContext.Session.GetCurrentUser();
                int staffID = currentUser?.StaffID ?? 1;

                // Insert into Transactions (TransactionTypeID = 2 for REPAIR)
                string insertTransaction = @"
                    INSERT INTO Transactions (CustomerID, StaffID, TransactionDate, TotalAmount, RemainingBalance, TransactionTypeID)
                    VALUES (@CustomerID, @StaffID, GETDATE(), @TotalAmount, @TotalAmount, 2);
                    SELECT SCOPE_IDENTITY();";

                int transactionID = _dbcon.executeInsert(insertTransaction,
                    ("@CustomerID", model.CustomerID),
                    ("@StaffID", staffID),
                    ("@TotalAmount", model.RepairCost)
                );

                // Insert into RepairTransaction
                string insertRepair = @"
                    INSERT INTO RepairTransaction (TransactionID, Description, Status)
                    VALUES (@TransactionID, @Description, 'Pending')";

                _dbcon.executeWithParams(insertRepair,
                    ("@TransactionID", transactionID),
                    ("@Description", model.Description)
                );

                TempData["Success"] = "Repair order created successfully!";
                return RedirectToAction(nameof(Details), new { id = transactionID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating repair: " + ex.Message;
                LoadCustomerDropdown();
                return View(model);
            }
        }

        /// <summary>
        /// Delete transaction
        /// POST: /Sales/Delete/5
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            try
            {
                _dbcon.getStoredProcedure("proc_DeleteTransaction", ("@TransactionID", id));
                TempData["Success"] = "Transaction deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting transaction: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private void LoadDropdowns()
        {
            LoadCustomerDropdown();
            LoadProductDropdown();
        }

        private void LoadCustomerDropdown()
        {
            var customers = new List<SelectListItem>();
            DataSet ds = _dbcon.getSelect("SELECT CustomerID, FirstName + ' ' + LastName AS FullName FROM Customer ORDER BY FirstName");
            
            if (ds.Tables.Count > 0)
            {
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    customers.Add(new SelectListItem
                    {
                        Value = row["CustomerID"].ToString(),
                        Text = row["FullName"].ToString()
                    });
                }
            }
            ViewBag.Customers = customers;
        }

        private void LoadProductDropdown()
        {
            var products = new List<SelectListItem>();
            DataSet ds = _dbcon.getSelect(@"
                SELECT P.ProductID, P.Brand + ' - ' + PT.TypeName + ' (' + CAST(P.Price AS VARCHAR) + ' TL)' AS ProductInfo, P.Price, P.StockQuantity
                FROM Product P
                INNER JOIN ProductTypes PT ON P.ProductTypeID = PT.ProductTypeID
                WHERE P.StockQuantity > 0
                ORDER BY P.Brand");
            
            if (ds.Tables.Count > 0)
            {
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    products.Add(new SelectListItem
                    {
                        Value = row["ProductID"].ToString(),
                        Text = row["ProductInfo"].ToString()
                    });
                }
            }
            ViewBag.Products = products;
        }
    }
}


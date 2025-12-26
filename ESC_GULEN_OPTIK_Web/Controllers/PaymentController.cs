using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using ESC_GULEN_OPTIK_Web.Filters;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Payment Controller - Process payments for transactions
    /// Accessible by all logged-in staff
    /// </summary>
    [ServiceFilter(typeof(AuthenticationFilter))]
    public class PaymentController : Controller
    {
        private readonly DBConnection _dbcon;

        public PaymentController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// List all payments
        /// GET: /Payment
        /// </summary>
        public IActionResult Index()
        {
            var payments = new List<dynamic>();

            string query = @"
                SELECT P.PaymentID, P.TransactionID, P.PaymentDate, P.AmountPaid, P.PaymentType,
                       C.FirstName + ' ' + C.LastName AS CustomerName,
                       TT.TypeName AS TransactionType
                FROM Payment P
                INNER JOIN Transactions T ON P.TransactionID = T.TransactionID
                INNER JOIN Customer C ON T.CustomerID = C.CustomerID
                INNER JOIN TransactionTypes TT ON T.TransactionTypeID = TT.TransactionTypeID
                ORDER BY P.PaymentDate DESC";

            DataSet ds = _dbcon.getSelect(query);

            if (ds.Tables.Count > 0)
            {
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    payments.Add(new
                    {
                        PaymentID = Convert.ToInt32(row["PaymentID"]),
                        TransactionID = Convert.ToInt32(row["TransactionID"]),
                        PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                        AmountPaid = Convert.ToDecimal(row["AmountPaid"]),
                        PaymentType = row["PaymentType"]?.ToString() ?? "Cash",
                        CustomerName = row["CustomerName"]?.ToString(),
                        TransactionType = row["TransactionType"]?.ToString()
                    });
                }
            }

            return View(payments);
        }

        /// <summary>
        /// Create payment form
        /// GET: /Payment/Create?transactionId=5
        /// </summary>
        public IActionResult Create(int? transactionId)
        {
            LoadTransactionDropdown();
            LoadPaymentTypes();

            var payment = new Payment();
            if (transactionId.HasValue)
            {
                payment.TransactionID = transactionId.Value;
                
                // Get remaining balance
                DataSet ds = _dbcon.getSelectWithParams(
                    "SELECT RemainingBalance FROM Transactions WHERE TransactionID = @id",
                    ("@id", transactionId.Value));
                
                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    ViewBag.RemainingBalance = Convert.ToDecimal(ds.Tables[0].Rows[0]["RemainingBalance"]);
                }
            }

            return View(payment);
        }

        /// <summary>
        /// Create payment - submit
        /// POST: /Payment/Create
        /// Uses proc_AddPayment stored procedure with Cash/CreditCard subtype support
        /// Trigger trg_UpdateRemainingBalanceOnPayment will update balance
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Payment payment)
        {
            if (!ModelState.IsValid)
            {
                LoadTransactionDropdown();
                LoadPaymentTypes();
                return View(payment);
            }

            try
            {
                // Check remaining balance
                DataSet dsBalance = _dbcon.getSelectWithParams(
                    "SELECT RemainingBalance FROM Transactions WHERE TransactionID = @id",
                    ("@id", payment.TransactionID));

                if (dsBalance.Tables.Count > 0 && dsBalance.Tables[0].Rows.Count > 0)
                {
                    decimal remaining = Convert.ToDecimal(dsBalance.Tables[0].Rows[0]["RemainingBalance"]);
                    
                    if (payment.AmountPaid > remaining)
                    {
                        TempData["Error"] = $"Payment amount ({payment.AmountPaid:C}) exceeds remaining balance ({remaining:C}).";
                        LoadTransactionDropdown();
                        LoadPaymentTypes();
                        ViewBag.RemainingBalance = remaining;
                        return View(payment);
                    }
                }

                // Get current user info - ReceivedBy is ALWAYS the logged-in staff
                var currentUser = HttpContext.Session.GetCurrentUser();
                string receivedBy = currentUser?.FullName ?? "Unknown Staff";
                
                // For CreditCard, still need CardOwner from form

                // Use proc_AddPayment stored procedure for subtype support
                _dbcon.getStoredProcedure("proc_AddPayment",
                    ("@TransactionID", payment.TransactionID),
                    ("@AmountPaid", payment.AmountPaid),
                    ("@PaymentType", payment.PaymentType),
                    ("@ReceivedBy", (object?)receivedBy ?? DBNull.Value),
                    ("@CardOwner", (object?)payment.CardOwner ?? DBNull.Value)
                );

                TempData["Success"] = $"Payment of {payment.AmountPaid:C} recorded successfully!";
                return RedirectToAction("Details", "Sales", new { id = payment.TransactionID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error processing payment: " + ex.Message;
                LoadTransactionDropdown();
                LoadPaymentTypes();
                return View(payment);
            }
        }

        /// <summary>
        /// Quick payment - for transactions with balance
        /// GET: /Payment/QuickPay/5
        /// </summary>
        public IActionResult QuickPay(int id)
        {
            // Get transaction info
            string query = @"
                SELECT T.TransactionID, T.TotalAmount, T.RemainingBalance,
                       C.FirstName + ' ' + C.LastName AS CustomerName,
                       TT.TypeName AS TransactionType
                FROM Transactions T
                INNER JOIN Customer C ON T.CustomerID = C.CustomerID
                INNER JOIN TransactionTypes TT ON T.TransactionTypeID = TT.TransactionTypeID
                WHERE T.TransactionID = @id";

            DataSet ds = _dbcon.getSelectWithParams(query, ("@id", id));

            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "Transaction not found.";
                return RedirectToAction(nameof(Index));
            }

            DataRow row = ds.Tables[0].Rows[0];
            ViewBag.TransactionID = id;
            ViewBag.TotalAmount = Convert.ToDecimal(row["TotalAmount"]);
            ViewBag.RemainingBalance = Convert.ToDecimal(row["RemainingBalance"]);
            ViewBag.CustomerName = row["CustomerName"]?.ToString();
            ViewBag.TransactionType = row["TransactionType"]?.ToString();

            LoadPaymentTypes();

            return View(new Payment { TransactionID = id, AmountPaid = Convert.ToDecimal(row["RemainingBalance"]) });
        }

        /// <summary>
        /// Get transactions with outstanding balance (AJAX)
        /// </summary>
        [HttpGet]
        public IActionResult GetPendingTransactions()
        {
            var transactions = new List<object>();

            string query = @"
                SELECT T.TransactionID, T.TotalAmount, T.RemainingBalance, T.TransactionDate,
                       C.FirstName + ' ' + C.LastName AS CustomerName
                FROM Transactions T
                INNER JOIN Customer C ON T.CustomerID = C.CustomerID
                WHERE T.RemainingBalance > 0
                ORDER BY T.TransactionDate DESC";

            DataSet ds = _dbcon.getSelect(query);

            if (ds.Tables.Count > 0)
            {
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    transactions.Add(new
                    {
                        transactionID = Convert.ToInt32(row["TransactionID"]),
                        totalAmount = Convert.ToDecimal(row["TotalAmount"]),
                        remainingBalance = Convert.ToDecimal(row["RemainingBalance"]),
                        transactionDate = Convert.ToDateTime(row["TransactionDate"]).ToString("dd/MM/yyyy"),
                        customerName = row["CustomerName"]?.ToString()
                    });
                }
            }

            return Json(transactions);
        }

        private void LoadTransactionDropdown()
        {
            var transactions = new List<SelectListItem>();
            
            string query = @"
                SELECT T.TransactionID, 
                       '#' + CAST(T.TransactionID AS VARCHAR) + ' - ' + C.FirstName + ' ' + C.LastName + 
                       ' (Balance: ' + CAST(T.RemainingBalance AS VARCHAR) + ' TL)' AS DisplayText
                FROM Transactions T
                INNER JOIN Customer C ON T.CustomerID = C.CustomerID
                WHERE T.RemainingBalance > 0
                ORDER BY T.TransactionDate DESC";

            DataSet ds = _dbcon.getSelect(query);

            if (ds.Tables.Count > 0)
            {
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    transactions.Add(new SelectListItem
                    {
                        Value = row["TransactionID"].ToString(),
                        Text = row["DisplayText"].ToString()
                    });
                }
            }

            ViewBag.Transactions = transactions;
        }

        private void LoadPaymentTypes()
        {
            // Only Cash and CreditCard have database tables
            ViewBag.PaymentTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Cash", Text = "Cash" },
                new SelectListItem { Value = "CreditCard", Text = "Credit Card" }
            };
        }
    }
}


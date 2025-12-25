using Microsoft.AspNetCore.Mvc;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using ESC_GULEN_OPTIK_Web.Filters;
using System.Diagnostics;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Home Controller - Dashboard page
    /// Requires login to access
    /// </summary>
    [ServiceFilter(typeof(AuthenticationFilter))]
    public class HomeController : Controller
    {
        private readonly DBConnection _dbcon;

        public HomeController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        public IActionResult Index()
        {
            // Get current user info
            var currentUser = HttpContext.Session.GetCurrentUser();
            ViewBag.IsAdmin = currentUser?.IsAdmin ?? false;
            ViewBag.UserName = currentUser?.FullName ?? "User";

            // Get counts using simple queries (like instructor's pattern)
            try
            {
                var dsStaff = _dbcon.getSelect("SELECT COUNT(*) as cnt FROM Staff");
                var dsCustomer = _dbcon.getSelect("SELECT COUNT(*) as cnt FROM Customer");
                var dsProduct = _dbcon.getSelect("SELECT COUNT(*) as cnt FROM Product");
                var dsLowStock = _dbcon.getSelect("SELECT COUNT(*) as cnt FROM Product WHERE StockQuantity < 5");

                ViewBag.StaffCount = dsStaff.Tables[0].Rows[0]["cnt"];
                ViewBag.CustomerCount = dsCustomer.Tables[0].Rows[0]["cnt"];
                ViewBag.ProductCount = dsProduct.Tables[0].Rows[0]["cnt"];
                ViewBag.LowStockCount = dsLowStock.Tables[0].Rows[0]["cnt"];

                // Get today's cash collected (total payments today)
                try
                {
                    var dsTodayCash = _dbcon.getSelect(@"
                        SELECT ISNULL(SUM(AmountPaid), 0) as total 
                        FROM Payment 
                        WHERE CAST(PaymentDate AS DATE) = CAST(GETDATE() AS DATE)");
                    ViewBag.TodayCashCollected = dsTodayCash.Tables[0].Rows[0]["total"];
                }
                catch
                {
                    ViewBag.TodayCashCollected = 0m;
                }

                // Get today's transaction count
                try
                {
                    var dsTodayTx = _dbcon.getSelect(@"
                        SELECT COUNT(*) as cnt 
                        FROM Transactions 
                        WHERE CAST(TransactionDate AS DATE) = CAST(GETDATE() AS DATE)");
                    ViewBag.TodayTransactions = dsTodayTx.Tables[0].Rows[0]["cnt"];
                }
                catch
                {
                    ViewBag.TodayTransactions = 0;
                }

                // Get today's revenue (total amount from transactions today)
                try
                {
                    var dsTodayRevenue = _dbcon.getSelect(@"
                        SELECT ISNULL(SUM(TotalAmount), 0) as total 
                        FROM Transactions 
                        WHERE CAST(TransactionDate AS DATE) = CAST(GETDATE() AS DATE)");
                    ViewBag.TodayRevenue = dsTodayRevenue.Tables[0].Rows[0]["total"];
                }
                catch
                {
                    ViewBag.TodayRevenue = 0m;
                }

                // Get pending payments (total outstanding balance)
                try
                {
                    var dsPending = _dbcon.getSelect("SELECT ISNULL(SUM(RemainingBalance), 0) as total FROM Transactions WHERE RemainingBalance > 0");
                    ViewBag.PendingPayments = dsPending.Tables[0].Rows[0]["total"];
                }
                catch
                {
                    ViewBag.PendingPayments = 0m;
                }
            }
            catch
            {
                ViewBag.StaffCount = 0;
                ViewBag.CustomerCount = 0;
                ViewBag.ProductCount = 0;
                ViewBag.LowStockCount = 0;
                ViewBag.TodayCashCollected = 0m;
                ViewBag.TodayTransactions = 0;
                ViewBag.TodayRevenue = 0m;
                ViewBag.PendingPayments = 0m;
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

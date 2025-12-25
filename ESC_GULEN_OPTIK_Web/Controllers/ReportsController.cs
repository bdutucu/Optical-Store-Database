using Microsoft.AspNetCore.Mvc;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Filters;
using System.Data;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Reports Controller - Admin only reports
    /// Uses stored procedures and views for reporting
    /// </summary>
    [ServiceFilter(typeof(AdminAuthorizationFilter))]
    public class ReportsController : Controller
    {
        private readonly DBConnection _dbcon;

        public ReportsController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// Reports dashboard
        /// GET: /Reports
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Monthly Financial Report
        /// GET: /Reports/MonthlyFinancial
        /// Uses proc_GetMonthlyFinancialReport
        /// </summary>
        public IActionResult MonthlyFinancial(int? month, int? year)
        {
            month ??= DateTime.Now.Month;
            year ??= DateTime.Now.Year;

            ViewBag.SelectedMonth = month;
            ViewBag.SelectedYear = year;

            try
            {
                DataSet ds = _dbcon.getStoredProcedure("proc_GetMonthlyFinancialReport",
                    ("@Month", month.Value),
                    ("@Year", year.Value)
                );

                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    DataRow row = ds.Tables[0].Rows[0];
                    ViewBag.Period = row["Period"]?.ToString();
                    ViewBag.TotalRevenue = Convert.ToDecimal(row["TotalRevenue"]);
                    ViewBag.CashInflow = Convert.ToDecimal(row["CashInflow"]);
                    ViewBag.PendingReceivables = Convert.ToDecimal(row["PendingReceivables"]);
                    ViewBag.TransactionCount = Convert.ToInt32(row["TransactionCount"]);
                }
                else
                {
                    ViewBag.Period = $"{year} - {month}";
                    ViewBag.TotalRevenue = 0m;
                    ViewBag.CashInflow = 0m;
                    ViewBag.PendingReceivables = 0m;
                    ViewBag.TransactionCount = 0;
                }
            }
            catch
            {
                ViewBag.Period = $"{year} - {month}";
                ViewBag.TotalRevenue = 0m;
                ViewBag.CashInflow = 0m;
                ViewBag.PendingReceivables = 0m;
                ViewBag.TransactionCount = 0;
            }

            return View();
        }

        /// <summary>
        /// Staff Performance Report
        /// GET: /Reports/StaffPerformance
        /// Uses proc_StaffPerformanceReport
        /// </summary>
        public IActionResult StaffPerformance(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.Now.AddMonths(-1);
            endDate ??= DateTime.Now;

            ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

            var staffPerformance = new List<dynamic>();

            try
            {
                DataSet ds = _dbcon.getStoredProcedure("proc_StaffPerformanceReport",
                    ("@StartDate", startDate.Value),
                    ("@EndDate", endDate.Value)
                );

                if (ds.Tables.Count > 0)
                {
                    foreach (DataRow row in ds.Tables[0].Rows)
                    {
                        staffPerformance.Add(new
                        {
                            StaffName = row["StaffName"]?.ToString(),
                            TotalTransactions = Convert.ToInt32(row["TotalTransactions"]),
                            TotalRevenueGenerated = Convert.ToDecimal(row["TotalRevenueGenerated"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading report: " + ex.Message;
            }

            return View(staffPerformance);
        }

        /// <summary>
        /// Customer Outstanding Balances
        /// GET: /Reports/CustomerBalances
        /// Uses view_CustomerOutstandingBalances
        /// </summary>
        public IActionResult CustomerBalances()
        {
            var balances = new List<dynamic>();

            try
            {
                DataSet ds = _dbcon.getSelect(@"
                    SELECT CustomerID, CustomerName, TotalOutstanding, LastTransactionDate, TransactionCount
                    FROM view_CustomerOutstandingBalances
                    WHERE TotalOutstanding > 0
                    ORDER BY TotalOutstanding DESC");

                if (ds.Tables.Count > 0)
                {
                    foreach (DataRow row in ds.Tables[0].Rows)
                    {
                        balances.Add(new
                        {
                            CustomerID = Convert.ToInt32(row["CustomerID"]),
                            CustomerName = row["CustomerName"]?.ToString(),
                            TotalOutstanding = Convert.ToDecimal(row["TotalOutstanding"]),
                            LastTransactionDate = row["LastTransactionDate"] != DBNull.Value 
                                ? Convert.ToDateTime(row["LastTransactionDate"]) 
                                : (DateTime?)null,
                            TransactionCount = Convert.ToInt32(row["TransactionCount"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading balances: " + ex.Message;
            }

            return View(balances);
        }

        /// <summary>
        /// Inventory Report - Total stock value calculation
        /// GET: /Reports/Inventory
        /// </summary>
        public IActionResult Inventory()
        {
            var products = new List<dynamic>();
            decimal totalValue = 0;
            int totalItems = 0;

            try
            {
                DataSet ds = _dbcon.getSelect(@"
                    SELECT P.ProductID, P.Brand, PT.TypeName AS ProductType, 
                           P.StockQuantity, P.Price,
                           (P.StockQuantity * P.Price) AS StockValue
                    FROM Product P
                    INNER JOIN ProductTypes PT ON P.ProductTypeID = PT.ProductTypeID
                    ORDER BY StockValue DESC");

                if (ds.Tables.Count > 0)
                {
                    foreach (DataRow row in ds.Tables[0].Rows)
                    {
                        var stockValue = Convert.ToDecimal(row["StockValue"]);
                        var stockQty = Convert.ToInt32(row["StockQuantity"]);
                        
                        totalValue += stockValue;
                        totalItems += stockQty;

                        products.Add(new
                        {
                            ProductID = Convert.ToInt32(row["ProductID"]),
                            Brand = row["Brand"]?.ToString(),
                            ProductType = row["ProductType"]?.ToString(),
                            StockQuantity = stockQty,
                            Price = Convert.ToDecimal(row["Price"]),
                            StockValue = stockValue
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading inventory: " + ex.Message;
            }

            ViewBag.TotalValue = totalValue;
            ViewBag.TotalItems = totalItems;
            ViewBag.ProductCount = products.Count;

            return View(products);
        }

        /// <summary>
        /// Daily Summary
        /// GET: /Reports/DailySummary
        /// Uses view_DailyFinancialSummary
        /// </summary>
        public IActionResult DailySummary(DateTime? date)
        {
            date ??= DateTime.Today;
            ViewBag.SelectedDate = date.Value.ToString("yyyy-MM-dd");

            try
            {
                DataSet ds = _dbcon.getSelectWithParams(@"
                    SELECT ReportDate, TotalRevenue, CashCollected, TotalRemainingBalance, TotalTransactionCount
                    FROM view_DailyFinancialSummary
                    WHERE ReportDate = @date",
                    ("@date", date.Value.Date));

                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    DataRow row = ds.Tables[0].Rows[0];
                    ViewBag.TotalRevenue = Convert.ToDecimal(row["TotalRevenue"]);
                    ViewBag.CashCollected = Convert.ToDecimal(row["CashCollected"]);
                    ViewBag.TotalRemainingBalance = Convert.ToDecimal(row["TotalRemainingBalance"]);
                    ViewBag.TotalTransactionCount = Convert.ToInt32(row["TotalTransactionCount"]);
                }
                else
                {
                    ViewBag.TotalRevenue = 0m;
                    ViewBag.CashCollected = 0m;
                    ViewBag.TotalRemainingBalance = 0m;
                    ViewBag.TotalTransactionCount = 0;
                }
            }
            catch
            {
                ViewBag.TotalRevenue = 0m;
                ViewBag.CashCollected = 0m;
                ViewBag.TotalRemainingBalance = 0m;
                ViewBag.TotalTransactionCount = 0;
            }

            return View();
        }
    }
}


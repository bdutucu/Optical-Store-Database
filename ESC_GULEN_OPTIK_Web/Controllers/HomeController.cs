using Microsoft.AspNetCore.Mvc;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using System.Diagnostics;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Home Controller - Dashboard page
    /// </summary>
    public class HomeController : Controller
    {
        private readonly DBConnection _dbcon;

        public HomeController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        public IActionResult Index()
        {
            // Get counts using simple queries (like instructor's pattern)
            try
            {
                var dsStaff = _dbcon.getSelect("SELECT COUNT(*) as cnt FROM Staff");
                var dsCustomer = _dbcon.getSelect("SELECT COUNT(*) as cnt FROM Customer");
                var dsProduct = _dbcon.getSelect("SELECT COUNT(*) as cnt FROM Product");
                var dsValue = _dbcon.getSelect("SELECT ISNULL(SUM(Price * StockQuantity), 0) as val FROM Product");

                ViewBag.StaffCount = dsStaff.Tables[0].Rows[0]["cnt"];
                ViewBag.CustomerCount = dsCustomer.Tables[0].Rows[0]["cnt"];
                ViewBag.ProductCount = dsProduct.Tables[0].Rows[0]["cnt"];
                ViewBag.InventoryValue = dsValue.Tables[0].Rows[0]["val"];
            }
            catch
            {
                ViewBag.StaffCount = 0;
                ViewBag.CustomerCount = 0;
                ViewBag.ProductCount = 0;
                ViewBag.InventoryValue = 0;
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

using Microsoft.AspNetCore.Mvc;
using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Models;
using System.Data;
using System.Text.Json;

namespace ESC_GULEN_OPTIK_Web.Controllers
{
    /// <summary>
    /// Authentication Controller - Login/Logout operations
    /// Uses proc_StaffLogin stored procedure
    /// </summary>
    public class AuthController : Controller
    {
        private readonly DBConnection _dbcon;

        public AuthController(DBConnection dbcon)
        {
            _dbcon = dbcon;
        }

        /// <summary>
        /// Show login page
        /// GET: /Auth/Login
        /// </summary>
        public IActionResult Login(string? returnUrl = null)
        {
            // If already logged in, redirect to home
            if (HttpContext.Session.GetString("UserSession") != null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        /// <summary>
        /// Handle login form submission
        /// POST: /Auth/Login
        /// Uses proc_StaffLogin stored procedure
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Call proc_StaffLogin stored procedure
                // Note: In production, password should be hashed before sending
                DataSet ds = _dbcon.getStoredProcedure("proc_StaffLogin",
                    ("@Email", model.Email),
                    ("@PasswordHash", model.Password) // In real app, hash this first
                );

                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    DataRow row = ds.Tables[0].Rows[0];
                    
                    // Check if login was successful
                    string loginStatus = row["LoginStatus"]?.ToString() ?? "";
                    if (loginStatus == "SUCCESS")
                    {
                        // Create user session
                        var userSession = new UserSession
                        {
                            StaffID = Convert.ToInt32(row["StaffID"]),
                            FirstName = row["FirstName"]?.ToString() ?? "",
                            LastName = row["LastName"]?.ToString() ?? "",
                            Position = row["Position"]?.ToString() ?? "",
                            Email = model.Email
                        };

                        // Store in session
                        HttpContext.Session.SetString("UserSession", JsonSerializer.Serialize(userSession));

                        TempData["Success"] = $"Welcome, {userSession.FullName}!";

                        // Redirect to return URL or home
                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        return RedirectToAction("Index", "Home");
                    }
                }

                // Login failed
                TempData["Error"] = "Invalid email or password.";
                return View(model);
            }
            catch
            {
                // proc_StaffLogin throws RAISERROR if user not found
                TempData["Error"] = "Invalid email or password.";
                return View(model);
            }
        }

        /// <summary>
        /// Logout and clear session
        /// GET: /Auth/Logout
        /// </summary>
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction(nameof(Login));
        }

        /// <summary>
        /// Access denied page
        /// GET: /Auth/AccessDenied
        /// </summary>
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}

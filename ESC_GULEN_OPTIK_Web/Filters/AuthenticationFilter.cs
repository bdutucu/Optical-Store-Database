using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ESC_GULEN_OPTIK_Web.Models;
using System.Text.Json;

namespace ESC_GULEN_OPTIK_Web.Filters
{
    /// <summary>
    /// Authentication filter - checks if user is logged in
    /// Apply to controllers/actions that require authentication
    /// </summary>
    public class AuthenticationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session.GetString("UserSession");
            
            if (string.IsNullOrEmpty(session))
            {
                // Not logged in - redirect to login
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Nothing to do after action
        }
    }

    /// <summary>
    /// Admin authorization filter - checks if user is admin
    /// Apply to controllers/actions that require admin role
    /// </summary>
    public class AdminAuthorizationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var sessionJson = context.HttpContext.Session.GetString("UserSession");
            
            if (string.IsNullOrEmpty(sessionJson))
            {
                // Not logged in
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
                return;
            }

            var userSession = JsonSerializer.Deserialize<UserSession>(sessionJson);
            
            if (userSession == null || !userSession.IsAdmin)
            {
                // Not admin - access denied
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Nothing to do after action
        }
    }

    /// <summary>
    /// Extension methods for session management
    /// </summary>
    public static class SessionExtensions
    {
        private const string UserSessionKey = "UserSession";

        /// <summary>
        /// Get current logged in user from session
        /// </summary>
        public static UserSession? GetCurrentUser(this ISession session)
        {
            var sessionJson = session.GetString(UserSessionKey);
            if (string.IsNullOrEmpty(sessionJson))
                return null;

            try
            {
                return JsonSerializer.Deserialize<UserSession>(sessionJson);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Set current user in session
        /// </summary>
        public static void SetCurrentUser(this ISession session, UserSession user)
        {
            session.SetString(UserSessionKey, JsonSerializer.Serialize(user));
        }

        /// <summary>
        /// Clear current user from session
        /// </summary>
        public static void ClearCurrentUser(this ISession session)
        {
            session.Remove(UserSessionKey);
        }

        /// <summary>
        /// Check if user is logged in
        /// </summary>
        public static bool IsLoggedIn(this ISession session)
        {
            return session.GetCurrentUser() != null;
        }

        /// <summary>
        /// Check if current user is admin
        /// </summary>
        public static bool IsAdmin(this ISession session)
        {
            return session.GetCurrentUser()?.IsAdmin ?? false;
        }
    }
}

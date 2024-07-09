using Microsoft.AspNetCore.Mvc;
using HelpdeskApp.Data;
using HelpdeskApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace HelpdeskApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly HelpdeskContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(HelpdeskContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private string GetIpAddress()
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Check for X-Forwarded-For header
            if (HttpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            }

            // Check for X-Real-IP header
            if (string.IsNullOrEmpty(ipAddress) && HttpContext.Request.Headers.ContainsKey("X-Real-IP"))
            {
                ipAddress = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            }

            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            // Convert IPv4-mapped IPv6 addresses to IPv4
            if (ipAddress != null && ipAddress.Contains("::ffff:"))
            {
                ipAddress = ipAddress.Split(':').Last();
            }

            return ipAddress ?? "Unknown IP";
        }


        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            _logger.LogInformation("Login page requested from IP {IP}.", GetIpAddress());
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var ipAddress = GetIpAddress();
            _logger.LogInformation("Login attempt for user {Username} from IP {IP}.", model.Username, ipAddress);

            if (ModelState.IsValid)
            {
                var user = await ValidateUserCredentialsAsync(model.Username, model.Password);

                if (user != null)
                {
                    user.LastLoginTimestamp = DateTime.Now;
                    user.FailedLoginsCount = 0;
                    await _context.SaveChangesAsync();

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Nume),
                        new Claim(ClaimTypes.GivenName, user.Name),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = false,
                    };

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                    HttpContext.Session.SetString("Username", user.Nume);
                    HttpContext.Session.SetString("Name", user.Name);
                    HttpContext.Session.SetInt32("UserId", user.Id);

                    _logger.LogInformation("User {Username} successfully logged in from IP {IP}.", model.Username, ipAddress);
                    return RedirectToAction("Dashboard", "Helpdesk");
                }

                var failedUser = await _context.Users.FirstOrDefaultAsync(u => u.Nume == model.Username);
                if (failedUser != null)
                {
                    failedUser.LastFailedLogin = DateTime.Now;
                    failedUser.FailedLoginsCount = (failedUser.FailedLoginsCount ?? 0) + 1;
                    await _context.SaveChangesAsync();
                }

                TempData["ErrorMessage"] = "Conectare eșuată";
                _logger.LogWarning("Login attempt failed for user {Username} from IP {IP}.", model.Username, ipAddress);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var ipAddress = GetIpAddress();
            _logger.LogInformation("Logout requested from IP {IP}.", ipAddress);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            _logger.LogInformation("User logged out from IP {IP}.", ipAddress);
            return RedirectToAction("Login");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> AddUser()
        {
            var ipAddress = GetIpAddress();
            _logger.LogInformation("AddUser page requested from IP {IP}.", ipAddress);

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == User.Identity.Name);

            if (currentUser != null)
            {
                ViewBag.UserName = currentUser.Name;
            }

            return View();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddUser(AddUserViewModel model)
        {
            var ipAddress = GetIpAddress();
            _logger.LogInformation("AddUser attempt for username {Username} from IP {IP}.", model.Username, ipAddress);

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == User.Identity.Name);

            if (currentUser != null)
            {
                ViewBag.UserName = currentUser.Name;
            }

            if (ModelState.IsValid)
            {
                var sql = @"
                INSERT INTO [HelpDesk].[dbo].[users] 
                (nume, parola, name, ins_time, is_deleted, ins_user_id) 
                VALUES 
                (@Nume, CONVERT(VARCHAR(128), HASHBYTES('SHA2_512', @Parola), 2), @Name, @InsTime, @IsDeleted, @InsUserId)";

                var parameters = new[]
                {
                    new SqlParameter("@Nume", model.Username),
                    new SqlParameter("@Parola", model.Password),
                    new SqlParameter("@Name", model.Name),
                    new SqlParameter("@InsTime", DateTime.Now),
                    new SqlParameter("@IsDeleted", System.Data.SqlDbType.Int) { Value = 0 },
                    new SqlParameter("@InsUserId", currentUser.Id)
                };

                await _context.Database.ExecuteSqlRawAsync(sql, parameters);
                _logger.LogInformation("User {Username} added successfully by {CurrentUser} from IP {IP}.", model.Username, currentUser.Name, ipAddress);
                return RedirectToAction("Index", "Helpdesk");
            }

            _logger.LogWarning("AddUser attempt failed for username {Username} from IP {IP}.", model.Username, ipAddress);
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ChangePassword()
        {
            var ipAddress = GetIpAddress();
            _logger.LogInformation("ChangePassword page requested from IP {IP}.", ipAddress);

            var currentUser = await _context.Users
               .FirstOrDefaultAsync(u => u.Nume == User.Identity.Name);

            if (currentUser != null)
            {
                ViewBag.UserName = currentUser.Name;
            }
            return View();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var ipAddress = GetIpAddress();
            _logger.LogInformation("ChangePassword attempt for user {Username} from IP {IP}.", User.Identity.Name, ipAddress);

            if (ModelState.IsValid)
            {
                var currentUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Nume == User.Identity.Name);

                if (currentUser != null)
                {
                    ViewBag.UserName = currentUser?.Name;
                    var sql = @"
                        UPDATE [HelpDesk].[dbo].[users] 
                        SET parola = CONVERT(VARCHAR(128), HASHBYTES('SHA2_512', @NewPassword), 2), 
                            mod_time = @ModTime, 
                            mod_user_id = @ModUserId 
                        WHERE nume = @Username";

                    var parameters = new[]
                    {
                        new SqlParameter("@NewPassword", model.NewPassword),
                        new SqlParameter("@ModTime", DateTime.Now),
                        new SqlParameter("@ModUserId", currentUser.Id),
                        new SqlParameter("@Username", currentUser.Nume)
                    };

                    await _context.Database.ExecuteSqlRawAsync(sql, parameters);
                    _logger.LogInformation("Password for user {Username} changed successfully by {CurrentUser} from IP {IP}.", currentUser.Nume, currentUser.Name, ipAddress);
                    return RedirectToAction("Index", "Helpdesk");
                }

                _logger.LogWarning("ChangePassword attempt failed: Current user not found from IP {IP}.", ipAddress);
                ModelState.AddModelError(string.Empty, "Current user not found.");
            }

            return View(model);
        }

        private async Task<User> ValidateUserCredentialsAsync(string username, string password)
        {
            var ipAddress = GetIpAddress();
            _logger.LogInformation("Validating credentials for user {Username} from IP {IP}.", username, ipAddress);

            var usernameParam = new SqlParameter("@Username", username);
            var passwordParam = new SqlParameter("@Password", password);

            var users = await _context.Users
                .FromSqlRaw("EXEC ValidateUserCredentials @Username, @Password", usernameParam, passwordParam)
                .ToListAsync();

            var user = users.FirstOrDefault();
            if (user != null)
            {
                _logger.LogInformation("User {Username} credentials validated successfully from IP {IP}.", username, ipAddress);
            }
            else
            {
                _logger.LogWarning("User {Username} credentials validation failed from IP {IP}.", username, ipAddress);
            }

            return user;
        }
    }
}

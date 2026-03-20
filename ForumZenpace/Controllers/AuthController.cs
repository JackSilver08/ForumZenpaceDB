using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ForumZenpace.Models;
using Microsoft.EntityFrameworkCore;
using ForumZenpace.Services;

namespace ForumZenpace.Controllers
{
    public class AuthController : Controller
    {
        private readonly ForumDbContext _context;
        private readonly EmailVerificationService _emailVerificationService;

        public AuthController(ForumDbContext context, EmailVerificationService emailVerificationService)
        {
            _context = context;
            _emailVerificationService = emailVerificationService;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            model.Username = model.Username?.Trim() ?? string.Empty;

            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == model.Username);

            if (user == null || user.Password != model.Password)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account is locked.");
                return View(model);
            }

            if (!user.IsEmailConfirmed)
            {
                var otpResult = await IssueAndSendOtpAsync(user);
                TempData[otpResult.Success ? "AuthSuccessMessage" : "AuthErrorMessage"] = otpResult.Success
                    ? "Tai khoan chua xac thuc email. Chung toi da gui ma OTP moi, vui long nhap ma de tiep tuc."
                    : BuildOtpFailureMessage(otpResult.ErrorMessage);
                return RedirectToAction(nameof(VerifyEmail), new { userId = user.Id });
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "User")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            model.Username = model.Username?.Trim() ?? string.Empty;
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;

            await CleanupExpiredPendingRegistrationsAsync();

            if (!ModelState.IsValid) return View(model);

            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.Username), "Username is already taken.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email da duoc su dung cho mot tai khoan khac.");
                return View(model);
            }

            var pendingRegistration = await FindPendingRegistrationAsync(model);
            if (pendingRegistration is null)
            {
                pendingRegistration = new PendingRegistration
                {
                    Username = model.Username,
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password
                };

                _context.PendingRegistrations.Add(pendingRegistration);
            }
            else
            {
                if (!string.Equals(pendingRegistration.Username, model.Username, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(pendingRegistration.Email, model.Email, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email nay dang duoc dung cho mot yeu cau dang ky khac.");
                    return View(model);
                }

                if (!string.Equals(pendingRegistration.Email, model.Email, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(pendingRegistration.Username, model.Username, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.Username), "Ten tai khoan nay dang duoc dung cho mot yeu cau dang ky khac.");
                    return View(model);
                }

                pendingRegistration.Username = model.Username;
                pendingRegistration.FullName = model.FullName;
                pendingRegistration.Email = model.Email;
                pendingRegistration.Password = model.Password;
                pendingRegistration.UpdatedAt = DateTime.UtcNow;
            }

            var otpResult = await IssueAndSendOtpAsync(pendingRegistration);
            TempData[otpResult.Success ? "AuthSuccessMessage" : "AuthErrorMessage"] = otpResult.Success
                ? "He thong da gui ma OTP toi email cua ban. Hay nhap dung ma de hoan tat dang ky tai khoan."
                : BuildOtpFailureMessage(otpResult.ErrorMessage);

            return RedirectToAction(nameof(VerifyRegistration), new { pendingRegistrationId = pendingRegistration.Id });
        }

        [HttpGet]
        public async Task<IActionResult> VerifyRegistration(int pendingRegistrationId)
        {
            var pendingRegistration = await _context.PendingRegistrations.FindAsync(pendingRegistrationId);
            if (pendingRegistration == null)
            {
                TempData["AuthErrorMessage"] = "Khong tim thay yeu cau dang ky can xac thuc.";
                return RedirectToAction(nameof(Register));
            }

            return View(new VerifyRegistrationOtpViewModel
            {
                PendingRegistrationId = pendingRegistration.Id,
                Username = pendingRegistration.Username,
                EmailMask = EmailVerificationService.MaskEmail(pendingRegistration.Email)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyRegistration(VerifyRegistrationOtpViewModel model)
        {
            var pendingRegistration = await _context.PendingRegistrations.FindAsync(model.PendingRegistrationId);
            if (pendingRegistration == null)
            {
                TempData["AuthErrorMessage"] = "Khong tim thay yeu cau dang ky can xac thuc.";
                return RedirectToAction(nameof(Register));
            }

            model.Username = pendingRegistration.Username;
            model.EmailMask = EmailVerificationService.MaskEmail(pendingRegistration.Email);
            model.OtpCode = (model.OtpCode ?? string.Empty).Trim();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!_emailVerificationService.VerifyOtp(pendingRegistration, model.OtpCode))
            {
                ModelState.AddModelError(nameof(VerifyRegistrationOtpViewModel.OtpCode), "Ma OTP khong dung hoac da het han.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Username == pendingRegistration.Username))
            {
                TempData["AuthErrorMessage"] = "Ten tai khoan nay da duoc dang ky trong luc ban dang xac thuc OTP.";
                _context.PendingRegistrations.Remove(pendingRegistration);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Register));
            }

            if (await _context.Users.AnyAsync(u => u.Email == pendingRegistration.Email))
            {
                TempData["AuthErrorMessage"] = "Email nay da duoc dang ky trong luc ban dang xac thuc OTP.";
                _context.PendingRegistrations.Remove(pendingRegistration);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Register));
            }

            var user = new User
            {
                Username = pendingRegistration.Username,
                FullName = pendingRegistration.FullName,
                Email = pendingRegistration.Email,
                Password = pendingRegistration.Password,
                RoleId = 2,
                IsEmailConfirmed = true
            };

            _context.Users.Add(user);
            _context.PendingRegistrations.Remove(pendingRegistration);
            await _context.SaveChangesAsync();

            TempData["AuthSuccessMessage"] = "Dang ky tai khoan thanh cong. Ban da co the dang nhap vao Zenpace.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendRegistrationOtp(int pendingRegistrationId)
        {
            var pendingRegistration = await _context.PendingRegistrations.FindAsync(pendingRegistrationId);
            if (pendingRegistration == null)
            {
                TempData["AuthErrorMessage"] = "Khong tim thay yeu cau dang ky can gui lai OTP.";
                return RedirectToAction(nameof(Register));
            }

            var otpResult = await IssueAndSendOtpAsync(pendingRegistration);
            TempData[otpResult.Success ? "AuthSuccessMessage" : "AuthErrorMessage"] = otpResult.Success
                ? "Da gui lai ma OTP moi toi email dang ky cua ban."
                : BuildOtpFailureMessage(otpResult.ErrorMessage);

            return RedirectToAction(nameof(VerifyRegistration), new { pendingRegistrationId = pendingRegistration.Id });
        }

        [HttpGet]
        public async Task<IActionResult> VerifyEmail(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["AuthErrorMessage"] = "Khong tim thay tai khoan can xac thuc.";
                return RedirectToAction(nameof(Login));
            }

            if (user.IsEmailConfirmed)
            {
                TempData["AuthSuccessMessage"] = "Email cua tai khoan nay da duoc xac thuc.";
                return RedirectToAction(nameof(Login));
            }

            return View(new VerifyEmailOtpViewModel
            {
                UserId = user.Id,
                EmailMask = EmailVerificationService.MaskEmail(user.Email)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailOtpViewModel model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                TempData["AuthErrorMessage"] = "Khong tim thay tai khoan can xac thuc.";
                return RedirectToAction(nameof(Login));
            }

            model.EmailMask = EmailVerificationService.MaskEmail(user.Email);
            model.OtpCode = (model.OtpCode ?? string.Empty).Trim();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (user.IsEmailConfirmed)
            {
                TempData["AuthSuccessMessage"] = "Email cua tai khoan nay da duoc xac thuc.";
                return RedirectToAction(nameof(Login));
            }

            if (!_emailVerificationService.VerifyOtp(user, model.OtpCode))
            {
                ModelState.AddModelError(nameof(VerifyEmailOtpViewModel.OtpCode), "Ma OTP khong dung hoac da het han.");
                return View(model);
            }

            _emailVerificationService.MarkEmailConfirmed(user);
            await _context.SaveChangesAsync();

            TempData["AuthSuccessMessage"] = "Xac thuc email thanh cong. Ban da co the dang nhap vao Zenpace.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailOtp(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["AuthErrorMessage"] = "Khong tim thay tai khoan can gui lai OTP.";
                return RedirectToAction(nameof(Login));
            }

            if (user.IsEmailConfirmed)
            {
                TempData["AuthSuccessMessage"] = "Email cua tai khoan nay da duoc xac thuc.";
                return RedirectToAction(nameof(Login));
            }

            var otpResult = await IssueAndSendOtpAsync(user);
            TempData[otpResult.Success ? "AuthSuccessMessage" : "AuthErrorMessage"] = otpResult.Success
                ? "Da gui lai ma OTP moi toi email cua ban."
                : BuildOtpFailureMessage(otpResult.ErrorMessage);

            return RedirectToAction(nameof(VerifyEmail), new { userId = user.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        private async Task<PendingRegistration?> FindPendingRegistrationAsync(RegisterViewModel model)
        {
            return await _context.PendingRegistrations.FirstOrDefaultAsync(pr =>
                pr.OtpExpiresAt > DateTime.UtcNow &&
                (pr.Username == model.Username || pr.Email == model.Email));
        }

        private async Task CleanupExpiredPendingRegistrationsAsync()
        {
            var expiredItems = await _context.PendingRegistrations
                .Where(pr => pr.OtpExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredItems.Count == 0)
            {
                return;
            }

            _context.PendingRegistrations.RemoveRange(expiredItems);
            await _context.SaveChangesAsync();
        }

        private async Task<(bool Success, string? ErrorMessage)> IssueAndSendOtpAsync(User user)
        {
            try
            {
                var otpCode = _emailVerificationService.IssueOtp(user);
                await _context.SaveChangesAsync();
                await _emailVerificationService.SendOtpEmailAsync(user, otpCode, HttpContext.RequestAborted);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool Success, string? ErrorMessage)> IssueAndSendOtpAsync(PendingRegistration pendingRegistration)
        {
            try
            {
                var otpCode = _emailVerificationService.IssueOtp(pendingRegistration);
                await _context.SaveChangesAsync();
                await _emailVerificationService.SendOtpEmailAsync(pendingRegistration, otpCode, HttpContext.RequestAborted);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string BuildOtpFailureMessage(string? detail)
        {
            return string.IsNullOrWhiteSpace(detail)
                ? "He thong chua gui duoc OTP luc nay."
                : $"He thong chua gui duoc OTP: {detail}";
        }
    }
}

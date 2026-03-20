using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ForumZenpace.Models;

namespace ForumZenpace.Services
{
    public class EmailVerificationService
    {
        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(15);
        private readonly IEmailSender _emailSender;

        public EmailVerificationService(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        public string IssueOtp(User user)
        {
            var otpCode = GenerateOtpCode();
            user.IsEmailConfirmed = false;
            user.EmailVerificationToken = HashOtp(otpCode);
            user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.Add(OtpLifetime);
            return otpCode;
        }

        public string IssueOtp(PendingRegistration pendingRegistration)
        {
            var otpCode = GenerateOtpCode();
            pendingRegistration.OtpHash = HashOtp(otpCode);
            pendingRegistration.OtpExpiresAt = DateTime.UtcNow.Add(OtpLifetime);
            pendingRegistration.UpdatedAt = DateTime.UtcNow;
            return otpCode;
        }

        public async Task SendOtpEmailAsync(User user, string otpCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(user.EmailVerificationToken) || user.EmailVerificationTokenExpiresAt is null)
            {
                throw new InvalidOperationException("Tai khoan chua co OTP xac thuc hop le.");
            }

            await _emailSender.SendVerificationOtpAsync(
                user.Email,
                GetRecipientName(user.FullName, user.Username),
                otpCode,
                user.EmailVerificationTokenExpiresAt.Value,
                cancellationToken);
        }

        public async Task SendOtpEmailAsync(PendingRegistration pendingRegistration, string otpCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pendingRegistration.OtpHash) || pendingRegistration.OtpExpiresAt <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Yeu cau dang ky tam thoi chua co OTP hop le.");
            }

            await _emailSender.SendVerificationOtpAsync(
                pendingRegistration.Email,
                GetRecipientName(pendingRegistration.FullName, pendingRegistration.Username),
                otpCode,
                pendingRegistration.OtpExpiresAt,
                cancellationToken);
        }

        public bool VerifyOtp(User user, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(user.EmailVerificationToken)
                || user.EmailVerificationTokenExpiresAt is null
                || user.EmailVerificationTokenExpiresAt.Value <= DateTime.UtcNow)
            {
                return false;
            }

            return string.Equals(user.EmailVerificationToken, HashOtp(otpCode), StringComparison.Ordinal);
        }

        public bool VerifyOtp(PendingRegistration pendingRegistration, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(pendingRegistration.OtpHash)
                || pendingRegistration.OtpExpiresAt <= DateTime.UtcNow)
            {
                return false;
            }

            return string.Equals(pendingRegistration.OtpHash, HashOtp(otpCode), StringComparison.Ordinal);
        }

        public void MarkEmailConfirmed(User user)
        {
            user.IsEmailConfirmed = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiresAt = null;
        }

        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                return email;
            }

            var parts = email.Split('@', 2);
            var name = parts[0];
            if (name.Length <= 2)
            {
                return $"{name[0]}*@{parts[1]}";
            }

            return $"{name[..2]}***{name[^1]}@{parts[1]}";
        }

        private static string GenerateOtpCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        }

        private static string GetRecipientName(string? fullName, string? username)
        {
            return string.IsNullOrWhiteSpace(fullName) ? (username ?? string.Empty) : fullName;
        }

        private static string HashOtp(string otpCode)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otpCode));
            return Convert.ToHexString(bytes);
        }
    }
}

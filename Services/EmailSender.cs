using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace HUIOTClinicMS.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public EmailSender(ILogger<EmailSender> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogInformation("Sending email to {Email} with subject {Subject}", email, subject);
            
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var port = _configuration.GetValue<int>("EmailSettings:Port");
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var password = _configuration["EmailSettings:SenderPassword"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogWarning("SMTP Settings are not configured. Email will not be sent.");
                return;
            }

            try
            {
                using var client = new System.Net.Mail.SmtpClient(smtpServer, port)
                {
                    Credentials = new System.Net.NetworkCredential(senderEmail, password),
                    EnableSsl = true,
                    Timeout = 8000
                };
                
                using var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(senderEmail, "HU IoT Clinic"),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {Email}.", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} via SMTP ({SmtpServer}:{Port})", email, smtpServer, port);
                throw;
            }
        }
    }
}

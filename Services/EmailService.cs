using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UserRoles.Data;
using UserRoles.Models;

namespace UserRoles.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<EmailSettings> settings,
            HttpClient httpClient,
            AppDbContext context,
            ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _httpClient = httpClient;
            _context = context;
            _logger = logger;
        }

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string emailType = "Other",
            string? sentByUserId = null,
            int? taskId = null)
        {
            var log = new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                EmailType = emailType,
                SentByUserId = sentByUserId,
                TaskId = taskId,
                SentAt = DateTime.UtcNow
            };

            try
            {
                // ================= BUILD ZEPTO MAIL PAYLOAD =================
                // Follows official ZeptoMail API documentation exactly:
                // https://api.zeptomail.in/v1.1/email
                var payload = new
                {
                    from = new
                    {
                        address = _settings.FromAddress,
                        name = _settings.FromName
                    },
                    to = new[]
                    {
                        new
                        {
                            email_address = new
                            {
                                address = toEmail,
                                name = toEmail.Split('@')[0]   // derive name from email
                            }
                        }
                    },
                    subject = subject,
                    htmlbody = htmlBody,
                    track_clicks = true,
                    track_opens = true
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // ================= BUILD API URL =================
                // BaseUrl should be "https://api.zeptomail.in" — append /v1.1/email
                var apiUrl = _settings.ZeptoMail.BaseUrl.TrimEnd('/');
                if (!apiUrl.Contains("/v1.1/email"))
                    apiUrl += "/v1.1/email";

                // Ensure https:// prefix exists
                if (!apiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !apiUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    apiUrl = "https://" + apiUrl;

                // ================= SET AUTHORIZATION HEADER =================
                // Official format: "Zoho-enczapikey <space> <send mail token>"
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Content = content;

                var token = _settings.ZeptoMail.ApiToken;
                if (token.StartsWith("Zoho-enczapikey ", StringComparison.OrdinalIgnoreCase))
                    token = token.Substring("Zoho-enczapikey ".Length);

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Zoho-enczapikey", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // ================= SEND REQUEST =================
                _logger.LogInformation(
                    "Sending ZeptoMail API request to {ApiUrl} for {ToEmail}",
                    apiUrl, toEmail);

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    log.Status = "Failed";
                    log.ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}"
                        .Substring(0, Math.Min(2000, responseBody.Length + 10));

                    _logger.LogError(
                        "ZeptoMail API failed for {ToEmail}. Status: {StatusCode}. Response: {Response}",
                        toEmail, (int)response.StatusCode, responseBody);
                }
                else
                {
                    log.Status = "Sent";
                    _logger.LogInformation(
                        "Email sent successfully to {ToEmail}. Subject: {Subject}. Response: {Response}",
                        toEmail, subject, responseBody);
                }
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                log.ErrorMessage = ex.Message.Length > 2000
                    ? ex.Message.Substring(0, 2000)
                    : ex.Message;

                _logger.LogError(ex,
                    "Exception while sending email to {ToEmail}. Subject: {Subject}",
                    toEmail, subject);
            }
            finally
            {
                // ================= ALWAYS LOG TO DATABASE =================
                try
                {
                    _context.EmailLogs.Add(log);
                    await _context.SaveChangesAsync();
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx,
                        "Failed to save email log for {ToEmail}. Status: {Status}",
                        toEmail, log.Status);
                }
            }
        }
        public async Task SendEmailSmtpAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string emailType = "Other",
            string? sentByUserId = null,
            string? fromEmail = null,
            string? fromName = null,
            int? taskId = null)
        {
            var log = new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                EmailType = emailType,
                SentByUserId = sentByUserId,
                TaskId = taskId,
                FromEmail = fromEmail,
                SentAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Sending SMTP email via {Server}:{Port} using user credentials for {Username}", _settings.Outlook.SmtpServer, _settings.Outlook.Port, _settings.Outlook.Username);

                using (var client = new System.Net.Mail.SmtpClient(_settings.Outlook.SmtpServer, _settings.Outlook.Port))
                {
                    client.EnableSsl = _settings.Outlook.UseStartTls || _settings.Outlook.UseSsl;
                    client.Credentials = new System.Net.NetworkCredential(_settings.Outlook.Username, _settings.Outlook.Password);

                    var senderAddress = fromEmail ?? _settings.FromAddress;
                    var senderName = fromName ?? _settings.FromName;

                    _logger.LogInformation("SMTP From: {FromAddress} ({FromName}), To: {ToEmail}", senderAddress, senderName, toEmail);

                    var mailMessage = new System.Net.Mail.MailMessage
                    {
                        From = new System.Net.Mail.MailAddress(senderAddress, senderName),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                }

                log.Status = "Sent";
                _logger.LogInformation("Email sent successfully via SMTP to {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                log.ErrorMessage = ex.Message.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message;
                _logger.LogError(ex, "Failed to send email via SMTP to {ToEmail}", toEmail);
                throw; // Re-throw so the controller knows it failed
            }
            finally
            {
                try
                {
                    _context.EmailLogs.Add(log);
                    await _context.SaveChangesAsync();
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Failed to save SMTP email log");
                }
            }
        }
    }
}

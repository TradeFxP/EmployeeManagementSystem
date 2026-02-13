namespace UserRoles.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// Sends an email via ZeptoMail and logs the result to the database.
        /// </summary>
        /// <param name="toEmail">Recipient email address</param>
        /// <param name="subject">Email subject line</param>
        /// <param name="htmlBody">HTML body content</param>
        /// <param name="emailType">Category: AccountCreated, PasswordReset, AdminEmailChange, Other</param>
        /// <param name="sentByUserId">ID of the user who triggered the email (null for system)</param>
        Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string emailType = "Other",
            string? sentByUserId = null);
    }
}

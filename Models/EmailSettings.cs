namespace UserRoles.Models
{
    public class EmailSettings
    {
        public string Provider { get; set; } = "ZeptoMail";
        public string FromName { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public ZeptoMailSettings ZeptoMail { get; set; } = new();
        public OutlookSettings Outlook { get; set; } = new();
    }

    public class ZeptoMailSettings
    {
        public string BaseUrl { get; set; } = "https://api.zeptomail.com/v1.1/email";
        public string ApiToken { get; set; } = string.Empty;
        public string AgentAlias { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
    }

    public class OutlookSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = false;
        public bool UseStartTls { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

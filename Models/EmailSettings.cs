namespace UserRoles.Models
{
    public class EmailSettings
    {
        public string Provider { get; set; } = "ZeptoMail";
        public string FromName { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public ZeptoMailSettings ZeptoMail { get; set; } = new();
    }

    public class ZeptoMailSettings
    {
        public string BaseUrl { get; set; } = "https://api.zeptomail.com/v1.1/email";
        public string ApiToken { get; set; } = string.Empty;
        public string AgentAlias { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
    }
}

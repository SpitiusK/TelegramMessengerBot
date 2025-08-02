// CoreLogic/Models/Models.cs
using System;

namespace CoreLogic.Models
{
    public class AccountConnectionRequest
    {
        public string Name { get; set; }
        public string ApiId { get; set; }
        public string ApiHash { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class ScriptResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
    }

    public class MessageTemplate
    {
        public string Name { get; set; }
        public string Template { get; set; }
        public string[] Parameters { get; set; }
        public string Description { get; set; }
    }
}
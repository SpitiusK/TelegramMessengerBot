// CoreLogic/Models/Models.cs
using System;
using System.Text.Json.Serialization;

namespace CoreLogic.Models
{
    public class AccountConnectionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ApiId { get; set; } = string.Empty;
        public string ApiHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class ScriptResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Message { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Модель шаблона сообщения
    /// Используется как для внутреннего представления, так и для сериализации JSON
    /// </summary>
    public class MessageTemplate
    {
        /// <summary>
        /// Название/ключ шаблона
        /// </summary>
        [JsonIgnore] // Не сохраняется в JSON, поскольку используется как ключ в Dictionary
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Шаблон сообщения с параметрами в формате {parametr1}{parametr2}
        /// </summary>
        [JsonPropertyName("Template")]
        public string Template { get; set; } = string.Empty;

        /// <summary>
        /// Описание назначения шаблона
        /// </summary>
        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Массив параметров, извлеченных из шаблона
        /// </summary>
        [JsonPropertyName("Parameters")]
        public string[] Parameters { get; set; } = Array.Empty<string>();
    }
}
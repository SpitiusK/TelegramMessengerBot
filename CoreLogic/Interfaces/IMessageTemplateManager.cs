// CoreLogic/Interfaces/IMessageTemplateManager.cs
using System;
using System.Collections.Generic;
using CoreLogic.Models;

namespace CoreLogic.Interfaces
{
    /// <summary>
    /// Интерфейс для управления шаблонами сообщений
    /// </summary>
    public interface IMessageTemplateManager
    {
        /// <summary>
        /// События для уведомления об ошибках и изменениях
        /// </summary>
        event Action<string> OnError;
        event Action<string> OnTemplateAdded;
        event Action<string> OnTemplateUpdated;
        event Action<string> OnTemplateDeleted;

        /// <summary>
        /// Добавляет новый шаблон сообщения
        /// </summary>
        bool AddTemplate(string templateName, string template, string description);

        /// <summary>
        /// Обновляет существующий шаблон
        /// </summary>
        bool UpdateTemplate(string templateName, string template, string description);

        /// <summary>
        /// Удаляет шаблон
        /// </summary>
        bool DeleteTemplate(string templateName);

        /// <summary>
        /// Получает конкретный шаблон
        /// </summary>
        MessageTemplate GetTemplate(string templateName);

        /// <summary>
        /// Получает все шаблоны
        /// </summary>
        IReadOnlyList<MessageTemplate> GetAllTemplates();

        /// <summary>
        /// Получает список названий всех шаблонов
        /// </summary>
        IReadOnlyList<string> GetTemplateNames();

        /// <summary>
        /// Проверяет существование шаблона
        /// </summary>
        bool TemplateExists(string templateName);

        /// <summary>
        /// Перезагружает шаблоны из файла
        /// </summary>
        bool ReloadTemplates();
    }
}
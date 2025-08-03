// CoreLogic/MessageTemplateManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using CoreLogic.Models;
using CoreLogic.Interfaces;

namespace CoreLogic
{
    /// <summary>
    /// Менеджер для управления шаблонами сообщений
    /// Отвечает за загрузку, сохранение и манипуляцию шаблонами
    /// </summary>
    public sealed class MessageTemplateManager : IMessageTemplateManager
    {
        #region Private Fields

        private readonly string _templatesFilePath;
        private readonly Regex _parameterRegex;
        private Dictionary<string, MessageTemplate> _templates;
        private readonly JsonSerializerOptions _jsonOptions;

        #endregion

        #region Events

        public event Action<string>? OnError;
        public event Action<string>? OnTemplateAdded;
        public event Action<string>? OnTemplateUpdated;
        public event Action<string>? OnTemplateDeleted;

        #endregion

        #region Constructor

        public MessageTemplateManager(string? templatesFilePath = null)
        {
            _templatesFilePath = templatesFilePath ?? Constants.MESSAGE_TEMPLATES_FILE;
            _parameterRegex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);
            _templates = new Dictionary<string, MessageTemplate>();
            
            _jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNameCaseInsensitive = true
            };

            LoadTemplates();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Добавляет новый шаблон сообщения
        /// </summary>
        /// <param name="templateName">Название шаблона (ключ)</param>
        /// <param name="template">Шаблон с параметрами в формате {parametr1}{parametr2}</param>
        /// <param name="description">Описание шаблона</param>
        /// <returns>True если шаблон успешно добавлен</returns>
        public bool AddTemplate(string templateName, string template, string description)
        {
            if (!ValidateTemplateInput(templateName, template))
                return false;

            try
            {
                var parameters = ExtractParametersFromTemplate(template);
                var isUpdate = _templates.ContainsKey(templateName);

                var templateData = new MessageTemplate
                {
                    Name = templateName,
                    Template = template,
                    Description = description ?? string.Empty,
                    Parameters = parameters
                };

                _templates[templateName] = templateData;

                if (!SaveTemplates())
                    return false;

                if (isUpdate)
                {
                    OnTemplateUpdated?.Invoke($"Шаблон '{templateName}' успешно обновлен");
                }
                else
                {
                    OnTemplateAdded?.Invoke($"Шаблон '{templateName}' успешно добавлен");
                }

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка добавления шаблона: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Обновляет существующий шаблон
        /// </summary>
        public bool UpdateTemplate(string templateName, string template, string description)
        {
            if (!_templates.ContainsKey(templateName))
            {
                OnError?.Invoke($"Шаблон '{templateName}' не найден");
                return false;
            }

            return AddTemplate(templateName, template, description);
        }

        /// <summary>
        /// Удаляет шаблон
        /// </summary>
        public bool DeleteTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                OnError?.Invoke("Название шаблона не может быть пустым");
                return false;
            }

            if (!_templates.ContainsKey(templateName))
            {
                OnError?.Invoke($"Шаблон '{templateName}' не найден");
                return false;
            }

            try
            {
                _templates.Remove(templateName);

                if (SaveTemplates())
                {
                    OnTemplateDeleted?.Invoke($"Шаблон '{templateName}' успешно удален");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка удаления шаблона: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получает конкретный шаблон
        /// </summary>
        public MessageTemplate? GetTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return null;

            return _templates.TryGetValue(templateName, out var template) 
                ? CloneTemplate(template) 
                : null;
        }

        /// <summary>
        /// Получает все шаблоны
        /// </summary>
        public IReadOnlyList<MessageTemplate> GetAllTemplates()
        {
            return _templates.Values
                .Select(CloneTemplate)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Получает список названий всех шаблонов
        /// </summary>
        public IReadOnlyList<string> GetTemplateNames()
        {
            return _templates.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Проверяет существование шаблона
        /// </summary>
        public bool TemplateExists(string templateName)
        {
            return !string.IsNullOrWhiteSpace(templateName) && 
                   _templates.ContainsKey(templateName);
        }

        /// <summary>
        /// Перезагружает шаблоны из файла
        /// </summary>
        public bool ReloadTemplates()
        {
            try
            {
                LoadTemplates();
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка перезагрузки шаблонов: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Загружает шаблоны из JSON файла
        /// </summary>
        private void LoadTemplates()
        {
            try
            {
                if (!File.Exists(_templatesFilePath))
                {
                    _templates = new Dictionary<string, MessageTemplate>();
                    return;
                }

                var json = File.ReadAllText(_templatesFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _templates = new Dictionary<string, MessageTemplate>();
                    return;
                }

                var templatesFromFile = JsonSerializer.Deserialize<Dictionary<string, MessageTemplate>>(json, _jsonOptions);
                _templates = new Dictionary<string, MessageTemplate>();

                if (templatesFromFile != null)
                {
                    foreach (var kvp in templatesFromFile)
                    {
                        var template = kvp.Value;
                        template.Name = kvp.Key; // Устанавливаем имя из ключа
                        
                        // Обновляем параметры на случай, если они изменились в шаблоне
                        template.Parameters = ExtractParametersFromTemplate(template.Template);
                        
                        _templates[kvp.Key] = template;
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка загрузки шаблонов: {ex.Message}");
                _templates = new Dictionary<string, MessageTemplate>();
            }
        }

        /// <summary>
        /// Сохраняет шаблоны в JSON файл
        /// </summary>
        private bool SaveTemplates()
        {
            try
            {
                // Создаем словарь без поля Name для сериализации
                var templatesForSave = _templates.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new MessageTemplate
                    {
                        Template = kvp.Value.Template,
                        Description = kvp.Value.Description,
                        Parameters = kvp.Value.Parameters
                    }
                );

                var json = JsonSerializer.Serialize(templatesForSave, _jsonOptions);
                File.WriteAllText(_templatesFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка сохранения шаблонов: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Извлекает параметры из шаблона сообщения
        /// </summary>
        private string[] ExtractParametersFromTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
                return Array.Empty<string>();

            var matches = _parameterRegex.Matches(template);
            
            return matches
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }

        /// <summary>
        /// Валидирует входные данные для шаблона
        /// </summary>
        private bool ValidateTemplateInput(string templateName, string template)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                OnError?.Invoke("Название шаблона не может быть пустым");
                return false;
            }

            if (string.IsNullOrWhiteSpace(template))
            {
                OnError?.Invoke("Шаблон не может быть пустым");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Создает копию шаблона для предотвращения изменения оригинала
        /// </summary>
        private static MessageTemplate CloneTemplate(MessageTemplate original)
        {
            return new MessageTemplate
            {
                Name = original.Name,
                Template = original.Template,
                Description = original.Description,
                Parameters = (string[])original.Parameters.Clone()
            };
        }

        #endregion
    }
}
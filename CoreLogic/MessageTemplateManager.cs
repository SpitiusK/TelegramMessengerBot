// CoreLogic/MessageTemplateManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using CoreLogic.Models;

namespace CoreLogic
{
    public class MessageTemplateManager
    {
        private readonly string _scriptsFilePath;
        private Dictionary<string, MessageTemplateData> _templates;

        public event Action<string> OnError;
        public event Action<string> OnTemplateAdded;
        public event Action<string> OnTemplateUpdated;
        public event Action<string> OnTemplateDeleted;

        public MessageTemplateManager(string scriptsFilePath = "messageScripts.json")
        {
            _scriptsFilePath = scriptsFilePath;
            LoadTemplates();
        }

        /// <summary>
        /// Добавляет новый шаблон сообщения
        /// </summary>
        /// <param name="templateName">Название шаблона (ключ)</param>
        /// <param name="template">Шаблон с параметрами в формате {parametr1}{parametr2}</param>
        /// <param name="description">Описание шаблона</param>
        /// <returns>True если шаблон успешно добавлен, False если произошла ошибка</returns>
        public bool AddTemplate(string templateName, string template, string description)
        {
            try
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

                if (_templates == null)
                {
                    _templates = new Dictionary<string, MessageTemplateData>();
                }

                // Извлекаем параметры из шаблона
                var parameters = ExtractParametersFromTemplate(template);

                var templateData = new MessageTemplateData
                {
                    Template = template,
                    Description = description ?? "",
                    Parameters = parameters
                };

                _templates[templateName] = templateData;

                // Сохраняем в файл
                if (SaveTemplates())
                {
                    OnTemplateAdded?.Invoke($"Шаблон '{templateName}' успешно добавлен");
                    return true;
                }

                return false;
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
            try
            {
                if (!_templates.ContainsKey(templateName))
                {
                    OnError?.Invoke($"Шаблон '{templateName}' не найден");
                    return false;
                }

                return AddTemplate(templateName, template, description); // AddTemplate обновит существующий
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка обновления шаблона: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Удаляет шаблон
        /// </summary>
        public bool DeleteTemplate(string templateName)
        {
            try
            {
                if (_templates == null || !_templates.ContainsKey(templateName))
                {
                    OnError?.Invoke($"Шаблон '{templateName}' не найден");
                    return false;
                }

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
        public MessageTemplate GetTemplate(string templateName)
        {
            if (_templates == null || !_templates.TryGetValue(templateName, out var templateData))
            {
                return null;
            }

            return new MessageTemplate
            {
                Name = templateName,
                Template = templateData.Template,
                Description = templateData.Description,
                Parameters = templateData.Parameters
            };
        }

        /// <summary>
        /// Получает все шаблоны
        /// </summary>
        public List<MessageTemplate> GetAllTemplates()
        {
            if (_templates == null)
            {
                return new List<MessageTemplate>();
            }

            return _templates.Select(kvp => new MessageTemplate
            {
                Name = kvp.Key,
                Template = kvp.Value.Template,
                Description = kvp.Value.Description,
                Parameters = kvp.Value.Parameters
            }).ToList();
        }

        /// <summary>
        /// Получает список названий всех шаблонов
        /// </summary>
        public List<string> GetTemplateNames()
        {
            return _templates?.Keys.ToList() ?? new List<string>();
        }

        /// <summary>
        /// Проверяет существование шаблона
        /// </summary>
        public bool TemplateExists(string templateName)
        {
            return _templates?.ContainsKey(templateName) ?? false;
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

        #region Private Methods

        private void LoadTemplates()
        {
            try
            {
                if (!File.Exists(_scriptsFilePath))
                {
                    // Создаем файл с базовыми шаблонами если его нет
                    _templates = CreateDefaultTemplates();
                    SaveTemplates();
                    return;
                }

                var json = File.ReadAllText(_scriptsFilePath);
                
                // Пытаемся загрузить в новом формате
                try
                {
                    _templates = JsonSerializer.Deserialize<Dictionary<string, MessageTemplateData>>(json) 
                                ?? new Dictionary<string, MessageTemplateData>();
                }
                catch
                {
                    // Если не получилось, пытаемся загрузить в старом формате (простые строки)
                    var oldFormat = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (oldFormat != null)
                    {
                        _templates = ConvertFromOldFormat(oldFormat);
                        SaveTemplates(); // Сохраняем в новом формате
                    }
                    else
                    {
                        _templates = new Dictionary<string, MessageTemplateData>();
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка загрузки шаблонов: {ex.Message}");
                _templates = new Dictionary<string, MessageTemplateData>();
            }
        }

        private bool SaveTemplates()
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var json = JsonSerializer.Serialize(_templates, options);
                File.WriteAllText(_scriptsFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка сохранения шаблонов: {ex.Message}");
                return false;
            }
        }

        private string[] ExtractParametersFromTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
                return new string[0];

            var regex = new Regex(@"\{([^}]+)\}");
            var matches = regex.Matches(template);
            
            return matches.Cast<Match>()
                          .Select(m => m.Groups[1].Value)
                          .Distinct()
                          .ToArray();
        }

        private Dictionary<string, MessageTemplateData> CreateDefaultTemplates()
        {
            return new Dictionary<string, MessageTemplateData>
            {
                ["no_reply"] = new MessageTemplateData
                {
                    Template = "Здравствуйте, {Имя}, вы не ответили на предыдущее сообщение...",
                    Description = "Сообщение для напоминания о неотвеченном сообщении",
                    Parameters = new[] { "Имя" }
                },
                ["first_message"] = new MessageTemplateData
                {
                    Template = "Здравствуйте, {Имя}, приглашаем вас на встречу {Дата}...",
                    Description = "Первое сообщение для начала общения",
                    Parameters = new[] { "Имя", "Дата" }
                },
                ["reminder"] = new MessageTemplateData
                {
                    Template = "Напоминаем, {Имя}, о встрече {Дата}. Просим подтвердить участие.",
                    Description = "Напоминание о встрече или событии",
                    Parameters = new[] { "Имя", "Дата" }
                },
                ["follow_up"] = new MessageTemplateData
                {
                    Template = "Добрый день, {Имя}! Следуем по нашей договоренности от {Дата}.",
                    Description = "Последующее сообщение по договоренности",
                    Parameters = new[] { "Имя", "Дата" }
                },
                ["custom_greeting"] = new MessageTemplateData
                {
                    Template = "Привет, {Имя}! Как дела? Не забыл про {Дата}?",
                    Description = "Персонализированное приветствие",
                    Parameters = new[] { "Имя", "Дата" }
                }
            };
        }

        private Dictionary<string, MessageTemplateData> ConvertFromOldFormat(Dictionary<string, string> oldTemplates)
        {
            var newTemplates = new Dictionary<string, MessageTemplateData>();
            
            foreach (var kvp in oldTemplates)
            {
                newTemplates[kvp.Key] = new MessageTemplateData
                {
                    Template = kvp.Value,
                    Description = GetDefaultDescription(kvp.Key),
                    Parameters = ExtractParametersFromTemplate(kvp.Value)
                };
            }
            
            return newTemplates;
        }

        private string GetDefaultDescription(string templateName)
        {
            return templateName switch
            {
                "no_reply" => "Сообщение для напоминания о неотвеченном сообщении",
                "first_message" => "Первое сообщение для начала общения",
                "reminder" => "Напоминание о встрече или событии",
                "follow_up" => "Последующее сообщение по договоренности",
                "custom_greeting" => "Персонализированное приветствие",
                _ => $"Шаблон сообщения '{templateName}'"
            };
        }

        #endregion
    }

    /// <summary>
    /// Внутренний класс для хранения данных шаблона в JSON
    /// </summary>
    internal class MessageTemplateData
    {
        public string Template { get; set; }
        public string Description { get; set; }
        public string[] Parameters { get; set; }
    }
}
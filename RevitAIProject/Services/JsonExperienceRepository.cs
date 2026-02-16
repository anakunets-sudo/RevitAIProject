using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitAIProject.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RevitAIProject.Services
{
    // <summary>
    /// Service for managing AI experience data in a local JSON file.
    /// </summary>
    public class JsonExperienceRepository : IExperienceRepository
    {
        private readonly string _filePath;
        private const int MaxHistory = 50; // Лимит записей для актуальности

        public JsonExperienceRepository()
        {
            // Путь: папка плагина / user_experience.json
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            _filePath = Path.Combine(Path.GetDirectoryName(assemblyPath), "user_experience.json");
        }

        public void Save(ExperienceRecord record)
        {
            try
            {
                // 1. Не сохраняем нейтральные отзывы (0), чтобы не засорять базу обучения
                if (record.Rating == 0) return;

                // 2. ФОРМАТИРОВАНИЕ: Превращаем строку JSON в объект JToken
                // Это позволит записать "AiJson" как структуру, а не как текст в кавычках
                if (record.AiJson is string jsonString)
                {
                    try
                    {
                        record.AiJson = JToken.Parse(jsonString);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[JSON Parse Error]: {ex.Message}");
                        // Если это не валидный JSON, оставляем как есть
                    }
                }

                var allRecords = GetAll();
                allRecords.Add(record);

                // 3. Оставляем только свежие записи (Rolling Buffer)
                // Сначала удаляем записи с AiJson == null (если такие просочились)
                var cleaned = allRecords
                    .Where(r => r.AiJson != null)
                    .OrderByDescending(r => r.Timestamp)
                    .Take(MaxHistory)
                    .ToList();

                // 4. Сохраняем с отступами (Formatting.Indented) для человекочитаемости
                string json = JsonConvert.SerializeObject(cleaned, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Experience Save Error]: {ex.Message}");
            }
        }

        private List<ExperienceRecord> GetAll()
        {
            if (!File.Exists(_filePath)) return new List<ExperienceRecord>();
            try
            {
                return JsonConvert.DeserializeObject<List<ExperienceRecord>>(File.ReadAllText(_filePath))
                       ?? new List<ExperienceRecord>();
            }
            catch { return new List<ExperienceRecord>(); }
        }

        public List<ExperienceRecord> GetLearningSet(int limit = 10)
        {
            return GetAll().Take(limit).ToList();
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace RevitAIProject.Services
{
    public class VoiceService
    {
        public event Action<string> OnTextRecognized;
        public event Action<string> OnPartialTextReceived;
        private Process _voiceProcess;
        private TaskCompletionSource<bool> _processExitTcs; // Хелпер для асинхронного ожидания

        public void Start()
        {
            if (_voiceProcess != null) { Task.Run(() => StopAsync()); }

            string dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string exePath = Path.Combine(dllPath, "services", "VoskVoiceHost.exe");
            string modelPath = Path.Combine(dllPath, "models", "vosk-model-small-ru-0.22");

            if (!File.Exists(exePath))
            {
                System.Windows.MessageBox.Show("EXE не найден: " + exePath);
                return;
            }

            _voiceProcess = new Process();

            // ОШИБКА БЫЛА ТУТ: Нужно обязательно заполнить StartInfo
            _voiceProcess.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"{modelPath}", // Передаем путь к модели аргументом
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            // Подписываемся на события ДО запуска
            _voiceProcess.OutputDataReceived += (s, e) =>
            {
                //System.Windows.MessageBox.Show("Данные дошли: " + e.Data);

                // 1. Проверка на пустые строки (защита от падения Revit)
                if (string.IsNullOrWhiteSpace(e.Data) || e.Data.Length < 3) return;

                try
                {
                    string prefix = e.Data.Substring(0, 2); // "P:" или "F:"
                    string jsonContent = e.Data.Substring(2);

                    // 2. Используем dynamic для парсинга
                    dynamic result = JsonConvert.DeserializeObject(jsonContent);
                    if (result == null) return;

                    if (prefix == "P:")
                    {
                        // У Vosk промежуточный результат лежит в поле "partial"
                        string partialText = result.partial;
                        if (!string.IsNullOrEmpty(partialText))
                        {
                            OnPartialTextReceived?.Invoke(partialText);
                        }
                    }
                    else if (prefix == "F:")
                    {
                        // Финальный результат лежит в поле "text"
                        string finalText = result.text;
                        if (!string.IsNullOrEmpty(finalText))
                        {
                            OnTextRecognized?.Invoke(finalText);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 3. Ловим любые ошибки парсинга, чтобы Revit не "схлопнулся"
                    System.Diagnostics.Debug.WriteLine("Ошибка распознавания: " + ex.Message);
                }
            };

            try
            {
                _voiceProcess.Start();
                // Начинаем чтение ПОСЛЕ старта
                _voiceProcess.BeginOutputReadLine();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Ошибка запуска: " + ex.Message);
            }
        }

        public async Task StopAsync()
        {
            if (_voiceProcess != null && !_voiceProcess.HasExited)
            {
                _voiceProcess.StandardInput.WriteLine(""); // Отправляем Enter в хост
                await Task.Delay(100); // Даем время на сохранение финала
                _voiceProcess.Kill();
            }
        }
    }
}
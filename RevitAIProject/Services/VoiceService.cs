using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace RevitAIProject.Services
{
    public class VoiceService
    {
        public event Action<string> OnTextRecognized;
        public event Action<string> OnPartialTextReceived;
        private Process _voiceProcess;

        public void Start()
        {
            if (_voiceProcess != null) Stop();

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
                Arguments = $"\"{modelPath}\"", // Передаем путь к модели аргументом
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Подписываемся на события ДО запуска
            _voiceProcess.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                if (e.Data.StartsWith("P:"))
                {
                    OnPartialTextReceived?.Invoke(e.Data.Substring(2));
                }
                else if (e.Data.StartsWith("F:"))
                {
                    OnTextRecognized?.Invoke(e.Data.Substring(2));
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

        public void Stop()
        {
            if (_voiceProcess == null || _voiceProcess.HasExited) return;

            _voiceProcess.StandardInput.WriteLine(); // Сигнал к завершению

            // Читаем всё, что EXE успел написать в выходной поток
            string json = _voiceProcess.StandardOutput.ReadToEnd();
            _voiceProcess.WaitForExit();

            if (!string.IsNullOrWhiteSpace(json))
            {
                OnTextRecognized?.Invoke(json);
            }

            _voiceProcess = null;
        }
    }
}
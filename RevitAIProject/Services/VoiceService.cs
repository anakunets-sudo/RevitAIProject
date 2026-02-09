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
            if (_voiceProcess != null) Stop(); // Страховка

            string dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // Путь к EXE в подпапке services
            string exePath = Path.Combine(dllPath, "services", "VoksVoiceHost.exe");
            string modelPath = Path.Combine(dllPath, "models", "vosk-model-small-ru-0.22");

            _voiceProcess.Start();

            _voiceProcess.StartInfo.RedirectStandardOutput = true;

            _voiceProcess.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                if (e.Data.StartsWith("P:"))
                {
                    // Отправляем промежуточный текст (JSON внутри Vosk содержит поле "partial")
                    OnPartialTextReceived?.Invoke(e.Data.Substring(2));
                }
                else if (e.Data.StartsWith("F:"))
                {
                    // Отправляем финальный текст
                    OnTextRecognized?.Invoke(e.Data.Substring(2));
                }
            };

            _voiceProcess.Start();
            _voiceProcess.BeginOutputReadLine(); // КРИТИЧНО для живого чтения
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
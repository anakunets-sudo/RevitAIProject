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

            _voiceProcess = new Process();
            _voiceProcess.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{modelPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true, // Скрываем консоль
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _voiceProcess.Start();

            _voiceProcess.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                if (e.Data.StartsWith("P:"))
                    OnPartialTextReceived?.Invoke(e.Data.Substring(2));
                else if (e.Data.StartsWith("F:"))
                    OnTextRecognized?.Invoke(e.Data.Substring(2));
            };

            _voiceProcess.Start();
            _voiceProcess.BeginOutputReadLine(); // Важно для живого чтения
        }

        public void Stop()
        {
            if (_voiceProcess == null || _voiceProcess.HasExited) return;
            _voiceProcess.StandardInput.WriteLine(); // Сигнал Стоп
            _voiceProcess.WaitForExit(1000); // Даем время на финализацию
            _voiceProcess = null;
        }
    }
}
using Newtonsoft.Json;
using RevitAIProject.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    // Простая модель для десериализации
    public class VoskResponse3
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class VoiceService3
    {
        public event Action<string> OnTextRecognized;
        private Process _voiceProcess;
        private readonly IUIDispatcherHelper _dispatcher;
        private StringBuilder _sessionAccumulator = new StringBuilder(); // Буфер для накопления текста

        public VoiceService3(IUIDispatcherHelper dispatcher) => _dispatcher = dispatcher;

        public void Start()
        {
            StopCurrentProcess();
            _sessionAccumulator.Clear(); // Очищаем перед новой записью

            string dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string exePath = Path.Combine(dllPath, "services", "VoskVoiceHost.exe");
            string modelPath = Path.Combine(dllPath, "models", "vosk-model-small-ru-0.22");

            _voiceProcess = new Process();
            _voiceProcess.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{modelPath}\"",
                WorkingDirectory = Path.Combine(dllPath, "services"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            _voiceProcess.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                try
                {
                    var response = JsonConvert.DeserializeObject<VoskResponse3>(e.Data);
                    if (response != null && !string.IsNullOrWhiteSpace(response.Text))
                    {
                        // СРАЗУ отправляем текст в UI, как только Vosk его распознал
                        _dispatcher.Invoke(() => OnTextRecognized?.Invoke(response.Text));
                    }
                }
                catch { }
            };

            _voiceProcess.Start();
            _voiceProcess.BeginOutputReadLine();
            _voiceProcess.BeginErrorReadLine();
        }

        public async Task StopAsync()
        {
            await Task.Run(() =>
            {
                StopCurrentProcess();

                string finalResult = _sessionAccumulator.ToString().Trim();

                // Передаем накопленный результат в UI один раз
                if (!string.IsNullOrEmpty(finalResult))
                {
                    _dispatcher.Invoke(() => OnTextRecognized?.Invoke(finalResult));
                }
            });
        }

        private void StopCurrentProcess()
        {
            try
            {
                if (_voiceProcess != null && !_voiceProcess.HasExited)
                {
                    _voiceProcess.Kill();
                    _voiceProcess.WaitForExit(500);
                }
            }
            catch { }
            finally { _voiceProcess = null; }
        }
    }
}
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
    public class VoskResponse
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class VoiceService
    {
        public event Action<string> OnTextRecognized;
        private Process _voiceProcess;
        IUIDispatcherHelper _dispatcher;

        public VoiceService(IUIDispatcherHelper dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Start()
        {
            // 1. Жесткая очистка перед стартом
            StopCurrentProcess();

            string dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string exePath = Path.Combine(dllPath, "services", "VoskVoiceHost.exe");
            string modelPath = $"\"{Path.Combine(dllPath, "models", "vosk-model-small-ru-0.22")}\"";
            string logPath = Path.Combine(dllPath, "vosk_errors.log");

            if (!File.Exists(exePath)) return;

            _voiceProcess = new Process();
            _voiceProcess.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{modelPath}\"", // Кавычки для путей с пробелами
                WorkingDirectory = Path.Combine(dllPath, "services"), // КРИТИЧНО: EXE должен "думать", что он в своей папке
                UseShellExecute = false,         // ОБЯЗАТЕЛЬНО false для Redirect
                CreateNoWindow = true,           // Можно поставить false для теста (увидишь окно)
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Логируем системные сообщения Vosk (stderr)
            _voiceProcess.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] VOSK: {e.Data}{Environment.NewLine}"); } catch { }
            };

            // Слушаем только финальный результат (stdout)
            _voiceProcess.OutputDataReceived += (s, e) =>
            {
                // 1. Проверка на пустоту (BeginOutputReadLine шлет null при закрытии процесса)
                if (string.IsNullOrEmpty(e.Data)) return;

                try
                {
                    // Логируем в консоль отладки Revit, чтобы понять, дошла ли строка
                    Debug.WriteLine($"[VOSK_DEBUG] Raw Data: {e.Data}");

                    // 2. Парсим JSON
                    var response = JsonConvert.DeserializeObject<VoskResponse>(e.Data);

                    if (response != null && !string.IsNullOrWhiteSpace(response.Text))
                    {
                        string recognizedText = response.Text.Trim();

                        // 3. ПЕРЕДАЧА В UI (Dispatcher)
                        // Используем Application.Current.Dispatcher, так как событие прилетает из фонового потока процесса
                        //System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                        //{
                        //    OnTextRecognized?.Invoke(recognizedText);
                        //}), System.Windows.Threading.DispatcherPriority.Background);

                        _dispatcher.Invoke(new Action(() =>
                        {
                            OnTextRecognized?.Invoke(recognizedText);
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VOSK_ERROR] Parsing error: {ex.Message} | Data: {e.Data}");
                }
            };

            try
            {
                _voiceProcess.Start();
                _voiceProcess.BeginOutputReadLine();
                _voiceProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Vosk Start Error: " + ex.Message);
            }
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

        public async Task StopAsync()
        {
            await Task.Run(() => StopCurrentProcess());
        }
    }
}
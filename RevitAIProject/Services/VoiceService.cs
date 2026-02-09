using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

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
                Arguments = $"\"{modelPath}\"", // Передаем путь к модели аргументом
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // Подписываемся на события ДО запуска
            _voiceProcess.OutputDataReceived += (s, e) =>
            {
                //System.Diagnostics.Debug.WriteLine("ОТЛАДКА: Пришла строка от EXE: " + e.Data);

                if (string.IsNullOrEmpty(e.Data)) return; // Игнорируем пустые строки

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

        public async Task StopAsync() // Делаем метод асинхронным
        {
            if (_voiceProcess == null || _voiceProcess.HasExited) return;

            _processExitTcs = new TaskCompletionSource<bool>();

            // Подписываемся на событие завершения процесса
            _voiceProcess.EnableRaisingEvents = true;
            _voiceProcess.Exited += (sender, e) => _processExitTcs.TrySetResult(true);

            try
            {
                _voiceProcess.StandardInput.WriteLine(); // Посылаем сигнал "Стоп"

                // Асинхронно ждем завершения (не блокируем Revit)
                await _processExitTcs.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Stop error: " + ex.Message);
            }
            finally
            {
                _voiceProcess.Dispose();
                _voiceProcess = null;
            }
        }
    }
}
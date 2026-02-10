using System;
using System.IO;
using System.Text;
using NAudio.Wave;
using Vosk;

namespace VoskVoiceHost
{
    class Program
    {
        static void Main(string[] args)
        {
            // Устанавливаем UTF8 для корректной передачи кириллицы
            Console.OutputEncoding = Encoding.UTF8;

            // 1. Проверка аргументов
            if (args.Length == 0)
            {
                Console.Error.WriteLine("HOST_ERROR: Путь к модели не передан.");
                return;
            }

            string modelPath = args[0];

            if (!Directory.Exists(modelPath))
            {
                Console.Error.WriteLine($"HOST_ERROR: Модель не найдена: {modelPath}");
                return;
            }

            // Служебная информация в Error (уйдет в лог Revit)
            Console.Error.WriteLine($"HOST_LOG: Модель загружена из {modelPath}");

            // Дебаг аудио-устройств в файл
            try
            {
                var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.txt");
                using (StreamWriter sw = new StreamWriter(debugPath, false))
                {
                    sw.WriteLine($"--- Audio Devices Check at {DateTime.Now} ---");
                    int deviceCount = WaveInEvent.DeviceCount;
                    sw.WriteLine($"Found {deviceCount} devices.");
                    for (int i = 0; i < deviceCount; i++)
                    {
                        var capabilities = WaveInEvent.GetCapabilities(i);
                        sw.WriteLine($"Device {i}: {capabilities.ProductName}, Channels: {capabilities.Channels}");
                    }
                }
            }
            catch { /* Игнорируем ошибки записи дебага */ }

            // 2. Инициализация Vosk и Аудио
            try
            {
                using (var model = new Model(modelPath))
                using (var rec = new VoskRecognizer(model, 16000f))
                using (var waveIn = new WaveInEvent
                {
                    DeviceNumber = 0, // Убедись, что это верный индекс
                    WaveFormat = new WaveFormat(16000, 1) // 16кГц Моно
                })
                {
                    waveIn.DataAvailable += (s, e) =>
                    {
                        if (rec.AcceptWaveform(e.Buffer, e.BytesRecorded))
                        {
                            var result = rec.Result(); // Получаем многострочный JSON от Vosk

                            if (!string.IsNullOrWhiteSpace(result))
                            {
                                // --- ВОТ ЭТОТ БЛОК НУЖЕН ---
                                // Убираем все переносы строк, чтобы Revit получил всё одним куском
                                string cleanJson = result.Replace("\r", "").Replace("\n", "").Trim();

                                // Отправляем ОДНУ строку и СРАЗУ сбрасываем буфер
                                Console.WriteLine(cleanJson);
                                Console.Out.Flush();

                                // Для визуального контроля
                                Console.Title = "Распознано: " + cleanJson;
                                // ---------------------------
                            }
                        }
                    };

                    waveIn.StartRecording();
                    Console.Error.WriteLine("HOST_LOG: Microphone Started. Listening...");

                    // Ждем нажатия Enter (от StopAsync в Revit) или закрытия процесса
                    Console.ReadLine();

                    waveIn.StopRecording();

                    // Финальный сброс (если что-то осталось в буфере)
                    var final = rec.FinalResult();
                    if (!string.IsNullOrWhiteSpace(final) && !final.Contains("\"text\": \"\""))
                    {
                        Console.WriteLine(final);
                        Console.Out.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                // Ошибки исполнения в Error
                Console.Error.WriteLine($"HOST_EXCEPTION: {ex.Message}");
            }
        }
    }
}
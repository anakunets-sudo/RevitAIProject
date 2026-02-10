using System;
using System.IO;
using NAudio.Wave;
using Vosk;

namespace VoskVoiceHost
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Проверка аргументов
            if (args.Length == 0)
            {
                Console.WriteLine("E:Путь к модели не передан.");
                return;
            }

            string modelPath = args[0];

            if (!Directory.Exists(modelPath))
            {
                Console.WriteLine($"E:Модель не найдена: {modelPath}");
                return;
            }

            Console.WriteLine($"E:Готов к работе");

            // 2. Инициализация Vosk и Аудио
            try
            {
                using (Model model = new Model(modelPath))
                using (VoskRecognizer rec = new VoskRecognizer(model, 16000))
                using (WaveInEvent waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 1) })
                {
                    waveIn.DataAvailable += (s, e) =>
                    {
                        if (rec.AcceptWaveform(e.Buffer, e.BytesRecorded))
                        {
                            string result = rec.Result();
                            // Проверяем, что результат не пустой
                            if (!string.IsNullOrWhiteSpace(result))
                            {
                                Console.WriteLine("F:" + result);
                            }
                        }
                        else
                        {
                            string partial = rec.PartialResult();
                            // Проверяем, что промежуточный результат не пустой
                            if (!string.IsNullOrWhiteSpace(partial))
                            {
                                Console.WriteLine("P:" + partial);
                            }
                        }
                        // Выталкиваем данные из буфера консоли немедленно в любом случае
                        Console.Out.Flush();
                    };

                    waveIn.StartRecording();

                    // Ожидаем команды "Стоп" (Enter) из Revit через StandardInput
                    // Если Revit закроется, ReadLine выдаст null и мы выйдем из цикла
                    Console.ReadLine();

                    waveIn.StopRecording();

                    // Отправляем самый последний накопленный результат
                    Console.WriteLine("F:" + rec.FinalResult());
                    Console.Out.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("E:" + ex.Message);
                Console.Out.Flush();
                Console.WriteLine("Нажмите любую клавишу для выхода...");
                Console.ReadKey(); // Даст вам время прочитать текст ошибки при ручном запуске
            }
        }
    }
}
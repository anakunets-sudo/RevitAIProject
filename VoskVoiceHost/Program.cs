using System;
using System.IO;
using NAudio.Wave;
using Vosk;

namespace RevitVoiceHost
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) return;
            string modelPath = args[0];

            using (Model model = new Model(modelPath))
            using (VoskRecognizer rec = new VoskRecognizer(model, 16000))
            using (WaveInEvent waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 1) })
            {
                waveIn.DataAvailable += (s, e) =>
                {
                    if (rec.AcceptWaveform(e.Buffer, e.BytesRecorded))
                    {
                        // Финальный кусок фразы
                        Console.WriteLine("F:" + rec.Result());
                    }
                    else
                    {
                        // Живой ввод (черновик)
                        Console.WriteLine("P:" + rec.PartialResult());
                    }
                };
                waveIn.StartRecording();

                // Ожидаем ЛЮБОГО ввода от Revit в StandardInput для остановки
                Console.ReadLine();

                waveIn.StopRecording();
                // Выводим результат в StandardOutput, который прочитает Revit
                Console.WriteLine(rec.FinalResult());
            }
        }
    }
}
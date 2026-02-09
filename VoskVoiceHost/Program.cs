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
                waveIn.DataAvailable += (s, e) => rec.AcceptWaveform(e.Buffer, e.BytesRecorded);
                waveIn.StartRecording();

                // Ожидаем ЛЮБОГО ввода от Revit в StandardInput для остановки
                Console.ReadLine();

                //waveIn.StopRecording();

                // Выводим результат в StandardOutput, который прочитает Revit
                string result = rec.FinalResult();
                Console.WriteLine(result); // Вывод только одного финального JSON
            }
        }
    }
}
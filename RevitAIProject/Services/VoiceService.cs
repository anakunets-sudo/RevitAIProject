using NAudio.Wave;
using Newtonsoft.Json;
using RevitAIProject.Views;
using System;
using System.IO;
using System.Threading.Tasks;
using Vosk;

public class VoiceService : IDisposable
{
    public event Action<string> OnTextRecognized;
    private Model _model;
    private VoskRecognizer _recognizer;
    private WaveInEvent _waveIn;
    private readonly IUIDispatcherHelper _dispatcher;

    public VoiceService(IUIDispatcherHelper dispatcher)
    {
        _dispatcher = dispatcher;
        string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string modelPath = Path.Combine(Path.GetDirectoryName(dllPath), "models", "vosk-model-small-ru-0.22");

        _model = new Model(modelPath);
        _recognizer = new VoskRecognizer(_model, 16000);
        // Важно: не подписываемся на OnTextRecognized внутри цикла записи!
    }

    public void Start()
    {
        // 1. Сначала жестко чистим старый вход, если он выжил
        if (_waveIn != null)
        {
            try { _waveIn.StopRecording(); } catch { }
            _waveIn.Dispose();
            _waveIn = null;
        }

        _recognizer.Reset();

        // 2. Создаем новый экземпляр
        _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 1) };
        _waveIn.DataAvailable += (s, e) =>
        {
            // Проверка на null, чтобы данные не летели в закрывающийся объект
            if (_waveIn != null)
                _recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded);
        };
        _waveIn.StartRecording();
    }

    public async Task StopAsync()
    {
        await Task.Run(() =>
        {
            if (_waveIn == null) return;

            try
            {
                // 1. Сначала отключаем микрофон, чтобы данные больше не текли
                _waveIn.StopRecording();

                // 2. Даем небольшую паузу (100мс), чтобы буферы Vosk успокоились
                System.Threading.Thread.Sleep(100);

                // 3. Вызываем Vosk в максимально защищенном блоке
                string finalJson = string.Empty;

                // Проверяем, жив ли еще объект распознавателя
                if (_recognizer != null)
                {
                    finalJson = _recognizer.FinalResult();
                }

                if (!string.IsNullOrEmpty(finalJson))
                {
                    var result = JsonConvert.DeserializeObject<VoskResult>(finalJson);
                    if (!string.IsNullOrWhiteSpace(result?.text))
                    {
                        OnTextRecognized?.Invoke(result.text);
                    }
                }
            }
            catch (Exception ex)
            {
                // Ловим любые C# ошибки
                System.Diagnostics.Debug.WriteLine("Vosk Error: " + ex.Message);
            }
            finally
            {
                // 4. ГАРАНТИРОВАННОЕ уничтожение объекта записи
                if (_waveIn != null)
                {
                    _waveIn.Dispose();
                    _waveIn = null;
                }
                // После краша на втором вызове лучше даже ресетнуть сам рекогнайзер
                _recognizer?.Reset();
            }
        });
    }

    public void Dispose()
    {
        _waveIn?.Dispose();
        _recognizer?.Dispose();
        _model?.Dispose();
    }

    private class VoskResult { public string text { get; set; } }
}
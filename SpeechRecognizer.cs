using System.Net.Http.Headers;
using System.Text.Json;
using NAudio.Wave;

namespace Dictator;

public class SpeechRecognizer : IDisposable
{
    public event Action<string>? OnResult;
    public event Action<string>? OnError;
    public event Action<string>? OnStateChanged;

    private WaveInEvent? _waveIn;
    private MemoryStream? _audioStream;
    private WaveFileWriter? _waveWriter;
    private bool _recording = false;

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public void StartRecording()
    {
        try
        {
            _audioStream = new MemoryStream();
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 100
            };
            _waveWriter = new WaveFileWriter(_audioStream, _waveIn.WaveFormat);

            _waveIn.DataAvailable += (_, e) =>
            {
                if (_recording) _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            };

            _recording = true;
            _waveIn.StartRecording();
        }
        catch (Exception ex)
        {
            OnError?.Invoke("Микрофон: " + ex.Message);
        }
    }

    public async Task StopAndRecognize()
    {
        if (!_recording || _waveIn == null) return;

        _recording = false;
        _waveIn.StopRecording();
        _waveWriter?.Flush();

        var audioData = _audioStream?.ToArray();

        _waveWriter?.Dispose(); _waveWriter = null;
        _waveIn.Dispose();      _waveIn = null;
        _audioStream?.Dispose(); _audioStream = null;

        if (audioData == null || audioData.Length < 1000)
        {
            OnError?.Invoke("Слишком короткая запись");
            return;
        }

        OnStateChanged?.Invoke("Отправляю на распознавание...");
        await RecognizeWithYandex(audioData);
    }

    private async Task RecognizeWithYandex(byte[] audioData)
    {
        var apiKey = AppSettings.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            OnError?.Invoke("API ключ не задан");
            return;
        }

        try
        {
            const string url = "https://stt.api.cloud.yandex.net/speech/v1/stt:recognize" +
                               "?lang=ru-RU&format=lpcm&sampleRateHertz=16000";

            // Strip 44-byte WAV header — send raw PCM
            byte[] pcm = new byte[audioData.Length - 44];
            Array.Copy(audioData, 44, pcm, 0, pcm.Length);

            var content = new ByteArrayContent(pcm);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", apiKey);
            request.Content = content;

            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var msg = doc.RootElement.TryGetProperty("error_message", out var em)
                        ? em.GetString() ?? "Ошибка API"
                        : $"HTTP {(int)response.StatusCode}";
                    OnError?.Invoke(msg);
                }
                catch { OnError?.Invoke($"HTTP {(int)response.StatusCode}"); }
                return;
            }

            using var respDoc = JsonDocument.Parse(json);
            if (respDoc.RootElement.TryGetProperty("result", out var result))
                OnResult?.Invoke(result.GetString() ?? "");
            else
                OnError?.Invoke("Пустой ответ от API");
        }
        catch (TaskCanceledException) { OnError?.Invoke("Таймаут запроса"); }
        catch (HttpRequestException ex) { OnError?.Invoke("Сеть: " + ex.Message); }
        catch (Exception ex) { OnError?.Invoke(ex.Message); }
    }

    public void Dispose()
    {
        _recording = false;
        _waveIn?.Dispose();
        _waveWriter?.Dispose();
        _audioStream?.Dispose();
    }
}

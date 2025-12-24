using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using PingPongClient.Core;

namespace PingPongClient.Network
{
    // Простая TCP-клиент обёртка (framed JSON)
    public class TcpClientConnector : IDisposable
    {
        private TcpClient _tcp;
        private NetworkStream _stream;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        public event Action<GameState> OnGameStateReceived;
        public event Action<int> OnWelcome; // playerId

        public bool Connected => _tcp?.Connected ?? false;

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                _tcp = new TcpClient();
                await _tcp.ConnectAsync(ip, port);
                _tcp.NoDelay = true;
                _stream = _tcp.GetStream();
                _ = Task.Run(ReceiveLoopAsync);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connect failed: {ex.Message}");
                return false;
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var lenBuf = new byte[4];
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    int read = 0;
                    while (read < 4)
                    {
                        int r = await _stream.ReadAsync(lenBuf, read, 4 - read, _cts.Token);
                        if (r == 0) throw new Exception("Disconnected");
                        read += r;
                    }
                    int len = BitConverter.ToInt32(lenBuf, 0);
                    if (len <= 0 || len > 1_000_000) throw new Exception("Invalid length");
                    var buf = new byte[len];
                    int received = 0;
                    while (received < len)
                    {
                        int r = await _stream.ReadAsync(buf, received, len - received, _cts.Token);
                        if (r == 0) throw new Exception("Disconnected");
                        received += r;
                    }
                    string json = Encoding.UTF8.GetString(buf);
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            JsonElement t;
                            if (doc.RootElement.TryGetProperty("type", out t))
                            {
                                string type = t.GetString();
                                if (type == "state")
                                {
                                    GameState gs = JsonSerializer.Deserialize<GameState>(json);
                                    OnGameStateReceived?.Invoke(gs);
                                }
                                else if (type == "welcome")
                                {
                                    JsonElement p;
                                    if (doc.RootElement.TryGetProperty("playerId", out p))
                                    {
                                        int pid = p.GetInt32();
                                        OnWelcome?.Invoke(pid);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Malformed JSON: {ex.Message}");
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Disconnected from server.");
            }
        }

        public async Task SendAsync(object obj)
        {
            if (_stream == null) return;
            try
            {
                string json = JsonSerializer.Serialize(obj);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                byte[] len = BitConverter.GetBytes(payload.Length);
                await _stream.WriteAsync(len, 0, 4, _cts.Token);
                await _stream.WriteAsync(payload, 0, payload.Length, _cts.Token);
                await _stream.FlushAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyCafe_Nhom8.Services
{
    public class TcpOrderService
    {
        // ================= PHẦN TCP SERVER (DÀNH CHO FrmMainPOS) =================
        private TcpListener _server;
        private readonly List<TcpClient> _connectedClients = new List<TcpClient>();
        private bool _isServerRunning = false;

        public event Action<string> OnServerReceivedMessage;
        public event Action<int> OnClientCountChanged;

        public void StartServer(int port = 8888)
        {
            if (_isServerRunning) return;
            _isServerRunning = true;

            Task.Run(async () =>
            {
                try
                {
                    _server = new TcpListener(IPAddress.Any, port);
                    _server.Start();

                    while (_isServerRunning)
                    {
                        TcpClient client = await _server.AcceptTcpClientAsync();
                        lock (_connectedClients)
                        {
                            _connectedClients.Add(client);
                        }
                        OnClientCountChanged?.Invoke(_connectedClients.Count);

                        // Lắng nghe phản hồi từ Client (KDS báo hoàn thành món)
                        _ = ListenToClientAsync(client);
                    }
                }
                catch { }
            });
        }

        private async Task ListenToClientAsync(TcpClient client)
        {
            byte[] buffer = new byte[4096];
            try
            {
                NetworkStream stream = client.GetStream();
                while (_isServerRunning && client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    OnServerReceivedMessage?.Invoke(msg);
                }
            }
            catch { }
            finally
            {
                lock (_connectedClients)
                {
                    _connectedClients.Remove(client);
                }
                OnClientCountChanged?.Invoke(_connectedClients.Count);
            }
        }

        // Server bắn gói tin sang tất cả màn hình KDS đang kết nối
        public void BroadcastToClients(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            lock (_connectedClients)
            {
                for (int i = _connectedClients.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (_connectedClients[i].Connected)
                        {
                            NetworkStream stream = _connectedClients[i].GetStream();
                            stream.Write(data, 0, data.Length);
                        }
                    }
                    catch
                    {
                        _connectedClients.RemoveAt(i);
                    }
                }
            }
        }

        public void StopServer()
        {
            _isServerRunning = false;
            try { _server?.Stop(); } catch { }
        }

        // ================= PHẦN TCP CLIENT (DÀNH CHO FrmKDS) =================
        private TcpClient _client;
        private NetworkStream _clientStream;

        public event Action<string> OnClientReceivedMessage;
        public event Action<bool> OnConnectionStatusChanged;

        public bool IsClientConnected => _client != null && _client.Connected;

        public async Task<bool> ConnectToServerAsync(string ip = "127.0.0.1", int port = 8888)
        {
            try
            {
                if (IsClientConnected) return true;

                _client = new TcpClient();
                await _client.ConnectAsync(ip, port);
                _clientStream = _client.GetStream();
                OnConnectionStatusChanged?.Invoke(true);

                // Bắt đầu lắng nghe gói tin từ Server (Thu ngân gửi món mới)
                _ = Task.Run(async () =>
                {
                    byte[] buffer = new byte[4096];
                    try
                    {
                        while (_client != null && _client.Connected)
                        {
                            int bytesRead = await _clientStream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead == 0) break;

                            string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            OnClientReceivedMessage?.Invoke(msg);
                        }
                    }
                    catch { }
                    finally
                    {
                        OnConnectionStatusChanged?.Invoke(false);
                    }
                });

                return true;
            }
            catch
            {
                OnConnectionStatusChanged?.Invoke(false);
                return false;
            }
        }

        // Client (KDS) bắn gói tin ngược về Server (Thu ngân)
        public void SendToServer(string message)
        {
            try
            {
                if (IsClientConnected && _clientStream != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    _clientStream.Write(data, 0, data.Length);
                }
            }
            catch { }
        }
    }
}
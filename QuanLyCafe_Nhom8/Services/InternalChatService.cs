using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuanLyCafe_Nhom8.Network;

namespace QuanLyCafe_Nhom8.Services
{
    public class InternalChatItem
    {
        public string SenderName { get; set; }
        public string SenderRole { get; set; }
        public string Content { get; set; }
        public string SentTime { get; set; }
        public bool IsAlert { get; set; }
    }

    public class InternalChatService
    {
        public static InternalChatService Instance = new InternalChatService();

        private int _port = 9002;
        private List<InternalChatItem> _history = new List<InternalChatItem>();
        private UdpClient _udpReceiver;
        private bool _isRunning = false;

        public event Action<InternalChatItem> OnMessageReceived;

        // Bắt đầu lắng nghe tin nhắn trong mạng LAN
        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            Task.Run(() => ListenMessages());
        }

        public List<InternalChatItem> GetHistory()
        {
            return _history;
        }

        // Gửi tin nhắn đi cho các máy khác trong quán
        public async Task SendMessageAsync(string content, bool isAlert = false)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var user = CafeNetworkClient.Instance.CurrentUser;
            var msg = new InternalChatItem
            {
                SenderName = user != null ? user.FullName : "Quản lý",
                SenderRole = user != null ? user.Role : "Admin",
                Content = content.Trim(),
                SentTime = DateTime.Now.ToString("HH:mm:ss"),
                IsAlert = isAlert
            };

            try
            {
                string json = JsonConvert.SerializeObject(msg);
                byte[] data = Encoding.UTF8.GetBytes(json);

                using (UdpClient udpSender = new UdpClient())
                {
                    udpSender.EnableBroadcast = true;
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, _port);
                    await udpSender.SendAsync(data, data.Length, endPoint);
                }
            }
            catch
            {
                // Nếu mạng lỗi thì vẫn hiện tin nhắn ở máy mình
                AddMessage(msg);
            }
        }

        // Vòng lặp nhận tin nhắn từ các máy khác
        private void ListenMessages()
        {
            try
            {
                _udpReceiver = new UdpClient();
                _udpReceiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpReceiver.Client.Bind(new IPEndPoint(IPAddress.Any, _port));

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                while (_isRunning)
                {
                    byte[] data = _udpReceiver.Receive(ref remoteEP);
                    string json = Encoding.UTF8.GetString(data);
                    var msg = JsonConvert.DeserializeObject<InternalChatItem>(json);

                    if (msg != null)
                    {
                        AddMessage(msg);
                    }
                }
            }
            catch
            {
                _isRunning = false;
            }
        }

        private void AddMessage(InternalChatItem msg)
        {
            _history.Add(msg);
            if (OnMessageReceived != null)
            {
                OnMessageReceived(msg);
            }
        }
    }
}

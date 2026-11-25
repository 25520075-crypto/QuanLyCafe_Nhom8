using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Protocol
{
    public enum MsgType
    {
        // 1. Auth & User Management
        LOGIN_REQ, LOGIN_OK, AUTH_FAIL,
        FORGOT_SEND_OTP_REQ, FORGOT_RESET_PASS_REQ,
        ADMIN_SEND_CREATE_OTP_REQ, ADMIN_CREATE_USER_REQ,
        GET_USERS_REQ, DELETE_USER_REQ, TOGGLE_USER_REQ,
        VERIFY_MANAGER_REQ,

        // 2. Menu & Category
        GET_CATEGORIES_REQ, ADD_CATEGORY_REQ,
        GET_ACTIVE_PRODUCTS_REQ, GET_ALL_PRODUCTS_REQ,
        INSERT_PRODUCT_REQ, UPDATE_PRODUCT_REQ,
        TOGGLE_PRODUCT_REQ, DELETE_PRODUCT_REQ,

        // 3. POS, Bàn, Order & Thanh toán
        GET_TABLES_REQ, GET_BILL_DETAILS_REQ,
        ADD_ITEM_REQ, REMOVE_ITEM_REQ, SEND_KITCHEN_REQ,
        MOVE_TABLE_REQ, MERGE_TABLE_REQ,
        CHECKOUT_REQ, GET_VIETQR_REQ,

        // 4. Quầy Pha Chế (KDS) & Thống kê
        GET_PENDING_KDS_REQ, KDS_COMPLETE_ITEM_REQ,
        STATS_REVENUE_REQ,

        // 5. Server Push Broadcast & System
        TABLE_STATUS_CHANGED, MENU_UPDATED,
        KDS_NEW_ORDER, KDS_ITEM_DONE, CHAT_MESSAGE,
        PING, PONG, ERROR, TABLE_BUSY, PERMISSION_DENIED
    }

    public class PacketMessage
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
        public MsgType Type { get; set; }
        public string PayloadJson { get; set; } = "{}";
        public bool Success { get; set; } = true;
        public string ErrorCode { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public static class FramingHelper
    {
        public const int DefaultTcpPort = 9000;
        public const int DefaultUdpPort = 9001;
        public const string DiscoveryRequest = "DISCOVER_COFFEE_FEFA_SERVER";
        public const string DiscoveryResponsePrefix = "COFFEE_FEFA_SERVER_OK|";
        private const int MaxPacketSize = 1024 * 1024; // Giới hạn 1MB

        public static async Task SendPacketAsync(NetworkStream stream, PacketMessage packet)
        {
            string json = JsonConvert.SerializeObject(packet);
            byte[] payload = Encoding.UTF8.GetBytes(json);
            if (payload.Length > MaxPacketSize)
                throw new InvalidOperationException("Kích thước gói tin vượt quá 1MB.");

            byte[] lengthHeader = BitConverter.GetBytes(payload.Length); // 4-byte Int32
            byte[] buffer = new byte[4 + payload.Length];
            Buffer.BlockCopy(lengthHeader, 0, buffer, 0, 4);
            Buffer.BlockCopy(payload, 0, buffer, 4, payload.Length);

            await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        public static async Task<PacketMessage> ReceivePacketAsync(NetworkStream stream)
        {
            byte[] header = await ReadExactBytesAsync(stream, 4).ConfigureAwait(false);
            if (header == null) return null;

            int payloadLength = BitConverter.ToInt32(header, 0);
            if (payloadLength <= 0 || payloadLength > MaxPacketSize)
                throw new InvalidDataException($"Header độ dài gói không hợp lệ: {payloadLength}");

            byte[] payload = await ReadExactBytesAsync(stream, payloadLength).ConfigureAwait(false);
            if (payload == null) return null;

            string json = Encoding.UTF8.GetString(payload);
            return JsonConvert.DeserializeObject<PacketMessage>(json);
        }

        private static async Task<byte[]> ReadExactBytesAsync(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, totalRead, count - totalRead).ConfigureAwait(false);
                if (bytesRead == 0) return null; // Client đóng kết nối
                totalRead += bytesRead;
            }
            return buffer;
        }
    }
}
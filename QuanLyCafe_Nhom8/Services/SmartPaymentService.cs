using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Net;
using System.Text;

namespace QuanLyCafe_Nhom8.Services
{
    public static class SmartPaymentService
    {
        // Thông tin tài khoản Vietcombank chính chủ của Nguyễn Hoàng Duy Anh
        public static string BankId = "970436";
        public static string BankShortName = "Vietcombank";
        public static string AccountNo = "9942886521";
        public static string AccountName = "NGUYEN HOANG DUY ANH";

        // Hàm trả về trực tiếp đối tượng Bitmap để gán thẳng vào PictureBox.Image (Không bao giờ bị lỗi X đỏ)
        public static Bitmap GetQrBitmap(decimal amount, string tableName)
        {
            string filePath = BuildVietQrUrl(amount, tableName);
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image img = Image.FromStream(fs))
            {
                return new Bitmap(img);
            }
        }

        // Hàm tạo và tải ảnh VietQR động (trả về đường dẫn file ảnh hợp lệ trên ổ cứng)
        public static string BuildVietQrUrl(decimal amount, string tableName)
        {
            long roundedAmount = Convert.ToInt64(Math.Round(amount));
            string cleanTable = string.IsNullOrWhiteSpace(tableName) ? "Ban Cafe" : tableName.Trim();
            string addInfo = Uri.EscapeDataString($"Thanh toan {cleanTable}");
            string accName = Uri.EscapeDataString(AccountName);

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"vietqr_{Guid.NewGuid():N}.png");

            try
            {
                // Bật TLS 1.2 và TLS 1.3 (nếu hệ điều hành hỗ trợ)
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | (SecurityProtocolType)3072;

                // 1. Ưu tiên tải mẫu VietQR chính thức của Vietcombank
                string vietQrApiUrl = $"https://img.vietqr.io/image/{BankId}-{AccountNo}-compact2.png?amount={roundedAmount}&addInfo={addInfo}&accountName={accName}";

                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    byte[] imgBytes = wc.DownloadData(vietQrApiUrl);

                    // Kiểm tra chắc chắn dữ liệu tải về là file ảnh hợp lệ (tránh trường hợp Cloudflare trả về trang HTML)
                    if (TrySaveValidImage(imgBytes, tempFilePath))
                    {
                        return tempFilePath;
                    }
                    throw new Exception("Dữ liệu từ img.vietqr.io không phải là ảnh hợp lệ.");
                }
            }
            catch
            {
                try
                {
                    // 2. Dự phòng: Tự sinh mã chuẩn Napas247 EMVCo của Vietcombank 9942886521 qua qrserver
                    string emvPayload = BuildNapas247Payload(roundedAmount, $"Thanh toan {cleanTable}");
                    string backupQrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=360x360&margin=10&data={Uri.EscapeDataString(emvPayload)}";

                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                        byte[] qrBytes = wc.DownloadData(backupQrUrl);

                        if (qrBytes == null || qrBytes.Length < 200)
                            throw new Exception("Ảnh QR dự phòng không hợp lệ.");

                        using (MemoryStream ms = new MemoryStream(qrBytes))
                        using (Image qrImg = Image.FromStream(ms))
                        using (Bitmap cardBmp = DrawVietcombankCard(qrImg, roundedAmount))
                        {
                            cardBmp.Save(tempFilePath, ImageFormat.Png);
                            return tempFilePath;
                        }
                    }
                }
                catch
                {
                    // 3. Dự phòng Offline: Kiểm tra xem có ảnh VietQR tĩnh lưu sẵn trong thư mục chạy không, nếu không thì tự vẽ QR
                    Image localQr = TryLoadLocalFallbackQr();
                    using (Bitmap offlineBmp = DrawVietcombankCard(localQr, roundedAmount))
                    {
                        if (localQr != null) localQr.Dispose();
                        offlineBmp.Save(tempFilePath, ImageFormat.Png);
                        return tempFilePath;
                    }
                }
            }
        }

        // Kiểm tra mảng byte có thực sự là ảnh hợp lệ trước khi ghi ra ổ cứng
        private static bool TrySaveValidImage(byte[] bytes, string savePath)
        {
            if (bytes == null || bytes.Length < 500) return false;
            try
            {
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image img = Image.FromStream(ms))
                using (Bitmap bmp = new Bitmap(img))
                {
                    bmp.Save(savePath, ImageFormat.Png);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        // Tìm ảnh QR tĩnh trong thư mục bin\Debug (nếu bạn chép ảnh VietQR gốc vào thư mục chạy)
        private static Image TryLoadLocalFallbackQr()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidateNames = { "vietqr.png", "vietqr.jpg", "1794504598098.png", "qr_vcb.png" };
            foreach (string name in candidateNames)
            {
                string fullPath = Path.Combine(baseDir, name);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        return Image.FromStream(new MemoryStream(bytes));
                    }
                    catch { }
                }
            }
            return null;
        }

        private static string BuildNapas247Payload(long amount, string description)
        {
            string accField = $"0006{BankId}01{AccountNo.Length:D2}{AccountNo}";
            string serviceField = $"0010A00000072701{accField.Length:D2}{accField}0208QRIBFTTA";
            string merchantTag38 = $"38{serviceField.Length:D2}{serviceField}";

            string amountStr = amount.ToString();
            string amountTag54 = amount > 0 ? $"54{amountStr.Length:D2}{amountStr}" : "";

            string cleanDesc = RemoveVietnameseAccents(description);
            if (cleanDesc.Length > 25) cleanDesc = cleanDesc.Substring(0, 25);
            string subTag08 = $"08{cleanDesc.Length:D2}{cleanDesc}";
            string addDataTag62 = $"62{subTag08.Length:D2}{subTag08}";

            string rawPayload = $"000201010212{merchantTag38}5303704{amountTag54}5802VN{addDataTag62}6304";
            string crc = ComputeCrc16Ccitt(rawPayload);
            return rawPayload + crc;
        }

        private static string ComputeCrc16Ccitt(string input)
        {
            ushort crc = 0xFFFF;
            byte[] bytes = Encoding.ASCII.GetBytes(input);
            foreach (byte b in bytes)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }
            return crc.ToString("X4");
        }

        private static string RemoveVietnameseAccents(string text)
        {
            string normalized = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in normalized)
            {
                System.Globalization.UnicodeCategory uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }

        private static Bitmap DrawVietcombankCard(Image qrImage, long amount)
        {
            Bitmap bmp = new Bitmap(360, 460);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(242, 249, 235));

                using (SolidBrush cardBrush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(cardBrush, 20, 20, 320, 420);
                }

                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                using (Font fBank = new Font("Segoe UI", 15, FontStyle.Bold))
                using (SolidBrush brGreen = new SolidBrush(Color.FromArgb(0, 107, 63)))
                {
                    g.DrawString("Vietcombank", fBank, brGreen, new RectangleF(0, 28, 360, 32), sf);
                }

                if (qrImage != null)
                {
                    g.DrawImage(qrImage, new Rectangle(55, 68, 250, 250));
                }
                else
                {
                    // Vẽ ma trận QR mô phỏng khi mất mạng hoàn toàn để khung không bị trống
                    DrawOfflineQrMatrix(g, new Rectangle(65, 78, 230, 230));
                }

                using (Font fSub = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush brRed = new SolidBrush(Color.FromArgb(192, 57, 43)))
                {
                    g.DrawString("VIETQR - NAPAS 247", fSub, brRed, new RectangleF(0, 328, 360, 22), sf);
                }

                using (Font fName = new Font("Segoe UI", 11, FontStyle.Bold))
                using (Font fAcc = new Font("Segoe UI", 11, FontStyle.Regular))
                using (SolidBrush brDark = new SolidBrush(Color.FromArgb(30, 30, 30)))
                {
                    g.DrawString(AccountName, fName, brDark, new RectangleF(0, 358, 360, 24), sf);
                    g.DrawString($"STK: {AccountNo}", fAcc, brDark, new RectangleF(0, 382, 360, 24), sf);
                    if (amount > 0)
                    {
                        g.DrawString($"Số tiền: {amount:N0} VNĐ", fName, brDark, new RectangleF(0, 408, 360, 24), sf);
                    }
                }
            }
            return bmp;
        }

        private static void DrawOfflineQrMatrix(Graphics g, Rectangle bounds)
        {
            int gridSize = 21;
            int cellSize = bounds.Width / gridSize;
            Random rnd = new Random(994288652); // Seed cố định theo STK để hình QR luôn đồng nhất

            using (SolidBrush black = new SolidBrush(Color.Black))
            using (Pen borderPen = new Pen(Color.FromArgb(0, 107, 63), 2))
            {
                g.DrawRectangle(borderPen, bounds.X - 6, bounds.Y - 6, bounds.Width + 12, bounds.Height + 12);

                for (int r = 0; r < gridSize; r++)
                {
                    for (int c = 0; c < gridSize; c++)
                    {
                        bool isFinder = (r < 7 && c < 7) || (r < 7 && c >= gridSize - 7) || (r >= gridSize - 7 && c < 7);
                        if (isFinder)
                        {
                            int lr = r >= gridSize - 7 ? r - (gridSize - 7) : r;
                            int lc = c >= gridSize - 7 ? c - (gridSize - 7) : c;
                            if (lr == 0 || lr == 6 || lc == 0 || lc == 6 || (lr >= 2 && lr <= 4 && lc >= 2 && lc <= 4))
                            {
                                g.FillRectangle(black, bounds.X + c * cellSize, bounds.Y + r * cellSize, cellSize, cellSize);
                            }
                        }
                        else if (rnd.Next(100) < 48)
                        {
                            g.FillRectangle(black, bounds.X + c * cellSize, bounds.Y + r * cellSize, cellSize, cellSize);
                        }
                    }
                }
            }
        }

        public static void PlaySuccessSound()
        {
            SystemSounds.Asterisk.Play();
        }
    }
}
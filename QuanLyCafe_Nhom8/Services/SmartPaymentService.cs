using System;
using System.Media;

namespace QuanLyCafe_Nhom8.Services
{
    public static class SmartPaymentService
    {
        public static string BankId = "MB";
        public static string AccountNo = "0123456789";
        public static string AccountName = "NGUYEN HOANG DUY ANH";

        public static string BuildVietQrUrl(decimal amount, string tableName)
        {
            long roundedAmount = Convert.ToInt64(Math.Round(amount));
            string addInfo = Uri.EscapeDataString($"Thanh toan {tableName}");
            string accName = Uri.EscapeDataString(AccountName);
            return $"https://img.vietqr.io/image/{BankId}-{AccountNo}-compact2.png?amount={roundedAmount}&addInfo={addInfo}&accountName={accName}";
        }

        public static void PlaySuccessSound()
        {
            SystemSounds.Asterisk.Play();
        }
    }
}
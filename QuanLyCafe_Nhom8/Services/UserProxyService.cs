using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Protocol;
using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.DTO;

namespace QuanLyCafe_Nhom8.Services
{
    public class UserProxyService
    {
        private readonly UserDAL _localDal = new UserDAL();
        public bool UseNetworkClient { get; set; } = true;

        // 1. Đăng nhập (truyền mật khẩu thô hoặc hash tùy theo Server đang expect - ở đây gọi thẳng LoginAsync của NetworkClient)
        public async Task<UserDTO> LoginAsync(string username, string password)
        {
            if (UseNetworkClient)
            {
                return await NetworkClient.Instance.LoginAsync(username, password).ConfigureAwait(false);
            }
            return _localDal.Login(username, password);
        }

        // Giữ lại hàm đồng bộ cũ nếu FrmAuth chưa chuyển sang async
        public UserDTO Login(string username, string password)
        {
            if (UseNetworkClient)
            {
                return NetworkClient.Instance.LoginAsync(username, password).GetAwaiter().GetResult();
            }
            return _localDal.Login(username, password);
        }

        // 2. Quên mật khẩu - Bước 1: Gửi yêu cầu Server gửi mã OTP về Email
        public async Task<(bool Success, string Message)> SendForgotOtpAsync(string email)
        {
            if (UseNetworkClient)
            {
                var res = await NetworkClient.Instance
                    .SendRequestAsync(MsgType.FORGOT_SEND_OTP_REQ, new { Email = email })
                    .ConfigureAwait(false);
                return (res.Success, res.Message);
            }
            bool exists = _localDal.CheckEmailExists(email);
            return (exists, exists ? "Email hợp lệ (Offline)." : "Email không tồn tại.");
        }

        // 3. Quên mật khẩu - Bước 2: Xác nhận OTP & Đổi mật khẩu mới trên Server
        public async Task<(bool Success, string Message)> ResetPasswordWithOtpAsync(string email, string otp, string newPassword)
        {
            if (UseNetworkClient)
            {
                var res = await NetworkClient.Instance
                    .SendRequestAsync(MsgType.FORGOT_RESET_PASS_REQ, new
                    {
                        Email = email,
                        Otp = otp,
                        NewPassword = newPassword
                    })
                    .ConfigureAwait(false);
                return (res.Success, res.Message);
            }
            bool ok = _localDal.ResetPasswordByEmail(email, newPassword);
            return (ok, ok ? "Đổi mật khẩu thành công." : "Đổi mật khẩu thất bại.");
        }

        // 4. Quản lý tài khoản (Admin) - Gửi OTP tạo user, Tạo user, Lấy danh sách, Khóa/Xóa
        public async Task<(bool Success, string Message)> SendCreateUserOtpAsync(string email)
        {
            var res = await NetworkClient.Instance
                .SendRequestAsync(MsgType.ADMIN_SEND_CREATE_OTP_REQ, new { Email = email })
                .ConfigureAwait(false);
            return (res.Success, res.Message);
        }

        public async Task<(bool Success, string Message)> CreateUserAsync(string username, string password, string fullName, string email, string role, string otp)
        {
            var res = await NetworkClient.Instance
                .SendRequestAsync(MsgType.ADMIN_CREATE_USER_REQ, new
                {
                    Username = username,
                    Password = password,
                    FullName = fullName,
                    Email = email,
                    Role = role,
                    Otp = otp
                })
                .ConfigureAwait(false);
            return (res.Success, res.Message);
        }

        public async Task<List<UserDTO>> GetAllUsersAsync()
        {
            var res = await NetworkClient.Instance
                .SendRequestAsync(MsgType.GET_USERS_REQ)
                .ConfigureAwait(false);
            if (!res.Success) throw new Exception(res.Message);
            return JsonConvert.DeserializeObject<List<UserDTO>>(res.PayloadJson);
        }

        public async Task<(bool Success, string Message)> ToggleUserStatusAsync(int userId)
        {
            var res = await NetworkClient.Instance
                .SendRequestAsync(MsgType.TOGGLE_USER_REQ, new { UserId = userId })
                .ConfigureAwait(false);
            return (res.Success, res.Message);
        }

        public async Task<(bool Success, string Message)> DeleteUserAsync(int userId)
        {
            var res = await NetworkClient.Instance
                .SendRequestAsync(MsgType.DELETE_USER_REQ, new { UserId = userId })
                .ConfigureAwait(false);
            return (res.Success, res.Message);
        }
    }
}

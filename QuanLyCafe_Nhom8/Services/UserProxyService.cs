using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Protocol;
using QuanLyCafe_Nhom8.DAL;

namespace QuanLyCafe_Nhom8.Services
{
    public class UserProxyService
    {
        private readonly UserDAL _localDal = new UserDAL();
        public bool UseNetworkClient { get; set; } = true;

        public async Task<UserDTO> LoginAsync(string username, string password)
        {
            if (UseNetworkClient)
                return await NetworkClient.Instance.LoginAsync(username, password);
            return null;
        }

        public async Task<(bool Success, string Message)> SendForgotOtpAsync(string email)
        {
            var res = await NetworkClient.Instance.SendRequestAsync(MsgType.FORGOT_SEND_OTP_REQ, new { Email = email });
            return (res.Success, res.Message);
        }

        public async Task<(bool Success, string Message)> ResetPasswordWithOtpAsync(string email, string otp, string newPassword)
        {
            var res = await NetworkClient.Instance.SendRequestAsync(MsgType.FORGOT_RESET_PASS_REQ, new
            {
                Email = email,
                Otp = otp,
                NewPassword = newPassword
            });
            return (res.Success, res.Message);
        }

        public async Task<(bool Success, string Message)> SendCreateUserOtpAsync(string email)
        {
            var res = await NetworkClient.Instance.SendRequestAsync(MsgType.ADMIN_SEND_CREATE_OTP_REQ, new { Email = email });
            return (res.Success, res.Message);
        }

        public async Task<(bool Success, string Message)> CreateUserAsync(string username, string password, string fullName, string email, string role, string otp)
        {
            var res = await NetworkClient.Instance.SendRequestAsync(MsgType.ADMIN_CREATE_USER_REQ, new
            {
                Username = username,
                Password = password,
                FullName = fullName,
                Email = email,
                Role = role,
                Otp = otp
            });
            return (res.Success, res.Message);
        }

        public async Task<List<UserDTO>> GetAllUsersAsync()
        {
            var res = await NetworkClient.Instance.SendRequestAsync(MsgType.GET_USERS_REQ);
            if (!res.Success) throw new Exception(res.Message);
            return JsonConvert.DeserializeObject<List<UserDTO>>(res.PayloadJson) ?? new List<UserDTO>();
        }

        public async Task<(bool Success, string Message)> ToggleUserStatusAsync(int userId)
        {
            var res = await NetworkClient.Instance.SendRequestAsync(MsgType.TOGGLE_USER_REQ, new { UserId = userId });
            return (res.Success, res.Message);
        }

        public async Task<(bool Success, string Message)> DeleteUserAsync(int userId)
        {
            var res = await NetworkClient.Instance.SendRequestAsync(MsgType.DELETE_USER_REQ, new { UserId = userId });
            return (res.Success, res.Message);
        }
    }
}

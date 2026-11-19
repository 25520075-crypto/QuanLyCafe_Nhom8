using System;
using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.DTO;

namespace QuanLyCafe_Nhom8.Services
{
    public class UserProxyService
    {
        private readonly UserDAL _localDal = new UserDAL();
        public bool UseNetworkClient { get; set; } = false;

        public UserDTO Login(string username, string passwordHash)
        {
            if (UseNetworkClient && CafeNetworkClient.Instance != null)
            {
                // Sẵn sàng gửi gói tin LOGIN_REQ qua NetworkClient ở Giai đoạn 3
            }
            return _localDal.Login(username, passwordHash);
        }

        public bool CheckEmailExists(string email)
        {
            return _localDal.CheckEmailExists(email);
        }

        public bool ResetPasswordByEmail(string email, string newPasswordHash)
        {
            return _localDal.ResetPasswordByEmail(email, newPasswordHash);
        }
    }
}

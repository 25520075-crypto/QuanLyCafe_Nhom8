using System;
using System.Data;
using System.Data.SqlClient;
using QuanLyCafe_Nhom8.DTO;
using QuanLyCafe_Nhom8.Services;

namespace QuanLyCafe_Nhom8.DAL
{
    public class UserDAL
    {
        public UserDAL()
        {
            EnsureUsersSchemaAndDefaultAdmin();
        }

        private void EnsureUsersSchemaAndDefaultAdmin()
        {
            try
            {
                string sql = @"
                    IF COL_LENGTH('dbo.Users', 'Email') IS NULL
                    BEGIN
                        ALTER TABLE dbo.Users ADD Email VARCHAR(100) NULL;
                    END";
                DatabaseHelper.ExecuteNonQuery(sql);

                string adminHash = AuthService.HashPassword("admin123");
                string staffHash = AuthService.HashPassword("staff123");

                string seedSql = @"
                    IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
                    BEGIN
                        INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, Email)
                        VALUES ('admin', @AdminHash, N'Quản lý Hệ thống', 'Admin', 1, 'admin@gmail.com');
                    END
                    ELSE
                    BEGIN
                        UPDATE Users SET Email = ISNULL(Email, 'admin@gmail.com') WHERE Username = 'admin';
                    END

                    IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'thungan1')
                    BEGIN
                        INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, Email)
                        VALUES ('thungan1', @StaffHash, N'Thu ngân Ca Sáng', 'Staff', 1, 'thungan1@gmail.com');
                    END";

                DatabaseHelper.ExecuteNonQuery(seedSql, new SqlParameter[]
                {
                    new SqlParameter("@AdminHash", adminHash),
                    new SqlParameter("@StaffHash", staffHash)
                });
            }
            catch
            {
                // Bỏ qua nếu bảng đã sẵn sàng
            }
        }

        public UserDTO CheckLogin(string username, string plainPassword)
        {
            string hash = AuthService.HashPassword(plainPassword);
            string query = @"
                SELECT UserId, Username, FullName, Role, IsActive, Email 
                FROM Users 
                WHERE Username = @u AND (PasswordHash = @hash OR PasswordHash = @raw) AND IsActive = 1";

            SqlParameter[] prms = {
                new SqlParameter("@u", username.Trim()),
                new SqlParameter("@hash", hash),
                new SqlParameter("@raw", plainPassword.Trim())
            };

            DataTable dt = DatabaseHelper.ExecuteQuery(query, prms);
            if (dt.Rows.Count > 0)
            {
                DataRow r = dt.Rows[0];
                return new UserDTO
                {
                    UserId = Convert.ToInt32(r["UserId"]),
                    Username = r["Username"].ToString(),
                    FullName = r["FullName"].ToString(),
                    Role = r["Role"].ToString(),
                    IsActive = Convert.ToBoolean(r["IsActive"]),
                    Email = r.Table.Columns.Contains("Email") && r["Email"] != DBNull.Value ? r["Email"].ToString() : ""
                };
            }
            return null;
        }

        // Tự động đánh lại Mã NV liên tục (1, 2, 3...) không bị nhảy cóc khi xóa
        public DataTable GetAllUsers()
        {
            string query = @"
                SELECT ROW_NUMBER() OVER (ORDER BY UserId ASC) AS [Mã NV], 
                       Username AS [Tài khoản], 
                       FullName AS [Họ và tên], 
                       Role AS [Quyền hạn], 
                       Email AS [Gmail liên kết], 
                       IsActive AS [Đang hoạt động]
                FROM Users
                ORDER BY UserId ASC";
            return DatabaseHelper.ExecuteQuery(query);
        }

        public bool IsUsernameOrEmailExist(string username, string email)
        {
            string query = "SELECT COUNT(1) FROM Users WHERE Username = @u OR (Email IS NOT NULL AND Email = @e)";
            SqlParameter[] prms = {
                new SqlParameter("@u", username.Trim()),
                new SqlParameter("@e", email.Trim())
            };
            int count = Convert.ToInt32(DatabaseHelper.ExecuteScalar(query, prms));
            return count > 0;
        }

        public bool CheckEmailExist(string email)
        {
            string query = "SELECT COUNT(1) FROM Users WHERE Email = @e AND IsActive = 1";
            SqlParameter[] prms = { new SqlParameter("@e", email.Trim()) };
            int count = Convert.ToInt32(DatabaseHelper.ExecuteScalar(query, prms));
            return count > 0;
        }

        public bool CreateAccountByAdmin(string username, string plainPassword, string fullName, string role, string email)
        {
            string hash = AuthService.HashPassword(plainPassword);
            string query = @"
                INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, Email) 
                VALUES (@u, @p, @fn, @role, 1, @e)";

            SqlParameter[] prms = {
                new SqlParameter("@u", username.Trim()),
                new SqlParameter("@p", hash),
                new SqlParameter("@fn", string.IsNullOrWhiteSpace(fullName) ? username.Trim() : fullName.Trim()),
                new SqlParameter("@role", role),
                new SqlParameter("@e", email.Trim())
            };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }

        public bool UpdatePasswordByEmail(string email, string newPlainPassword)
        {
            string hash = AuthService.HashPassword(newPlainPassword);
            string query = "UPDATE Users SET PasswordHash = @p WHERE Email = @e";
            SqlParameter[] prms = {
                new SqlParameter("@p", hash),
                new SqlParameter("@e", email.Trim())
            };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }

        // Xóa nhân viên theo đúng thứ tự Mã NV hiển thị và tự động Reseed lại bộ đếm SQL
        public bool DeleteUser(int displayOrderId)
        {
            string query = @"
                WITH OrderedUsers AS (
                    SELECT UserId, ROW_NUMBER() OVER (ORDER BY UserId ASC) AS RN
                    FROM Users
                )
                DELETE FROM Users
                WHERE UserId IN (SELECT UserId FROM OrderedUsers WHERE RN = @rn);

                DECLARE @MaxId INT = ISNULL((SELECT MAX(UserId) FROM Users), 0);
                DBCC CHECKIDENT ('Users', RESEED, @MaxId);";

            SqlParameter[] prms = { new SqlParameter("@rn", displayOrderId) };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }

        // Khóa / Mở khóa tài khoản theo đúng thứ tự Mã NV hiển thị
        public bool ToggleUserActiveStatus(int displayOrderId, bool currentStatus)
        {
            string query = @"
                WITH OrderedUsers AS (
                    SELECT UserId, ROW_NUMBER() OVER (ORDER BY UserId ASC) AS RN
                    FROM Users
                )
                UPDATE Users
                SET IsActive = @newStatus
                WHERE UserId IN (SELECT UserId FROM OrderedUsers WHERE RN = @rn)";

            SqlParameter[] prms = {
                new SqlParameter("@newStatus", !currentStatus),
                new SqlParameter("@rn", displayOrderId)
            };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }
    }
}
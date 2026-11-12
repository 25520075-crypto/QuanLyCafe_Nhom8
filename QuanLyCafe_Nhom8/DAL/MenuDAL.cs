using System;
using System.Data;
using System.Data.SqlClient;

namespace QuanLyCafe_Nhom8.DAL
{
    public class MenuDAL
    {
        public MenuDAL()
        {
            EnsureCategoriesSeeded();
        }

        // Đảm bảo bảng Categories luôn có sẵn các danh mục mặc định nếu DB mới tạo
        private void EnsureCategoriesSeeded()
        {
            try
            {
                string sql = @"
                    IF OBJECT_ID('dbo.Categories', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.Categories (
                            CategoryId INT IDENTITY(1,1) PRIMARY KEY,
                            CategoryName NVARCHAR(100) NOT NULL
                        );
                    END

                    IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
                    BEGIN
                        INSERT INTO dbo.Categories (CategoryName) VALUES
                        (N'Cà phê truyền thống'),
                        (N'Trà trái cây & Macchiato'),
                        (N'Đá xay & Freeze'),
                        (N'Bánh ngọt & Topping');
                    END";
                DatabaseHelper.ExecuteNonQuery(sql);
            }
            catch
            {
                // Bỏ qua nếu bảng đã có sẵn dữ liệu
            }
        }

        // Lấy toàn bộ danh mục thực đơn
        public DataTable GetAllCategories()
        {
            return DatabaseHelper.ExecuteQuery("SELECT CategoryId, CategoryName FROM Categories ORDER BY CategoryId ASC");
        }

        // Thêm nhanh 1 danh mục mới
        public bool AddCategory(string categoryName)
        {
            string query = "INSERT INTO Categories (CategoryName) VALUES (@Name)";
            SqlParameter[] prms = { new SqlParameter("@Name", categoryName.Trim()) };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }

        // Lấy danh sách món ăn kèm số thứ tự liên tục (ROW_NUMBER) để không bị nhảy số khi xóa
        public DataTable GetProductsForManager(int categoryId = 0, string keyword = "")
        {
            string query = @"
                SELECT 
                    ROW_NUMBER() OVER (ORDER BY p.ProductId ASC) AS [STT],
                    p.ProductId,
                    p.CategoryId,
                    p.ProductName AS [Tên món nước],
                    ISNULL(c.CategoryName, N'Chưa phân loại') AS [Danh mục],
                    p.Price AS [Đơn giá (VNĐ)],
                    p.IsActive AS [Đang bán],
                    ISNULL(p.ImagePath, '') AS [ImagePath]
                FROM Products p
                LEFT JOIN Categories c ON p.CategoryId = c.CategoryId
                WHERE (@CatId = 0 OR p.CategoryId = @CatId)
                  AND (@Kw = N'' OR p.ProductName LIKE N'%' + @Kw + N'%')
                ORDER BY p.ProductId ASC";

            SqlParameter[] prms = {
                new SqlParameter("@CatId", categoryId),
                new SqlParameter("@Kw", keyword.Trim())
            };
            return DatabaseHelper.ExecuteQuery(query, prms);
        }

        // Thêm món mới
        public bool InsertProduct(int categoryId, string name, decimal price, string imagePath, bool isActive)
        {
            string query = @"
                INSERT INTO Products (CategoryId, ProductName, Price, ImagePath, IsActive)
                VALUES (@CatId, @Name, @Price, @Img, @Active)";

            SqlParameter[] prms = {
                new SqlParameter("@CatId", categoryId),
                new SqlParameter("@Name", name.Trim()),
                new SqlParameter("@Price", price),
                new SqlParameter("@Img", string.IsNullOrEmpty(imagePath) ? (object)DBNull.Value : imagePath),
                new SqlParameter("@Active", isActive)
            };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }

        // Cập nhật thông tin món
        public bool UpdateProduct(int productId, int categoryId, string name, decimal price, string imagePath, bool isActive)
        {
            string query = @"
                UPDATE Products
                SET CategoryId = @CatId,
                    ProductName = @Name,
                    Price = @Price,
                    ImagePath = @Img,
                    IsActive = @Active
                WHERE ProductId = @Id";

            SqlParameter[] prms = {
                new SqlParameter("@CatId", categoryId),
                new SqlParameter("@Name", name.Trim()),
                new SqlParameter("@Price", price),
                new SqlParameter("@Img", string.IsNullOrEmpty(imagePath) ? (object)DBNull.Value : imagePath),
                new SqlParameter("@Active", isActive),
                new SqlParameter("@Id", productId)
            };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }

        // Đổi nhanh trạng thái Đang bán / Ngừng bán
        public bool ToggleProductStatus(int productId, bool currentStatus)
        {
            string query = "UPDATE Products SET IsActive = @NewStatus WHERE ProductId = @Id";
            SqlParameter[] prms = {
                new SqlParameter("@NewStatus", !currentStatus),
                new SqlParameter("@Id", productId)
            };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }

        // Kiểm tra xem món đã từng xuất hiện trong hóa đơn (BillDetails) chưa
        public bool IsProductUsedInBills(int productId)
        {
            string query = "SELECT COUNT(1) FROM BillDetails WHERE ProductId = @Id";
            SqlParameter[] prms = { new SqlParameter("@Id", productId) };
            int count = Convert.ToInt32(DatabaseHelper.ExecuteScalar(query, prms));
            return count > 0;
        }

        // Xóa món và tự động Reseed lại ID trong bảng Products
        public bool DeleteProduct(int productId)
        {
            string query = @"
                DELETE FROM Products WHERE ProductId = @Id;
                DECLARE @MaxId INT = ISNULL((SELECT MAX(ProductId) FROM Products), 0);
                DBCC CHECKIDENT ('Products', RESEED, @MaxId);";

            SqlParameter[] prms = { new SqlParameter("@Id", productId) };
            return DatabaseHelper.ExecuteNonQuery(query, prms) > 0;
        }
    }
}
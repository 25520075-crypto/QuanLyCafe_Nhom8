using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using QuanLyCafe_Nhom8.DTO;

namespace QuanLyCafe_Nhom8.DAL
{
    public class PosServiceDAL
    {
        // Hàm khởi tạo: Tự động kiểm tra và bổ sung các kênh bán hàng (Mang về, Shopee, XanhSM, Grab) và cột PaymentMethod nếu DB cũ chưa có
        public PosServiceDAL()
        {
            EnsureOrderChannelsAndColumnsSeeded();
        }

        // Tự động nâng cấp bảng Tables và Bills an toàn mà không cần xóa Database cũ
        public void EnsureOrderChannelsAndColumnsSeeded()
        {
            string sql = @"
                -- Tự động thêm cột PaymentMethod (Phương thức thanh toán) vào bảng Bills nếu chưa có
                IF COL_LENGTH('dbo.Bills', 'PaymentMethod') IS NULL
                BEGIN
                    ALTER TABLE dbo.Bills ADD PaymentMethod NVARCHAR(50) NULL DEFAULT N'Tiền mặt';
                END

                -- Tự động thêm các bàn Mua mang về nếu chưa có
                IF NOT EXISTS (SELECT 1 FROM Tables WHERE TableName LIKE N'Mang về%')
                BEGIN
                    INSERT INTO Tables (TableName, Status) VALUES
                    (N'Mang về 01', N'Trống'),
                    (N'Mang về 02', N'Trống'),
                    (N'Mang về 03', N'Trống'),
                    (N'Mang về 04', N'Trống');
                END

                -- Tự động thêm các đơn ShopeeFood và XanhSM nếu chưa có
                IF NOT EXISTS (SELECT 1 FROM Tables WHERE TableName LIKE N'Shopee%' OR TableName LIKE N'XanhSM%')
                BEGIN
                    INSERT INTO Tables (TableName, Status) VALUES
                    (N'ShopeeFood 01', N'Trống'),
                    (N'ShopeeFood 02', N'Trống'),
                    (N'ShopeeFood 03', N'Trống'),
                    (N'XanhSM 01', N'Trống'),
                    (N'XanhSM 02', N'Trống'),
                    (N'XanhSM 03', N'Trống');
                END

                -- Tự động thêm các đơn GrabFood nếu chưa có
                IF NOT EXISTS (SELECT 1 FROM Tables WHERE TableName LIKE N'GrabFood%')
                BEGIN
                    INSERT INTO Tables (TableName, Status) VALUES
                    (N'GrabFood 01', N'Trống'),
                    (N'GrabFood 02', N'Trống'),
                    (N'GrabFood 03', N'Trống'),
                    (N'GrabFood 04', N'Trống');
                END";
            DatabaseHelper.ExecuteNonQuery(sql);
        }

        // Hàm băm mật khẩu sang chuỗi SHA-256 để kiểm tra quyền Quản lý (Admin)
        public static string ComputeSha256(string rawText)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawText));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        // Kiểm tra tài khoản Quản lý (Admin) khi thực hiện thao tác nhạy cảm: Hủy món hoặc Giảm giá
        public bool VerifyManagerAccount(string username, string password, out string managerFullName)
        {
            managerFullName = "";
            string hash = ComputeSha256(password);
            string query = @"
                SELECT FullName, Role 
                FROM Users 
                WHERE Username = @User AND (PasswordHash = @Hash OR PasswordHash = @Raw) AND IsActive = 1";
            DataTable dt = DatabaseHelper.ExecuteQuery(query, new SqlParameter[]
            {
                new SqlParameter("@User", username.Trim()),
                new SqlParameter("@Hash", hash),
                new SqlParameter("@Raw", password.Trim())
            });

            // Hỗ trợ tài khoản admin mặc định khi chạy demo
            if (dt.Rows.Count == 0)
            {
                if (username.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase) && password == "admin123")
                {
                    managerFullName = "Duy Anh (Quản lý)";
                    return true;
                }
                return false;
            }

            string role = dt.Rows[0]["Role"].ToString();
            managerFullName = dt.Rows[0]["FullName"].ToString();
            return role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        // Lấy toàn bộ danh sách Bàn / Đơn từ bảng Tables
        public List<TableDTO> GetAllTables()
        {
            List<TableDTO> list = new List<TableDTO>();
            DataTable dt = DatabaseHelper.ExecuteQuery("SELECT TableId, TableName, Status FROM Tables ORDER BY TableId");
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new TableDTO
                {
                    TableId = Convert.ToInt32(row["TableId"]),
                    TableName = row["TableName"].ToString(),
                    Status = row["Status"].ToString()
                });
            }
            return list;
        }

        // Tìm thông tin 1 bàn theo mã TableId
        public TableDTO GetTableById(int tableId)
        {
            DataTable dt = DatabaseHelper.ExecuteQuery("SELECT TableId, TableName, Status FROM Tables WHERE TableId = @Id",
                new SqlParameter[] { new SqlParameter("@Id", tableId) });
            if (dt.Rows.Count == 0) return null;
            return new TableDTO
            {
                TableId = Convert.ToInt32(dt.Rows[0]["TableId"]),
                TableName = dt.Rows[0]["TableName"].ToString(),
                Status = dt.Rows[0]["Status"].ToString()
            };
        }

        // Lấy danh sách các món nước đang mở bán (IsActive = 1)
        public List<ProductDTO> GetActiveProducts()
        {
            List<ProductDTO> list = new List<ProductDTO>();
            DataTable dt = DatabaseHelper.ExecuteQuery("SELECT ProductId, CategoryId, ProductName, Price, ImagePath FROM Products WHERE IsActive = 1");
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new ProductDTO
                {
                    ProductId = Convert.ToInt32(row["ProductId"]),
                    CategoryId = Convert.ToInt32(row["CategoryId"]),
                    ProductName = row["ProductName"].ToString(),
                    Price = Convert.ToDecimal(row["Price"]),
                    ImagePath = row["ImagePath"] == DBNull.Value ? "" : row["ImagePath"].ToString()
                });
            }
            return list;
        }

        // Lấy danh sách các món khách đã gọi tại bàn đang chọn (chỉ lấy hóa đơn chưa thanh toán Status = 0)
        public DataTable GetBillDetailsByTable(int tableId)
        {
            string query = @"
                SELECT bd.BillDetailId, p.ProductName AS [Tên món], bd.Quantity AS [SL], bd.UnitPrice AS [Đơn giá], 
                       (bd.Quantity * bd.UnitPrice) AS [Thành tiền], bd.KitchenStatus AS [Trạng thái]
                FROM BillDetails bd
                INNER JOIN Bills b ON bd.BillId = b.BillId
                INNER JOIN Products p ON bd.ProductId = p.ProductId
                WHERE b.TableId = @TableId AND b.Status = 0";
            return DatabaseHelper.ExecuteQuery(query, new SqlParameter[] { new SqlParameter("@TableId", tableId) });
        }

        // Thêm món vào bàn với trạng thái ban đầu là 'Chờ xác nhận' (Order nháp để đọc lại cho khách trước khi gửi pha chế)
        public void AddItemToTable(int tableId, int productId, int quantity)
        {
            using (SqlConnection conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                // Sử dụng SqlTransaction để đảm bảo toàn vẹn dữ liệu khi vừa tạo Bill vừa thêm BillDetails
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        int billId = -1;
                        using (SqlCommand cmdCheckBill = new SqlCommand("SELECT TOP 1 BillId FROM Bills WHERE TableId = @TableId AND Status = 0", conn, trans))
                        {
                            cmdCheckBill.Parameters.AddWithValue("@TableId", tableId);
                            object res = cmdCheckBill.ExecuteScalar();
                            if (res != null && res != DBNull.Value)
                            {
                                billId = Convert.ToInt32(res);
                            }
                        }

                        // Nếu bàn chưa có hóa đơn thì tạo hóa đơn mới và đổi trạng thái bàn sang 'Có khách'
                        if (billId == -1)
                        {
                            using (SqlCommand cmdCreateBill = new SqlCommand("INSERT INTO Bills (TableId, CheckInTime, Status) OUTPUT INSERTED.BillId VALUES (@TableId, GETDATE(), 0)", conn, trans))
                            {
                                cmdCreateBill.Parameters.AddWithValue("@TableId", tableId);
                                billId = Convert.ToInt32(cmdCreateBill.ExecuteScalar());
                            }

                            using (SqlCommand cmdUpdateTable = new SqlCommand("UPDATE Tables SET Status = N'Có khách' WHERE TableId = @TableId", conn, trans))
                            {
                                cmdUpdateTable.Parameters.AddWithValue("@TableId", tableId);
                                cmdUpdateTable.ExecuteNonQuery();
                            }
                        }

                        decimal unitPrice = 0;
                        using (SqlCommand cmdPrice = new SqlCommand("SELECT Price FROM Products WHERE ProductId = @ProductId", conn, trans))
                        {
                            cmdPrice.Parameters.AddWithValue("@ProductId", productId);
                            unitPrice = Convert.ToDecimal(cmdPrice.ExecuteScalar());
                        }

                        // Kiểm tra xem món đó đã có ở trạng thái 'Chờ xác nhận' chưa, nếu có thì cộng dồn số lượng
                        int existingDraftDetailId = -1;
                        using (SqlCommand cmdCheckItem = new SqlCommand("SELECT TOP 1 BillDetailId FROM BillDetails WHERE BillId = @BillId AND ProductId = @ProductId AND KitchenStatus = N'Chờ xác nhận'", conn, trans))
                        {
                            cmdCheckItem.Parameters.AddWithValue("@BillId", billId);
                            cmdCheckItem.Parameters.AddWithValue("@ProductId", productId);
                            object detailObj = cmdCheckItem.ExecuteScalar();
                            if (detailObj != null && detailObj != DBNull.Value)
                            {
                                existingDraftDetailId = Convert.ToInt32(detailObj);
                            }
                        }

                        if (existingDraftDetailId != -1)
                        {
                            using (SqlCommand cmdUpdateDetail = new SqlCommand("UPDATE BillDetails SET Quantity = Quantity + @Quantity WHERE BillDetailId = @BillDetailId", conn, trans))
                            {
                                cmdUpdateDetail.Parameters.AddWithValue("@Quantity", quantity);
                                cmdUpdateDetail.Parameters.AddWithValue("@BillDetailId", existingDraftDetailId);
                                cmdUpdateDetail.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            using (SqlCommand cmdInsertDetail = new SqlCommand("INSERT INTO BillDetails (BillId, ProductId, Quantity, UnitPrice, KitchenStatus) VALUES (@BillId, @ProductId, @Quantity, @UnitPrice, N'Chờ xác nhận')", conn, trans))
                            {
                                cmdInsertDetail.Parameters.AddWithValue("@BillId", billId);
                                cmdInsertDetail.Parameters.AddWithValue("@ProductId", productId);
                                cmdInsertDetail.Parameters.AddWithValue("@Quantity", quantity);
                                cmdInsertDetail.Parameters.AddWithValue("@UnitPrice", unitPrice);
                                cmdInsertDetail.ExecuteNonQuery();
                            }
                        }

                        // Cập nhật lại tổng tiền của hóa đơn
                        using (SqlCommand cmdUpdateTotal = new SqlCommand("UPDATE Bills SET TotalAmount = (SELECT ISNULL(SUM(Quantity * UnitPrice), 0) FROM BillDetails WHERE BillId = @BillId) WHERE BillId = @BillId", conn, trans))
                        {
                            cmdUpdateTotal.Parameters.AddWithValue("@BillId", billId);
                            cmdUpdateTotal.ExecuteNonQuery();
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        // Chuyển các món từ 'Chờ xác nhận' sang 'Chờ pha chế' để hiển thị lên màn hình Quầy Bar (KDS)
        public int SendTableOrderToKitchen(int tableId)
        {
            string query = @"
                UPDATE bd
                SET bd.KitchenStatus = N'Chờ pha chế'
                FROM BillDetails bd
                INNER JOIN Bills b ON bd.BillId = b.BillId
                WHERE b.TableId = @TableId AND b.Status = 0 AND bd.KitchenStatus = N'Chờ xác nhận'";
            return DatabaseHelper.ExecuteNonQuery(query, new SqlParameter[] { new SqlParameter("@TableId", tableId) });
        }

        // Hủy 1 món khỏi hóa đơn (Lệnh DELETE này sẽ tự động kích hoạt Trigger trg_AuditBillDetailDelete trong SQL)
        public void RemoveBillDetailItem(int billDetailId, int tableId)
        {
            using (SqlConnection conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        int billId = -1;
                        using (SqlCommand cmdGetBill = new SqlCommand("SELECT BillId FROM BillDetails WHERE BillDetailId = @DetailId", conn, trans))
                        {
                            cmdGetBill.Parameters.AddWithValue("@DetailId", billDetailId);
                            object obj = cmdGetBill.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value)
                            {
                                billId = Convert.ToInt32(obj);
                            }
                        }

                        if (billId == -1) return;

                        using (SqlCommand cmdDelete = new SqlCommand("DELETE FROM BillDetails WHERE BillDetailId = @DetailId", conn, trans))
                        {
                            cmdDelete.Parameters.AddWithValue("@DetailId", billDetailId);
                            cmdDelete.ExecuteNonQuery();
                        }

                        int remainingCount = 0;
                        using (SqlCommand cmdCount = new SqlCommand("SELECT COUNT(*) FROM BillDetails WHERE BillId = @BillId", conn, trans))
                        {
                            cmdCount.Parameters.AddWithValue("@BillId", billId);
                            remainingCount = Convert.ToInt32(cmdCount.ExecuteScalar());
                        }

                        // Nếu hủy hết sạch các món trong bàn thì xóa hóa đơn rỗng và trả bàn về trạng thái 'Trống'
                        if (remainingCount == 0)
                        {
                            using (SqlCommand cmdDelBill = new SqlCommand("DELETE FROM Bills WHERE BillId = @BillId", conn, trans))
                            {
                                cmdDelBill.Parameters.AddWithValue("@BillId", billId);
                                cmdDelBill.ExecuteNonQuery();
                            }

                            using (SqlCommand cmdFreeTable = new SqlCommand("UPDATE Tables SET Status = N'Trống' WHERE TableId = @TableId", conn, trans))
                            {
                                cmdFreeTable.Parameters.AddWithValue("@TableId", tableId);
                                cmdFreeTable.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            using (SqlCommand cmdUpdateTotal = new SqlCommand("UPDATE Bills SET TotalAmount = (SELECT ISNULL(SUM(Quantity * UnitPrice), 0) FROM BillDetails WHERE BillId = @BillId) WHERE BillId = @BillId", conn, trans))
                            {
                                cmdUpdateTotal.Parameters.AddWithValue("@BillId", billId);
                                cmdUpdateTotal.ExecuteNonQuery();
                            }
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        // Nghiệp vụ CHUYỂN BÀN: Đổi khách từ bàn hiện tại sang một bàn đang TRỐNG
        public void MoveToEmptyTable(int sourceTableId, int targetTableId)
        {
            using (SqlConnection conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        using (SqlCommand cmdMoveBill = new SqlCommand("UPDATE Bills SET TableId = @TargetTableId WHERE TableId = @SourceTableId AND Status = 0", conn, trans))
                        {
                            cmdMoveBill.Parameters.AddWithValue("@TargetTableId", targetTableId);
                            cmdMoveBill.Parameters.AddWithValue("@SourceTableId", sourceTableId);
                            cmdMoveBill.ExecuteNonQuery();
                        }

                        using (SqlCommand cmdFreeSource = new SqlCommand("UPDATE Tables SET Status = N'Trống' WHERE TableId = @SourceTableId", conn, trans))
                        {
                            cmdFreeSource.Parameters.AddWithValue("@SourceTableId", sourceTableId);
                            cmdFreeSource.ExecuteNonQuery();
                        }

                        using (SqlCommand cmdOccupyTarget = new SqlCommand("UPDATE Tables SET Status = N'Có khách' WHERE TableId = @TargetTableId", conn, trans))
                        {
                            cmdOccupyTarget.Parameters.AddWithValue("@TargetTableId", targetTableId);
                            cmdOccupyTarget.ExecuteNonQuery();
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        // Nghiệp vụ GỘP BÀN: Dồn toàn bộ món của bàn nguồn vào bàn đích (cả 2 bàn đều đang CÓ KHÁCH)
        public void MergeOccupiedTables(int sourceTableId, int targetTableId)
        {
            using (SqlConnection conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        int sourceBillId = -1;
                        int targetBillId = -1;

                        using (SqlCommand cmdSource = new SqlCommand("SELECT TOP 1 BillId FROM Bills WHERE TableId = @TableId AND Status = 0", conn, trans))
                        {
                            cmdSource.Parameters.AddWithValue("@TableId", sourceTableId);
                            object res = cmdSource.ExecuteScalar();
                            if (res != null && res != DBNull.Value) sourceBillId = Convert.ToInt32(res);
                        }

                        using (SqlCommand cmdTarget = new SqlCommand("SELECT TOP 1 BillId FROM Bills WHERE TableId = @TableId AND Status = 0", conn, trans))
                        {
                            cmdTarget.Parameters.AddWithValue("@TableId", targetTableId);
                            object res = cmdTarget.ExecuteScalar();
                            if (res != null && res != DBNull.Value) targetBillId = Convert.ToInt32(res);
                        }

                        if (sourceBillId == -1 || targetBillId == -1) return;

                        // Chuyển toàn bộ các dòng món sang hóa đơn của bàn đích
                        using (SqlCommand cmdMoveDetails = new SqlCommand("UPDATE BillDetails SET BillId = @TargetBillId WHERE BillId = @SourceBillId", conn, trans))
                        {
                            cmdMoveDetails.Parameters.AddWithValue("@TargetBillId", targetBillId);
                            cmdMoveDetails.Parameters.AddWithValue("@SourceBillId", sourceBillId);
                            cmdMoveDetails.ExecuteNonQuery();
                        }

                        // Tính lại tổng tiền cho bàn đích
                        using (SqlCommand cmdUpdateTargetTotal = new SqlCommand("UPDATE Bills SET TotalAmount = (SELECT ISNULL(SUM(Quantity * UnitPrice), 0) FROM BillDetails WHERE BillId = @TargetBillId) WHERE BillId = @TargetBillId", conn, trans))
                        {
                            cmdUpdateTargetTotal.Parameters.AddWithValue("@TargetBillId", targetBillId);
                            cmdUpdateTargetTotal.ExecuteNonQuery();
                        }

                        // Xóa hóa đơn cũ của bàn nguồn và giải phóng bàn nguồn về 'Trống'
                        using (SqlCommand cmdDeleteOldBill = new SqlCommand("DELETE FROM Bills WHERE BillId = @SourceBillId", conn, trans))
                        {
                            cmdDeleteOldBill.Parameters.AddWithValue("@SourceBillId", sourceBillId);
                            cmdDeleteOldBill.ExecuteNonQuery();
                        }

                        using (SqlCommand cmdFreeSource = new SqlCommand("UPDATE Tables SET Status = N'Trống' WHERE TableId = @SourceTableId", conn, trans))
                        {
                            cmdFreeSource.Parameters.AddWithValue("@SourceTableId", sourceTableId);
                            cmdFreeSource.ExecuteNonQuery();
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        // Chốt thanh toán hóa đơn, lưu phương thức thanh toán (Tiền mặt / Chuyển khoản / Shopee / Grab) và trả bàn về 'Trống'
        public bool CheckoutTable(int tableId, int discountPercent, string paymentMethod, out decimal finalAmount)
        {
            finalAmount = 0;
            using (SqlConnection conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        int billId = -1;
                        using (SqlCommand cmdGetBill = new SqlCommand("SELECT TOP 1 BillId FROM Bills WHERE TableId = @TableId AND Status = 0", conn, trans))
                        {
                            cmdGetBill.Parameters.AddWithValue("@TableId", tableId);
                            object obj = cmdGetBill.ExecuteScalar();
                            if (obj == null || obj == DBNull.Value)
                            {
                                return false;
                            }
                            billId = Convert.ToInt32(obj);
                        }

                        decimal rawTotal = 0;
                        using (SqlCommand cmdSum = new SqlCommand("SELECT ISNULL(SUM(Quantity * UnitPrice), 0) FROM BillDetails WHERE BillId = @BillId", conn, trans))
                        {
                            cmdSum.Parameters.AddWithValue("@BillId", billId);
                            rawTotal = Convert.ToDecimal(cmdSum.ExecuteScalar());
                        }

                        finalAmount = rawTotal - (rawTotal * discountPercent / 100m);

                        // Cập nhật trạng thái hóa đơn Status = 1 (Đã thanh toán) và lưu loại tiền thanh toán
                        using (SqlCommand cmdCloseBill = new SqlCommand("UPDATE Bills SET CheckOutTime = GETDATE(), DiscountPercent = @Discount, TotalAmount = @FinalAmount, PaymentMethod = @Method, Status = 1 WHERE BillId = @BillId", conn, trans))
                        {
                            cmdCloseBill.Parameters.AddWithValue("@Discount", discountPercent);
                            cmdCloseBill.Parameters.AddWithValue("@FinalAmount", finalAmount);
                            cmdCloseBill.Parameters.AddWithValue("@Method", paymentMethod);
                            cmdCloseBill.Parameters.AddWithValue("@BillId", billId);
                            cmdCloseBill.ExecuteNonQuery();
                        }

                        // Giải phóng bàn về trạng thái 'Trống' sau khi khách thanh toán rời đi
                        using (SqlCommand cmdFreeTable = new SqlCommand("UPDATE Tables SET Status = N'Trống' WHERE TableId = @TableId", conn, trans))
                        {
                            cmdFreeTable.Parameters.AddWithValue("@TableId", tableId);
                            cmdFreeTable.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        // Lấy danh sách món đã được bấm 'Gửi pha chế' để hiển thị trên màn hình KDS
        public DataTable GetPendingKitchenItems()
        {
            string query = @"
                SELECT bd.BillDetailId AS [Mã], t.TableName AS [Bàn / Đơn], p.ProductName AS [Tên món cần pha], 
                       bd.Quantity AS [Số lượng], FORMAT(b.CheckInTime, 'HH:mm:ss') AS [Giờ gọi], bd.KitchenStatus AS [Trạng thái]
                FROM BillDetails bd
                INNER JOIN Bills b ON bd.BillId = b.BillId
                INNER JOIN Tables t ON b.TableId = t.TableId
                INNER JOIN Products p ON bd.ProductId = p.ProductId
                WHERE b.Status = 0 AND bd.KitchenStatus = N'Chờ pha chế'
                ORDER BY bd.BillDetailId ASC";
            return DatabaseHelper.ExecuteQuery(query);
        }

        // Đánh dấu hoàn thành pha chế trên màn hình KDS
        public void MarkKitchenItemDone(int billDetailId)
        {
            string query = "UPDATE BillDetails SET KitchenStatus = N'Đã phục vụ' WHERE BillDetailId = @Id";
            DatabaseHelper.ExecuteNonQuery(query, new SqlParameter[] { new SqlParameter("@Id", billDetailId) });
        }
    }
}
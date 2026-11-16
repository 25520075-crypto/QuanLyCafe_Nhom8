using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Protocol;

namespace CafeServer.DAL
{
    public class ServerDataAdapter
    {
        private readonly UserDAL _userDal = new UserDAL();
        private readonly MenuDAL _menuDal = new MenuDAL();
        private readonly PosServiceDAL _posDal = new PosServiceDAL();

        public List<UserDTO> GetAllUsersDto()
        {
            DataTable dt = _userDal.GetAllUsers();
            var list = new List<UserDTO>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new UserDTO
                {
                    DisplayOrderId = Convert.ToInt32(r["Mã NV"]),
                    Username = r["Tài khoản"].ToString(),
                    FullName = r["Họ và tên"].ToString(),
                    Role = r["Quyền hạn"].ToString(),
                    Email = r["Gmail liên kết"] == DBNull.Value ? "" : r["Gmail liên kết"].ToString(),
                    IsActive = Convert.ToBoolean(r["Đang hoạt động"])
                });
            }
            return list;
        }

        public List<CategoryDTO> GetAllCategoriesDto()
        {
            DataTable dt = _menuDal.GetAllCategories();
            var list = new List<CategoryDTO>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new CategoryDTO
                {
                    CategoryId = Convert.ToInt32(r["CategoryId"]),
                    CategoryName = r["CategoryName"].ToString()
                });
            }
            return list;
        }

        public List<ProductDTO> GetProductsForManagerDto(int categoryId = 0, string keyword = "")
        {
            DataTable dt = _menuDal.GetProductsForManager(categoryId, keyword);
            var list = new List<ProductDTO>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new ProductDTO
                {
                    STT = Convert.ToInt32(r["STT"]),
                    ProductId = Convert.ToInt32(r["ProductId"]),
                    CategoryId = r["CategoryId"] == DBNull.Value ? 0 : Convert.ToInt32(r["CategoryId"]),
                    ProductName = r["Tên món nước"].ToString(),
                    CategoryName = r["Danh mục"].ToString(),
                    Price = Convert.ToDecimal(r["Đơn giá (VNĐ)"]),
                    IsActive = Convert.ToBoolean(r["Đang bán"]),
                    ImagePath = r["ImagePath"].ToString()
                });
            }
            return list;
        }

        public List<OrderItemDTO> GetBillDetailsByTableDto(int tableId)
        {
            DataTable dt = _posDal.GetBillDetailsByTable(tableId);
            var list = new List<OrderItemDTO>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new OrderItemDTO
                {
                    BillDetailId = Convert.ToInt32(r["BillDetailId"]),
                    ProductName = r["Tên món"].ToString(),
                    Quantity = Convert.ToInt32(r["SL"]),
                    UnitPrice = Convert.ToDecimal(r["Đơn giá"]),
                    KitchenStatus = r["Trạng thái"].ToString()
                });
            }
            return list;
        }

        public List<KitchenItemDTO> GetPendingKitchenItemsDto()
        {
            DataTable dt = _posDal.GetPendingKitchenItems();
            var list = new List<KitchenItemDTO>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new KitchenItemDTO
                {
                    BillDetailId = Convert.ToInt32(r["Mã"]),
                    TableName = r["Bàn / Đơn"].ToString(),
                    ProductName = r["Tên món cần pha"].ToString(),
                    Quantity = Convert.ToInt32(r["Số lượng"]),
                    OrderTime = r["Giờ gọi"].ToString(),
                    KitchenStatus = r["Trạng thái"].ToString()
                });
            }
            return list;
        }

        // Chuyển câu SQL từ FrmStatistics.cs về Server
        public List<BillStatDTO> GetRevenueStats(DateTime fromDate, DateTime toDate)
        {
            string sql = @"SELECT BillId, TableId, CheckInTime, CheckOutTime, PaymentMethod, DiscountPercent, TotalAmount 
                           FROM Bills 
                           WHERE Status = 1 AND CAST(CheckOutTime AS DATE) BETWEEN @tu AND @den
                           ORDER BY CheckOutTime DESC";
            SqlParameter[] p = {
                new SqlParameter("@tu", fromDate.Date),
                new SqlParameter("@den", toDate.Date)
            };

            DataTable dt = DatabaseHelper.ExecuteQuery(sql, p);
            var list = new List<BillStatDTO>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new BillStatDTO
                {
                    BillId = Convert.ToInt32(r["BillId"]),
                    TableId = Convert.ToInt32(r["TableId"]),
                    CheckInTime = Convert.ToDateTime(r["CheckInTime"]),
                    CheckOutTime = r["CheckOutTime"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["CheckOutTime"]),
                    PaymentMethod = r["PaymentMethod"] == DBNull.Value ? "Tiền mặt" : r["PaymentMethod"].ToString(),
                    DiscountPercent = r["DiscountPercent"] == DBNull.Value ? 0 : Convert.ToInt32(r["DiscountPercent"]),
                    TotalAmount = r["TotalAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(r["TotalAmount"])
                });
            }
            return list;
        }
    }
}
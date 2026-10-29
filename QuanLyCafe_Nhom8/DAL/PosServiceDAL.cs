using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using QuanLyCafe_Nhom8.DTO;

namespace QuanLyCafe_Nhom8.DAL
{
    public class PosServiceDAL
    {
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

        public DataTable GetBillDetailsByTable(int tableId)
        {
            string query = @"
                SELECT bd.BillDetailId, p.ProductName, bd.Quantity, bd.UnitPrice, (bd.Quantity * bd.UnitPrice) AS SubTotal, bd.KitchenStatus
                FROM BillDetails bd
                INNER JOIN Bills b ON bd.BillId = b.BillId
                INNER JOIN Products p ON bd.ProductId = p.ProductId
                WHERE b.TableId = @TableId AND b.Status = 0";
            return DatabaseHelper.ExecuteQuery(query, new SqlParameter[] { new SqlParameter("@TableId", tableId) });
        }

        public void AddItemToTable(int tableId, int productId, int quantity)
        {
            using (SqlConnection conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
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

                        int existingDetailId = -1;
                        using (SqlCommand cmdCheckItem = new SqlCommand("SELECT TOP 1 BillDetailId FROM BillDetails WHERE BillId = @BillId AND ProductId = @ProductId", conn, trans))
                        {
                            cmdCheckItem.Parameters.AddWithValue("@BillId", billId);
                            cmdCheckItem.Parameters.AddWithValue("@ProductId", productId);
                            object detailObj = cmdCheckItem.ExecuteScalar();
                            if (detailObj != null && detailObj != DBNull.Value)
                            {
                                existingDetailId = Convert.ToInt32(detailObj);
                            }
                        }

                        if (existingDetailId != -1)
                        {
                            using (SqlCommand cmdUpdateDetail = new SqlCommand("UPDATE BillDetails SET Quantity = Quantity + @Quantity WHERE BillDetailId = @BillDetailId", conn, trans))
                            {
                                cmdUpdateDetail.Parameters.AddWithValue("@Quantity", quantity);
                                cmdUpdateDetail.Parameters.AddWithValue("@BillDetailId", existingDetailId);
                                cmdUpdateDetail.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            using (SqlCommand cmdInsertDetail = new SqlCommand("INSERT INTO BillDetails (BillId, ProductId, Quantity, UnitPrice, KitchenStatus) VALUES (@BillId, @ProductId, @Quantity, @UnitPrice, N'Chờ pha chế')", conn, trans))
                            {
                                cmdInsertDetail.Parameters.AddWithValue("@BillId", billId);
                                cmdInsertDetail.Parameters.AddWithValue("@ProductId", productId);
                                cmdInsertDetail.Parameters.AddWithValue("@Quantity", quantity);
                                cmdInsertDetail.Parameters.AddWithValue("@UnitPrice", unitPrice);
                                cmdInsertDetail.ExecuteNonQuery();
                            }
                        }

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

        public bool CheckoutTable(int tableId, int discountPercent, out decimal finalAmount)
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

                        using (SqlCommand cmdCloseBill = new SqlCommand("UPDATE Bills SET CheckOutTime = GETDATE(), DiscountPercent = @Discount, TotalAmount = @FinalAmount, Status = 1 WHERE BillId = @BillId", conn, trans))
                        {
                            cmdCloseBill.Parameters.AddWithValue("@Discount", discountPercent);
                            cmdCloseBill.Parameters.AddWithValue("@FinalAmount", finalAmount);
                            cmdCloseBill.Parameters.AddWithValue("@BillId", billId);
                            cmdCloseBill.ExecuteNonQuery();
                        }

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
    }
}
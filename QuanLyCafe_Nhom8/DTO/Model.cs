using System;

namespace QuanLyCafe_Nhom8.DTO
{
    public class TableDTO
    {
        public int TableId { get; set; }
        public string TableName { get; set; }
        public string Status { get; set; }
    }

    public class ProductDTO
    {
        public int ProductId { get; set; }
        public int CategoryId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string ImagePath { get; set; }
    }

    public class OrderItemDTO
    {
        public int BillDetailId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal => Quantity * UnitPrice;
    }
}

public class UserDTO
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string FullName { get; set; }
    public string Role { get; set; }
    public bool IsActive { get; set; }
    public string Email { get; set; }
}

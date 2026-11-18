using System;
using System.Collections.Generic;

namespace Protocol
{
    public class UserDTO
    {
        public int UserId { get; set; }
        public int DisplayOrderId { get; set; } // Mã NV hiển thị (1, 2, 3...)
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public string Email { get; set; }
    }

    public class CategoryDTO
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
    }

    public class TableDTO
    {
        public int TableId { get; set; }
        public string TableName { get; set; }
        public string Status { get; set; }
    }

    public class ProductDTO
    {
        public int STT { get; set; }
        public int ProductId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
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
        public string KitchenStatus { get; set; }
    }

    public class KitchenItemDTO
    {
        public int BillDetailId { get; set; }
        public string TableName { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public string OrderTime { get; set; }
        public string KitchenStatus { get; set; }
    }

    public class BillStatDTO
    {
        public int BillId { get; set; }
        public int TableId { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string PaymentMethod { get; set; }
        public int DiscountPercent { get; set; }
        public decimal TotalAmount { get; set; }
    }

    // Các gói phản hồi thay thế tham số out của bản cục bộ
    public class ManagerVerifyResponseDTO
    {
        public bool IsApproved { get; set; }
        public string ManagerFullName { get; set; }
    }

    public class CheckoutResponseDTO
    {
        public bool IsSuccess { get; set; }
        public int TableId { get; set; }
        public decimal FinalAmount { get; set; }
    }

    public class QrResponseDTO
    {
        public string Base64Png { get; set; }
    }
}
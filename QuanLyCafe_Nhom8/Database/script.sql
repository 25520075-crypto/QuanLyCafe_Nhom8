-- =========================================================================
-- KHỞI TẠO CƠ SỞ DỮ LIỆU QUẢN LÝ QUÁN CAFE (CHUẨN 3NF - 7 BẢNG)
-- Đồ án Lập trình C# WinForms - Nhóm 8
-- =========================================================================

CREATE DATABASE CAFE;
GO

USE CAFE;
GO

-- 1. Bảng Users: Lưu tài khoản đăng nhập, mật khẩu mã hóa SHA-256 và phân quyền (Admin / Staff)
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(128) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Role VARCHAR(20) NOT NULL DEFAULT 'Staff',
    IsActive BIT NOT NULL DEFAULT 1
);

-- 2. Bảng Categories: Lưu danh mục nhóm đồ uống (Xóa mềm bằng cờ IsActive)
CREATE TABLE Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

-- 3. Bảng Products: Lưu thông tin món nước, giá bán và đường dẫn ảnh
CREATE TABLE Products (
    ProductId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId INT NOT NULL FOREIGN KEY REFERENCES Categories(CategoryId),
    ProductName NVARCHAR(150) NOT NULL,
    Price DECIMAL(18,0) NOT NULL CHECK (Price >= 0),
    ImagePath NVARCHAR(255) NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

-- 4. Bảng Tables: Lưu sơ đồ bàn tại quán và các kênh đơn (Mang về, ShopeeFood, XanhSM, GrabFood)
CREATE TABLE Tables (
    TableId INT IDENTITY(1,1) PRIMARY KEY,
    TableName NVARCHAR(50) NOT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT N'Trống'
);

-- 5. Bảng Bills: Lưu hóa đơn tổng của từng bàn/đơn và phương thức thanh toán (PaymentMethod)
CREATE TABLE Bills (
    BillId INT IDENTITY(1,1) PRIMARY KEY,
    TableId INT NOT NULL FOREIGN KEY REFERENCES Tables(TableId),
    UserId INT NULL FOREIGN KEY REFERENCES Users(UserId),
    CheckInTime DATETIME NOT NULL DEFAULT GETDATE(),
    CheckOutTime DATETIME NULL,
    DiscountPercent INT NOT NULL DEFAULT 0 CHECK (DiscountPercent BETWEEN 0 AND 100),
    TotalAmount DECIMAL(18,0) NOT NULL DEFAULT 0,
    PaymentMethod NVARCHAR(50) NULL DEFAULT N'Tiền mặt',
    Status INT NOT NULL DEFAULT 0 -- 0: Chưa thanh toán, 1: Đã thanh toán
);

-- 6. Bảng BillDetails: Lưu chi tiết từng món khách gọi và trạng thái pha chế (Chờ xác nhận / Chờ pha chế / Đã phục vụ)
CREATE TABLE BillDetails (
    BillDetailId INT IDENTITY(1,1) PRIMARY KEY,
    BillId INT NOT NULL FOREIGN KEY REFERENCES Bills(BillId),
    ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(ProductId),
    Quantity INT NOT NULL CHECK (Quantity > 0),
    UnitPrice DECIMAL(18,0) NOT NULL,
    KitchenStatus NVARCHAR(30) NOT NULL DEFAULT N'Chờ xác nhận'
);

-- 7. Bảng AuditLogs: Lưu lịch sử vết hủy món để chống gian lận thu ngân
CREATE TABLE AuditLogs (
    LogId INT IDENTITY(1,1) PRIMARY KEY,
    BillId INT NOT NULL,
    ProductId INT NOT NULL,
    DeletedQuantity INT NOT NULL,
    ActionType NVARCHAR(50) NOT NULL,
    LogTime DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- TRIGGER TỰ ĐỘNG GHI LỊCH SỬ VÀO BẢNG AuditLogs KHI CÓ THAO TÁC HỦY MÓN TRONG BillDetails
CREATE TRIGGER trg_AuditBillDetailDelete
ON BillDetails
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO AuditLogs (BillId, ProductId, DeletedQuantity, ActionType, LogTime)
    SELECT d.BillId, d.ProductId, d.Quantity, N'Hủy món khỏi hóa đơn', GETDATE()
    FROM deleted d;
END;
GO

-- NẠP DỮ LIỆU MẪU BAN ĐẦU (Tài khoản: admin / admin123 và thungan1 / 123456)
INSERT INTO Users (Username, PasswordHash, FullName, Role) VALUES
('admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', N'Duy Anh (Quản lý)', 'Admin'),
('thungan1', 'e6c3da5b206634d7f3f3586d747ffdb36b5c675757b380c6a5fe5c570c714349', N'Nhân viên Thu ngân', 'Staff');

INSERT INTO Categories (CategoryName) VALUES
(N'Cà phê truyền thống'),
(N'Trà trái cây'),
(N'Đá xay & Matcha');

INSERT INTO Products (CategoryId, ProductName, Price, ImagePath) VALUES
(1, N'Cà phê Đen Đá', 25000, 'Assets/Images/cf_den.png'),
(1, N'Cà phê Sữa Đá', 29000, 'Assets/Images/cf_sua.png'),
(1, N'Bạc Xỉu', 32000, 'Assets/Images/bac_xiu.png'),
(2, N'Trà Đào Cam Sả', 39000, 'Assets/Images/tra_dao.png'),u
(3, N'Matcha Đá Xay', 45000, 'Assets/Images/matcha.png');

-- Danh sách bàn phục vụ tại quán + Mua mang về + ShopeeFood/XanhSM + GrabFood
INSERT INTO Tables (TableName, Status) VALUES
(N'Bàn 01', N'Trống'),
(N'Bàn 02', N'Trống'),
(N'Bàn 03', N'Trống'),
(N'Bàn 04', N'Trống'),
(N'Bàn 05', N'Trống'),
(N'Bàn 06', N'Trống'),
(N'Bàn 07', N'Trống'),
(N'Bàn 08', N'Trống'),
(N'Mang về 01', N'Trống'),
(N'Mang về 02', N'Trống'),
(N'Mang về 03', N'Trống'),
(N'Mang về 04', N'Trống'),
(N'ShopeeFood 01', N'Trống'),
(N'ShopeeFood 02', N'Trống'),
(N'ShopeeFood 03', N'Trống'),
(N'XanhSM 01', N'Trống'),
(N'XanhSM 02', N'Trống'),
(N'XanhSM 03', N'Trống'),
(N'GrabFood 01', N'Trống'),
(N'GrabFood 02', N'Trống'),
(N'GrabFood 03', N'Trống'),
(N'GrabFood 04', N'Trống');
GO
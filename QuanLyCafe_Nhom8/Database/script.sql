CREATE DATABASE CAFE;
GO

USE CAFE;
GO

CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(128) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Role VARCHAR(20) NOT NULL DEFAULT 'Staff',
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Products (
    ProductId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId INT NOT NULL FOREIGN KEY REFERENCES Categories(CategoryId),
    ProductName NVARCHAR(150) NOT NULL,
    Price DECIMAL(18,0) NOT NULL CHECK (Price >= 0),
    ImagePath NVARCHAR(255) NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Tables (
    TableId INT IDENTITY(1,1) PRIMARY KEY,
    TableName NVARCHAR(50) NOT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT N'Trống'
);

CREATE TABLE Bills (
    BillId INT IDENTITY(1,1) PRIMARY KEY,
    TableId INT NOT NULL FOREIGN KEY REFERENCES Tables(TableId),
    UserId INT NULL FOREIGN KEY REFERENCES Users(UserId),
    CheckInTime DATETIME NOT NULL DEFAULT GETDATE(),
    CheckOutTime DATETIME NULL,
    DiscountPercent INT NOT NULL DEFAULT 0 CHECK (DiscountPercent BETWEEN 0 AND 100),
    TotalAmount DECIMAL(18,0) NOT NULL DEFAULT 0,
    Status INT NOT NULL DEFAULT 0
);

CREATE TABLE BillDetails (
    BillDetailId INT IDENTITY(1,1) PRIMARY KEY,
    BillId INT NOT NULL FOREIGN KEY REFERENCES Bills(BillId),
    ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(ProductId),
    Quantity INT NOT NULL CHECK (Quantity > 0),
    UnitPrice DECIMAL(18,0) NOT NULL,
    KitchenStatus NVARCHAR(30) NOT NULL DEFAULT N'Chờ pha chế'
);

CREATE TABLE AuditLogs (
    LogId INT IDENTITY(1,1) PRIMARY KEY,
    BillId INT NOT NULL,
    ProductId INT NOT NULL,
    DeletedQuantity INT NOT NULL,
    ActionType NVARCHAR(50) NOT NULL,
    LogTime DATETIME NOT NULL DEFAULT GETDATE()
);
GO

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
(2, N'Trà Đào Cam Sả', 39000, 'Assets/Images/tra_dao.png'),
(3, N'Matcha Đá Xay', 45000, 'Assets/Images/matcha.png');

INSERT INTO Tables (TableName, Status) VALUES
(N'Bàn 01', N'Trống'),
(N'Bàn 02', N'Trống'),
(N'Bàn 03', N'Trống'),
(N'Bàn 04', N'Trống'),
(N'Bàn 05', N'Trống'),
(N'Bàn 06', N'Trống'),
(N'Bàn 07', N'Trống'),
(N'Bàn 08', N'Trống');
GO
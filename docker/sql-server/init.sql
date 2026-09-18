-- SQL Server initialization script
-- Creates sample schema for Dialect.Samples

-- Create database
IF DB_ID('DialectSamples') IS NULL
BEGIN
    CREATE DATABASE DialectSamples;
END
GO

USE DialectSamples;
GO

-- Create Users table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        UserId INT PRIMARY KEY IDENTITY(1,1),
        Username NVARCHAR(100) NOT NULL,
        Email NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
    
    -- Create index on Email
    CREATE UNIQUE INDEX IX_Users_Email ON Users(Email);
    
    -- Insert sample data
    INSERT INTO Users (Username, Email, CreatedAt) VALUES
        ('john_doe', 'john@example.com', '2024-01-15'),
        ('jane_smith', 'jane@example.com', '2024-02-20'),
        ('bob_wilson', 'bob@example.com', '2024-03-10'),
        ('alice_johnson', 'alice@example.com', '2024-01-05');
END
GO

-- Create Products table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE Products (
        ProductId INT PRIMARY KEY IDENTITY(1,1),
        Name NVARCHAR(100) NOT NULL,
        Price DECIMAL(10, 2) NOT NULL,
        StockQuantity INT DEFAULT 0
    );
    
    -- Insert sample data
    INSERT INTO Products (Name, Price, StockQuantity) VALUES
        ('Laptop', 999.99, 50),
        ('Mouse', 29.99, 200),
        ('Keyboard', 79.99, 150),
        ('Monitor', 299.99, 75),
        ('Headphones', 149.99, 100);
END
GO

-- Create Orders table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE Orders (
        OrderId INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        OrderDate DATETIME DEFAULT GETDATE(),
        Total DECIMAL(10, 2) NOT NULL,
        FOREIGN KEY (UserId) REFERENCES Users(UserId)
    );
    
    -- Create index on UserId
    CREATE INDEX IX_Orders_UserId ON Orders(UserId);
    
    -- Insert sample data
    INSERT INTO Orders (UserId, OrderDate, Total) VALUES
        (1, '2024-06-01', 1079.97),
        (2, '2024-06-05', 129.97),
        (3, '2024-06-10', 449.97),
        (1, '2024-06-15', 299.99),
        (4, '2024-06-20', 1229.96);
END
GO

-- Create OrderItems table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderItems')
BEGIN
    CREATE TABLE OrderItems (
        OrderItemId INT PRIMARY KEY IDENTITY(1,1),
        OrderId INT NOT NULL,
        ProductId INT NOT NULL,
        Quantity INT NOT NULL,
        UnitPrice DECIMAL(10, 2) NOT NULL,
        FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
        FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
    );
    
    -- Create indexes
    CREATE INDEX IX_OrderItems_OrderId ON OrderItems(OrderId);
    CREATE INDEX IX_OrderItems_ProductId ON OrderItems(ProductId);
    
    -- Insert sample data
    INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice) VALUES
        (1, 1, 1, 999.99),  -- Laptop
        (1, 2, 3, 29.99),   -- Mouses
        (2, 2, 4, 29.99),   -- Mouses
        (3, 4, 1, 299.99),  -- Monitor
        (3, 3, 1, 79.99),   -- Keyboard
        (4, 4, 1, 299.99),  -- Monitor
        (5, 1, 1, 999.99),  -- Laptop
        (5, 6, 1, 149.99);  -- Headphones (note: ProductId 6 doesn't exist, just for demo)
END
GO

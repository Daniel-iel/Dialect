-- MySQL initialization script
-- Creates sample schema for Dialect.Samples

-- Create database
CREATE DATABASE IF NOT EXISTS dialect_samples;
USE dialect_samples;

-- Create Users table
CREATE TABLE IF NOT EXISTS Users (
    user_id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(100) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert sample data
INSERT IGNORE INTO Users (username, email, created_at) VALUES
    ('john_doe', 'john@example.com', '2024-01-15'),
    ('jane_smith', 'jane@example.com', '2024-02-20'),
    ('bob_wilson', 'bob@example.com', '2024-03-10'),
    ('alice_johnson', 'alice@example.com', '2024-01-05');

-- Create Products table
CREATE TABLE IF NOT EXISTS Products (
    product_id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    price DECIMAL(10, 2) NOT NULL,
    stock_quantity INT DEFAULT 0
);

-- Insert sample data
INSERT IGNORE INTO Products (name, price, stock_quantity) VALUES
    ('Laptop', 999.99, 50),
    ('Mouse', 29.99, 200),
    ('Keyboard', 79.99, 150),
    ('Monitor', 299.99, 75),
    ('Headphones', 149.99, 100);

-- Create Orders table
CREATE TABLE IF NOT EXISTS Orders (
    order_id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    order_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    total DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (user_id) REFERENCES Users(user_id),
    INDEX idx_user_id (user_id)
);

-- Insert sample data
INSERT IGNORE INTO Orders (user_id, order_date, total) VALUES
    (1, '2024-06-01', 1079.97),
    (2, '2024-06-05', 129.97),
    (3, '2024-06-10', 449.97),
    (1, '2024-06-15', 299.99),
    (4, '2024-06-20', 1229.96);

-- Create OrderItems table
CREATE TABLE IF NOT EXISTS OrderItems (
    order_item_id INT AUTO_INCREMENT PRIMARY KEY,
    order_id INT NOT NULL,
    product_id INT NOT NULL,
    quantity INT NOT NULL,
    unit_price DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (order_id) REFERENCES Orders(order_id),
    FOREIGN KEY (product_id) REFERENCES Products(product_id),
    INDEX idx_order_id (order_id),
    INDEX idx_product_id (product_id)
);

-- Insert sample data
INSERT IGNORE INTO OrderItems (order_id, product_id, quantity, unit_price) VALUES
    (1, 1, 1, 999.99),
    (1, 2, 3, 29.99),
    (2, 2, 4, 29.99),
    (3, 4, 1, 299.99),
    (3, 3, 1, 79.99),
    (4, 4, 1, 299.99),
    (5, 1, 1, 999.99),
    (5, 5, 1, 149.99);

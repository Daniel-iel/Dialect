-- PostgreSQL initialization script
-- Creates sample schema for Dialect.Samples

-- Create database
SELECT 'CREATE DATABASE dialect_samples'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'dialect_samples');

-- Connect to the database
\c dialect_samples;

-- Create Users table
CREATE TABLE IF NOT EXISTS users (
    user_id SERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert sample data
INSERT INTO users (username, email, created_at) VALUES
    ('john_doe', 'john@example.com', '2024-01-15'),
    ('jane_smith', 'jane@example.com', '2024-02-20'),
    ('bob_wilson', 'bob@example.com', '2024-03-10'),
    ('alice_johnson', 'alice@example.com', '2024-01-05')
ON CONFLICT (email) DO NOTHING;

-- Create Products table
CREATE TABLE IF NOT EXISTS products (
    product_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    price DECIMAL(10, 2) NOT NULL,
    stock_quantity INT DEFAULT 0
);

-- Insert sample data
INSERT INTO products (name, price, stock_quantity) VALUES
    ('Laptop', 999.99, 50),
    ('Mouse', 29.99, 200),
    ('Keyboard', 79.99, 150),
    ('Monitor', 299.99, 75),
    ('Headphones', 149.99, 100)
ON CONFLICT DO NOTHING;

-- Create Orders table
CREATE TABLE IF NOT EXISTS orders (
    order_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    order_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    total DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);

-- Create index on user_id
CREATE INDEX IF NOT EXISTS idx_orders_user_id ON orders(user_id);

-- Insert sample data
INSERT INTO orders (user_id, order_date, total) VALUES
    (1, '2024-06-01', 1079.97),
    (2, '2024-06-05', 129.97),
    (3, '2024-06-10', 449.97),
    (1, '2024-06-15', 299.99),
    (4, '2024-06-20', 1229.96)
ON CONFLICT DO NOTHING;

-- Create OrderItems table
CREATE TABLE IF NOT EXISTS order_items (
    order_item_id SERIAL PRIMARY KEY,
    order_id INT NOT NULL,
    product_id INT NOT NULL,
    quantity INT NOT NULL,
    unit_price DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (order_id) REFERENCES orders(order_id),
    FOREIGN KEY (product_id) REFERENCES products(product_id)
);

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_order_items_order_id ON order_items(order_id);
CREATE INDEX IF NOT EXISTS idx_order_items_product_id ON order_items(product_id);

-- Insert sample data
INSERT INTO order_items (order_id, product_id, quantity, unit_price) VALUES
    (1, 1, 1, 999.99),
    (1, 2, 3, 29.99),
    (2, 2, 4, 29.99),
    (3, 4, 1, 299.99),
    (3, 3, 1, 79.99),
    (4, 4, 1, 299.99),
    (5, 1, 1, 999.99),
    (5, 5, 1, 149.99)
ON CONFLICT DO NOTHING;

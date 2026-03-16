CREATE TABLE Inventory (
    warehouse_id INT,
    product_id INT,
    stock_qty INT NOT NULL,
    reserved_qty INT NOT NULL,
    PRIMARY KEY (warehouse_id, product_id),
    FOREIGN KEY (warehouse_id) REFERENCES Warehouses(warehouse_id),
    FOREIGN KEY (product_id) REFERENCES Products(product_id)
);

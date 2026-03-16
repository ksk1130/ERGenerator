CREATE TABLE Shipments (
    shipment_id INT PRIMARY KEY,
    order_id INT NOT NULL,
    warehouse_id INT NOT NULL,
    carrier_id INT NOT NULL,
    shipped_at DATE,
    delivered_at DATE,
    FOREIGN KEY (order_id) REFERENCES Orders(order_id),
    FOREIGN KEY (warehouse_id) REFERENCES Warehouses(warehouse_id),
    FOREIGN KEY (carrier_id) REFERENCES Carriers(carrier_id)
);

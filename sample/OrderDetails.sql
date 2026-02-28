CREATE TABLE OrderDetails (
    order_id INT,
    menu_id INT,
    quantity INT,
    PRIMARY KEY (order_id, menu_id),
    FOREIGN KEY (order_id) REFERENCES Orders(order_id),
    FOREIGN KEY (menu_id) REFERENCES Menus(menu_id)
);
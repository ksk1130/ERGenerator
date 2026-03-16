CREATE TABLE OrderStatusHistory (
    history_id INT PRIMARY KEY,
    order_id INT NOT NULL,
    status_name VARCHAR(40) NOT NULL,
    changed_at DATE NOT NULL,
    changed_by_user_id INT NOT NULL,
    FOREIGN KEY (order_id) REFERENCES Orders(order_id),
    FOREIGN KEY (changed_by_user_id) REFERENCES Users(user_id)
);

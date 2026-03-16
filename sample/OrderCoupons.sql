CREATE TABLE OrderCoupons (
    order_id INT,
    coupon_id INT,
    applied_at DATE,
    PRIMARY KEY (order_id, coupon_id),
    FOREIGN KEY (order_id) REFERENCES Orders(order_id),
    FOREIGN KEY (coupon_id) REFERENCES Coupons(coupon_id)
);

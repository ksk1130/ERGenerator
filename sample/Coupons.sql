CREATE TABLE Coupons (
    coupon_id INT PRIMARY KEY,
    coupon_code VARCHAR(40) NOT NULL,
    discount_type VARCHAR(20) NOT NULL,
    discount_value DECIMAL(10,2) NOT NULL,
    valid_from DATE,
    valid_to DATE
);

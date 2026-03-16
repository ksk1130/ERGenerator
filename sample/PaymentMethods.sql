CREATE TABLE PaymentMethods (
    payment_method_id INT PRIMARY KEY,
    method_name VARCHAR(80) NOT NULL,
    provider_name VARCHAR(80) NOT NULL
);

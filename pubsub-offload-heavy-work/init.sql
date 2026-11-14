CREATE TABLE orders (
  order_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  status VARCHAR(16) NOT NULL,          -- 'pending' / 'done'
  process_count INT NOT NULL DEFAULT 0, -- 重い処理が実際に走った回数（二重処理の検出用）
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  done_at DATETIME(3) NULL
);

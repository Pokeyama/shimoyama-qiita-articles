CREATE TABLE orders (
  order_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  status VARCHAR(16) NOT NULL,          -- 'pending' / 'done'
  process_count INT NOT NULL DEFAULT 0, -- doneへの更新回数を確認するため
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  done_at DATETIME(3) NULL
);

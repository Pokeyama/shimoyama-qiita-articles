#!/bin/bash
# usage: ./bench.sh sync|async [N=5]
set -eu
MODE=$1; N=${2:-5}
SQL() { docker exec pubsub-lab-mysql mysql -uroot -ppass lab -N -e "$1" 2>/dev/null; }

SQL "TRUNCATE orders"
total=0
for i in $(seq "$N"); do
  t=$(curl -s -o /dev/null -w '%{time_total}' -X POST "http://127.0.0.1:5080/orders/$MODE")
  echo "#$i ${t}s"
  total=$(echo "$total + $t" | bc)
done
echo "http total: ${total}s"

if [ "$MODE" = async ]; then
  for _ in $(seq 120); do
    [ "$(SQL "SELECT COUNT(*) FROM orders WHERE status='pending'")" = 0 ] && break
    sleep 1
  done
fi
docker exec pubsub-lab-mysql mysql -uroot -ppass lab -e "SELECT TIMESTAMPDIFF(MICROSECOND, MIN(created_at), MAX(done_at))/1000000 AS all_done_sec, SUM(status='done') AS done, SUM(process_count) AS process_count_sum FROM orders" 2>/dev/null

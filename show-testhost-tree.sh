#!/bin/zsh

while true; do
  testhost_pid=$(ps aux | grep testhost | grep -v grep | awk '{print $2}')
  if [[ -z "$testhost_pid" ]]; then
    echo "No testhost process found."
  else
    echo "testhost PID: $testhost_pid"
    if command -v pstree >/dev/null 2>&1; then
      pstree -p $testhost_pid
    else
      echo "pstree not found. Showing child processes with ps:"
      ps -o pid,ppid,command | awk -v ppid=$testhost_pid '$2 == ppid'
    fi
  fi
  sleep 2
done
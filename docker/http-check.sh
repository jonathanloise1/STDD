#!/usr/bin/env bash
# Health check for the .NET images, which have no curl or wget: GET the path on 127.0.0.1 and succeed on HTTP 200.
# Usage: http-check.sh PORT PATH
set -u
exec 3<>"/dev/tcp/127.0.0.1/${1:?port}" || exit 1
printf 'GET %s HTTP/1.0\r\nHost: localhost\r\nConnection: close\r\n\r\n' "${2:-/}" >&3
read -r -t 5 status <&3 || exit 1
case "$status" in
  "HTTP/1."*" 200"*) exit 0 ;;
  *) exit 1 ;;
esac

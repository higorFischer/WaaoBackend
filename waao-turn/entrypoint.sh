#!/bin/sh
set -e

if [ -z "$TURN_SHARED_SECRET" ]; then
	echo "ERROR: TURN_SHARED_SECRET env var is required." >&2
	exit 1
fi

sed "s|__TURN_SHARED_SECRET__|${TURN_SHARED_SECRET}|" \
	/etc/coturn/turnserver.conf.template > /etc/coturn/turnserver.conf

exec turnserver -c /etc/coturn/turnserver.conf

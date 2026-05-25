FROM coturn/coturn:4.6.2

# Drop coturn's default entrypoint helpers; we run turnserver directly with
# our own config. envsubst lets us inject the shared secret at boot.
USER root
RUN apt-get update && apt-get install -y --no-install-recommends gettext-base \
	&& rm -rf /var/lib/apt/lists/*

COPY turnserver.conf /etc/coturn/turnserver.conf.template
COPY entrypoint.sh   /usr/local/bin/waao-turn-entrypoint.sh
RUN chmod +x /usr/local/bin/waao-turn-entrypoint.sh

EXPOSE 3478/udp 3478/tcp 5349/tcp
ENTRYPOINT ["/usr/local/bin/waao-turn-entrypoint.sh"]

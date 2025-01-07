FROM ipfs/kubo

# Expose the ports for swarm and the gateway
EXPOSE 4001
EXPOSE 4001/udp
EXPOSE 5001
EXPOSE 8080

# allow requests to the gateway from any host
RUN ipfs config --json Gateway.PublicGateways '{ "localhost": {  "UseSubdomains": true }, "ipfs.io": { "UseSubdomains": true } }'

# allow request from any host. fix cors issue
RUN ipfs config --json Gateway.HTTPHeaders '{ "Access-Control-Allow-Origin": [ "*" ], "Access-Control-Allow-Methods": [ "GET", "POST" ],"Access-Control-Allow-Headers": [ "Authorization" ] }'

# set Authorizations for the gateway
RUN ipfs config --json Gateway.Authorizations '{ "root": { "AuthSecret": "basic:root:root", "AllowedPaths": [ "/api/v0" ] } }'

# This just makes sure that:
# 1. There's an fs-repo, and initializes one if there isn't.
# 2. The API and Gateway are accessible from outside the container.
ENTRYPOINT ["/sbin/tini", "--", "/usr/local/bin/start_ipfs"]

# Healthcheck for the container
# QmUNLLsPACCz1vLxQVkXqqLX5R1X345qqfHbsf67hvA3Nn is the CID of empty folder
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD ipfs --api=/ip4/127.0.0.1/tcp/5001 dag stat /ipfs/QmUNLLsPACCz1vLxQVkXqqLX5R1X345qqfHbsf67hvA3Nn || exit 1

# Execute the daemon subcommand by default
CMD ["daemon", "--migrate=true", "--agent-version-suffix=docker"]
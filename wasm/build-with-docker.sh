#!/usr/bin/env bash

# set fail on error
set -e

# Create output directory
mkdir -p ./output

# build riscv64/alpine
# docker buildx build --platform linux/riscv64 -t gameguild/riscv64-alpine -f Dockerfile.alpine .
docker build -t gameguild/alpine -f Dockerfile.alpine .

# build the builder image
docker build -t gameguild/c2w -f Dockerfile.c2w .

# pass the socket to the container, mount the current director to /app and then call c2w
#docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v $(pwd):/project gameguild/c2w c2w --to-js --target-arch=riscv64 gameguild/riscv64-alpine ./output/

docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v $(pwd):/project gameguild/c2w c2w --to-js gameguild/alpine ./output/

# Try using the standard alpine:latest tag which is more likely to be available

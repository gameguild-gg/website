#!/usr/bin/env bash

# set fail on error
set -e

# Create output directory
mkdir -p ./output

# build the container
docker buildx build --platform linux/riscv64 --load -t gameguild/alpine-gcc-riscv64 -f Dockerfile.alpine-gcc-riscv64 .

# build the builder image
docker buildx build --platform linux/amd64 -t gameguild/c2w-amd64 -f Dockerfile.c2w-amd64 . --load

# pass the socket to the container, mount the current director to /app and then call c2w
docker run --rm --platform=linux/amd64 -v /var/run/docker.sock:/var/run/docker.sock -v $(pwd):/project gameguild/c2w-amd64 c2w --to-js --target-arch=riscv64 gameguild/alpine-gcc-riscv64 ./output/


#!/usr/bin/env bash

# build the container for riscv64 platform
docker buildx build \
  --platform linux/riscv64 \
  -f Dockerfile . \
  -t gameguild/riscv64-gcc

# temp folder
mkdir -p ./output

c2w --to-js --target-arch=riscv64 gameguild/riscv64-gcc .output/


#!/bin/sh
set -e # exit on first error
set -u # exit on using unset variable

mkdir -p build

#docker build -t townsuite/msicreator:latest .
#docker buildx build --output type=image,name=townsuite/msicreator,push=true,compression=zstd .
docker buildx build -o type=docker,compression=zstd,dest=build/msicreator.tar .

# save as tar.gz
docker save townsuite/msicreator:latest | gzip > build/msicreator.tar.gz
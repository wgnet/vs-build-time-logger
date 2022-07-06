# Build our builder for the extension with the vs2019 toolchain
docker build -t vs-logger-builder:vs2019 -m 2GB --network="Default Switch" . 
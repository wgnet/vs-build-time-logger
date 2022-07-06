$VERSION = 1.0

# Build our builder for the extension with the vs2019 toolchain
docker build -t build-logger-builder-vs2019:$VERSION -m 2GB .
#!/bin/bash
# Makes every installer into Builds/Installers. Build the game for Linux and
# Windows in Unity first. Needs only podman; the packaging tools are installed
# inside a throwaway Fedora container.
set -euo pipefail
cd "$(dirname "$0")/.."
podman run --rm --security-opt label=disable -e VERSION="${1:-0.1.0}" -v "$PWD":/work -w /work \
  registry.fedoraproject.org/fedora:latest bash Packaging/build.sh

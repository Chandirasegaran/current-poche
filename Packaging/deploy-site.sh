#!/bin/bash
# Publishes the website to GitHub Pages: the landing page (Site/) at the top,
# and the playable web build under /play/. Run after make-installers.sh, which
# produces the web build's zip.
#
#   Packaging/deploy-site.sh <version>
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION="${1:?usage: deploy-site.sh <version>}"
ZIP="$PWD/Builds/Installers/CurrentPochu-${VERSION}-Web.zip"
[ -f "$ZIP" ] || { echo "missing $ZIP"; exit 1; }

WORK=$(mktemp -d)
git clone -q --branch gh-pages --depth 1 "$(git remote get-url origin)" "$WORK"
find "$WORK" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -a Site/. "$WORK/"
mkdir -p "$WORK/play" && unzip -q "$ZIP" -d "$WORK/play"
# the play page itself comes from the repo, so its title and description can change without a rebuild
sed "s/productVersion: \"[^\"]*\"/productVersion: \"$VERSION\"/" Packaging/web/index.html > "$WORK/play/index.html"
touch "$WORK/.nojekyll"
cd "$WORK"
git config user.name "$(git -C "$OLDPWD" config user.name)"
git config user.email "$(git -C "$OLDPWD" config user.email)"
git add -A
git commit -q -m "Publish the website and web build ${VERSION}"
git push -q origin gh-pages
echo "published"

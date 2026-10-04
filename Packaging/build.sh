#!/bin/bash
# Builds every installer from the games already built in Builds/Linux and
# Builds/Windows. Runs inside a Fedora container (see make-installers.sh).
set -euo pipefail

VERSION="${VERSION:-0.1.0}"
ROOT=/work
PKG=$ROOT/Packaging
OUT=$ROOT/Builds/Installers
TMP=/tmp/pkg
SKIP='*_BackUpThisFolder_ButDontShipItWithYourGame'

dnf install -y -q mingw32-nsis msitools rpm-build dpkg squashfs-tools file curl desktop-file-utils zip >/dev/null
rm -rf "$OUT" "$TMP"; mkdir -p "$OUT" "$TMP"

# ---- the Linux files, laid out the way they will be installed
STAGE=$TMP/stage
mkdir -p $STAGE/opt/currentpochu $STAGE/usr/bin $STAGE/usr/share/applications $STAGE/usr/share/icons/hicolor/256x256/apps
cp -a $ROOT/Builds/Linux/. $STAGE/opt/currentpochu/
rm -rf $STAGE/opt/currentpochu/$SKIP
chmod 755 $STAGE/opt/currentpochu/CurrentPochu.x86_64
printf '#!/bin/sh\nexec /opt/currentpochu/CurrentPochu.x86_64 "$@"\n' > $STAGE/usr/bin/currentpochu
chmod 755 $STAGE/usr/bin/currentpochu
cp $PKG/currentpochu.desktop $STAGE/usr/share/applications/
cp $PKG/currentpochu.png $STAGE/usr/share/icons/hicolor/256x256/apps/
desktop-file-validate $PKG/currentpochu.desktop

# ---- .deb
DEB=$TMP/deb; cp -a $STAGE $DEB; mkdir -p $DEB/DEBIAN
cat > $DEB/DEBIAN/control <<CONTROL
Package: currentpochu
Version: $VERSION
Section: games
Priority: optional
Architecture: amd64
Maintainer: Segar Games <Chandirasegaran@users.noreply.github.com>
Depends: libc6, libstdc++6, libgl1
Homepage: https://github.com/Chandirasegaran/current-poche
Description: A power-cut adventure for 1 to 4 friends
 Current Pochu! is a top-down pixel-art co-op adventure. The power has gone
 out in the town of Minnalpatti on the night of the cricket final, and the
 kids of the street follow the dead wires to find out why.
CONTROL
dpkg-deb --build --root-owner-group -Zxz $DEB "$OUT/currentpochu_${VERSION}_amd64.deb" >/dev/null

# ---- .rpm
rpmbuild -bb --quiet --define "_topdir $TMP/rpm" --define "stage $STAGE" --define "version_ $VERSION" $PKG/currentpochu.spec
cp $TMP/rpm/RPMS/x86_64/*.rpm "$OUT/"

# ---- AppImage
APPDIR=$TMP/CurrentPochu.AppDir; cp -a $STAGE $APPDIR
cp $PKG/currentpochu.desktop $PKG/currentpochu.png $APPDIR/
cp $PKG/currentpochu.png $APPDIR/.DirIcon
printf '#!/bin/sh\nHERE="$(dirname "$(readlink -f "$0")")"\nexec "$HERE/opt/currentpochu/CurrentPochu.x86_64" "$@"\n' > $APPDIR/AppRun
chmod 755 $APPDIR/AppRun
curl -sSL -o $TMP/appimagetool https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
chmod +x $TMP/appimagetool
ARCH=x86_64 $TMP/appimagetool --appimage-extract-and-run $APPDIR "$OUT/CurrentPochu-${VERSION}-x86_64.AppImage" >/dev/null 2>&1

# ---- the Windows files
WIN=$TMP/win; mkdir -p $WIN; cp -a $ROOT/Builds/Windows/. $WIN/; rm -rf $WIN/$SKIP

# ---- Windows setup .exe
(cd $PKG && makensis -V2 -DVERSION=$VERSION -DSRC=$WIN -DOUT="$OUT/CurrentPochu-${VERSION}-Setup.exe" currentpochu.nsi >/dev/null)

# ---- Windows .msi
(cd $WIN && find . -type f | wixl-heat -p ./ --component-group AppFiles --var var.SRC --directory-ref INSTALLDIR --win64 > $TMP/files.wxs)
wixl -a x64 -D SRC=$WIN -D VERSION=$VERSION -D Win64=yes -o "$OUT/CurrentPochu-${VERSION}.msi" $PKG/currentpochu.wxs $TMP/files.wxs

# ---- the web build (with our own page that fills the window) and the Android APK, if they were built
if [ -d $ROOT/Builds/WebGL/Build ]; then
  WEB=$TMP/web; mkdir -p $WEB; cp -a $ROOT/Builds/WebGL/Build $WEB/
  [ -d $ROOT/Builds/WebGL/StreamingAssets ] && cp -a $ROOT/Builds/WebGL/StreamingAssets $WEB/
  sed "s/productVersion: \"[^\"]*\"/productVersion: \"$VERSION\"/" $PKG/web/index.html > $WEB/index.html
  (cd $WEB && zip -qr "$OUT/CurrentPochu-${VERSION}-Web.zip" .)
fi
APK=$(ls -t $ROOT/Builds/Android/*.apk 2>/dev/null | head -1 || true)
[ -n "$APK" ] && cp "$APK" "$OUT/CurrentPochu-${VERSION}-Android.apk"

# ---- plain zips, for people who don't want an installer
(cd $WIN && zip -qr "$OUT/CurrentPochu-${VERSION}-Windows-portable.zip" .)
(cd $STAGE/opt/currentpochu && zip -qr "$OUT/CurrentPochu-${VERSION}-Linux-portable.zip" .)

# ---- the same files again under names without the version, so the website's
#      download buttons (github.com/.../releases/latest/download/<name>) never change
ln "$OUT/CurrentPochu-${VERSION}-Setup.exe" "$OUT/CurrentPochu-Setup.exe"
ln "$OUT/CurrentPochu-${VERSION}.msi" "$OUT/CurrentPochu.msi"
ln "$OUT/currentpochu_${VERSION}_amd64.deb" "$OUT/currentpochu_amd64.deb"
ln "$OUT/currentpochu-${VERSION}-1.x86_64.rpm" "$OUT/currentpochu.x86_64.rpm"
ln "$OUT/CurrentPochu-${VERSION}-x86_64.AppImage" "$OUT/CurrentPochu-x86_64.AppImage"
ln "$OUT/CurrentPochu-${VERSION}-Windows-portable.zip" "$OUT/CurrentPochu-Windows-portable.zip"
ln "$OUT/CurrentPochu-${VERSION}-Linux-portable.zip" "$OUT/CurrentPochu-Linux-portable.zip"
[ -f "$OUT/CurrentPochu-${VERSION}-Android.apk" ] && ln "$OUT/CurrentPochu-${VERSION}-Android.apk" "$OUT/CurrentPochu-Android.apk"

(cd "$OUT" && sha256sum * > SHA256SUMS.txt)
ls -la "$OUT"

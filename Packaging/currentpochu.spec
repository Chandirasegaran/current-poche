# RPM for Current Pochu! Packages the already-built game from the staging folder; nothing is compiled.
%global debug_package %{nil}
%global __os_install_post %{nil}
%global _build_id_links none
AutoReqProv: no

Name:           currentpochu
Version:        %{version_}
Release:        1
Summary:        A power-cut adventure for 1 to 4 friends
License:        GPL-2.0-only
URL:            https://github.com/Chandirasegaran/current-poche
BuildArch:      x86_64

%description
Current Pochu! is a top-down pixel-art co-op adventure by Segar Games. The power
has gone out in the town of Minnalpatti on the night of the cricket final, and
the kids of the street follow the dead wires to find out why.

%install
mkdir -p %{buildroot}
cp -a %{stage}/opt %{stage}/usr %{buildroot}/

%files
/opt/currentpochu
/usr/bin/currentpochu
/usr/share/applications/currentpochu.desktop
/usr/share/icons/hicolor/256x256/apps/currentpochu.png

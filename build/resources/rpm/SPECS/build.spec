Name: sourcefork
Version: %_version
Release: 1
Summary: Open-source & Free Git Gui Client
License: MIT
URL: https://github.com/whynbnb/SourceFork
Source: https://github.com/whynbnb/SourceFork/archive/refs/tags/v%_version.tar.gz
Requires: libX11.so.6()(%{__isa_bits}bit)
Requires: libSM.so.6()(%{__isa_bits}bit)
Requires: libicu
Requires: xdg-utils

%define _build_id_links none

%description
Open-source & Free Git Gui Client

%install
mkdir -p %{buildroot}/opt/sourcefork
mkdir -p %{buildroot}/%{_bindir}
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons
cp -f %{_topdir}/../../SourceFork/* %{buildroot}/opt/sourcefork/
ln -rsf %{buildroot}/opt/sourcefork/sourcefork %{buildroot}/%{_bindir}
cp -r %{_topdir}/../_common/applications %{buildroot}/%{_datadir}
cp -r %{_topdir}/../_common/icons %{buildroot}/%{_datadir}
chmod 755 -R %{buildroot}/opt/sourcefork
chmod 755 %{buildroot}/%{_datadir}/applications/sourcefork.desktop

%files
%dir /opt/sourcefork/
/opt/sourcefork/*
/usr/share/applications/sourcefork.desktop
/usr/share/icons/*
%{_bindir}/sourcefork

%changelog
# skip

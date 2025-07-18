FROM ubuntu:noble

ARG DEBIAN_FRONTEND=noninteractive

LABEL maintainer="Peter Gill <peter.gill@townsuite.com>"

# Install Wine
RUN apt update && dpkg --add-architecture i386 \
    && apt update \
    && apt install -y --no-install-recommends software-properties-common gnupg2 zip bc wget curl \
    && mkdir -p /tmp && cd /tmp/ \
    && apt install -y wine winbind cabextract xvfb \
    && mkdir -p /${WINEPREFIX} \
    && apt-get clean \
    && rm -rf /tmp/* \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Turn off Fixme warnings
#ENV WINEDEBUG=fixme-all

# Setup a Wine prefix
ENV WINEPREFIX=/townsuite-wine
ENV WINEARCH=win64
RUN mkdir -p /townsuite-wine \
    && winecfg && wineboot -u \
    # download an install from https://builds.dotnet.microsoft.com/dotnet/Runtime/8.0.17/dotnet-runtime-8.0.17-win-x64.exe
    && wget --user-agent="Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:133.0) Gecko/20100101 Firefox/133.0" -O /tmp/windowsdesktop-runtime-8.0.11-win-x64.exe https://download.visualstudio.microsoft.com/download/pr/27bcdd70-ce64-4049-ba24-2b14f9267729/d4a435e55182ce5424a7204c2cf2b3ea/windowsdesktop-runtime-8.0.11-win-x64.exe \
    && wineserver -k
RUN  xvfb-run wine /tmp/windowsdesktop-runtime-8.0.11-win-x64.exe /quiet /install /norestart \& \
    && sleep 60 \
    && rm /tmp/windowsdesktop-runtime-8.0.11-win-x64.exe
    


COPY ./build /townsuite-msicreator

# example to run app and volume mount the host folder:  docker run -v "${env.WORKSPACE}:/build" townsuite/msicreator arg1 arg2
ENTRYPOINT ["wine", "/townsuite-msicreator/msicreator.exe"]
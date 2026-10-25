FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY AbsoluteBot/AbsoluteBot.csproj AbsoluteBot/
RUN dotnet restore AbsoluteBot/AbsoluteBot.csproj -r linux-x64

COPY AbsoluteBot/ AbsoluteBot/
RUN dotnet publish AbsoluteBot/AbsoluteBot.csproj \
    -c Release \
    -o /app/publish \
    -r linux-x64 \
    --self-contained false \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata libgomp1 ffmpeg \
    && ln -snf /usr/share/zoneinfo/Europe/Vienna /etc/localtime \
    && echo Europe/Vienna > /etc/timezone \
    && test -e /lib/x86_64-linux-gnu/ld-linux-x86-64.so.2 \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=Europe/Vienna \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://0.0.0.0:5000

WORKDIR /app

COPY --from=build /app/publish /opt/absolutebot

EXPOSE 5000

ENTRYPOINT ["dotnet", "/opt/absolutebot/AbsoluteBot.dll"]

# ============================================================================
# FlowDesk backend — üretim imajı (Faz 20)
# ----------------------------------------------------------------------------
# Tek Dockerfile, iki hedef: API ve Worker. İkisi de aynı kaynaktan derleniyor
# ve yalnızca yayınlanan proje farklı; ayrı dosyalar aynı adımları iki kez
# yazmak olurdu.
#
# Derleme bağlamı depo kökü:
#   docker build -f backend/Dockerfile --target api --build-arg PROJECT=...
# ============================================================================

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS build
WORKDIR /src

# Önce yalnızca proje ve paket dosyaları: kaynak değiştiğinde restore katmanı
# yeniden kullanılır.
# .editorconfig derlemenin parçası: kural şiddetleri orada ayarlanıyor ve
# uyarılar hata sayılıyor (Directory.Build.props). Kopyalanmazsa imajda başka
# bir kural kümesiyle derlenir ve derleme düşer.
COPY global.json .editorconfig ./
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/FlowDesk.slnx backend/
COPY backend/src/FlowDesk.Domain/FlowDesk.Domain.csproj backend/src/FlowDesk.Domain/
COPY backend/src/FlowDesk.Application/FlowDesk.Application.csproj backend/src/FlowDesk.Application/
COPY backend/src/FlowDesk.Infrastructure/FlowDesk.Infrastructure.csproj backend/src/FlowDesk.Infrastructure/
COPY backend/src/FlowDesk.Api/FlowDesk.Api.csproj backend/src/FlowDesk.Api/
COPY backend/src/FlowDesk.Worker/FlowDesk.Worker.csproj backend/src/FlowDesk.Worker/

ARG PROJECT=backend/src/FlowDesk.Api/FlowDesk.Api.csproj
RUN dotnet restore "${PROJECT}"

COPY backend/src backend/src

# Testler imaja girmez; onları CI çalıştırır (docs/ARCHITECTURE.md).
RUN dotnet publish "${PROJECT}" --no-restore -c Release -o /app

# ----------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble AS api
WORKDIR /app

# Türkçe biçimlendirme ve harf katlaması kültür verisine bağlı; invariant mod
# kapalı (ADR-0025), dolayısıyla ICU imajda bulunmalı.
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app .

# Kök olmayan kullanıcı, imajın kendi hazır kullanıcısı.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "FlowDesk.Api.dll"]

# ----------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION}-noble AS worker
WORKDIR /app

ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app .

USER $APP_UID

# Worker HTTP portu açmaz: metrikleri kendisi gönderir (ADR-0042).
ENTRYPOINT ["dotnet", "FlowDesk.Worker.dll"]

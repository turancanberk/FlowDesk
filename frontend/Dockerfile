# ============================================================================
# FlowDesk frontend — üretim imajı (Faz 20)
# ----------------------------------------------------------------------------
# Next'in "standalone" çıktısı: imaj yalnızca sunucunun gerçekten kullandığı
# node_modules'ü taşır.
#
# NEXT_PUBLIC_API_BASE_URL derleme anında gömülür. Üretimde frontend ile API
# aynı origin altında (Caddy), bu yüzden varsayılanı boş: tarayıcı istekleri
# göreli yola gider.
# ============================================================================

ARG NODE_VERSION=22.22.0

FROM node:${NODE_VERSION}-bookworm-slim AS deps
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

FROM node:${NODE_VERSION}-bookworm-slim AS build
WORKDIR /app

ENV NEXT_TELEMETRY_DISABLED=1

COPY --from=deps /app/node_modules ./node_modules
COPY frontend/ ./

ARG NEXT_PUBLIC_API_BASE_URL=""
ENV NEXT_PUBLIC_API_BASE_URL=${NEXT_PUBLIC_API_BASE_URL}

RUN npm run build

# ----------------------------------------------------------------------------

FROM node:${NODE_VERSION}-bookworm-slim AS runtime
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# HSTS yalnızca TLS'in arkasında anlamlı; Caddy'nin arkasında açılır.
ENV FLOWDESK_ENABLE_HSTS=true

COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static

USER node
EXPOSE 3000

ENTRYPOINT ["node", "server.js"]

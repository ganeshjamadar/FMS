# ──────────────────────────────────────────────
# Dockerfile for FundManager Web App
# Build from repo root:
#   docker build -f src/web/fund-manager-web/Dockerfile \
#     --build-arg WK_ARTIFACTORY_EMAIL=you@example.com \
#     --build-arg WK_ARTIFACTORY_TOKEN=<token> \
#     -t fundmanager-web .
# ──────────────────────────────────────────────

# ── Stage 1: Build ──
FROM node:20-alpine AS build
WORKDIR /app

# Private registry credentials (passed as build args)
ARG WK_ARTIFACTORY_EMAIL
ARG WK_ARTIFACTORY_TOKEN

# Copy .npmrc (uses env-var placeholders) + package files
COPY src/web/fund-manager-web/.npmrc src/web/fund-manager-web/package.json src/web/fund-manager-web/package-lock.json* ./

# npm expands ${WK_ARTIFACTORY_EMAIL} and ${WK_ARTIFACTORY_TOKEN} from env
ENV WK_ARTIFACTORY_EMAIL=${WK_ARTIFACTORY_EMAIL}
ENV WK_ARTIFACTORY_TOKEN=${WK_ARTIFACTORY_TOKEN}

RUN npm ci

# Remove .npmrc so credentials don't leak into later layers
RUN rm -f .npmrc

COPY src/web/fund-manager-web/ .

# API base URL at build time — gateway is proxied via nginx in the container
ARG VITE_API_URL=/api
ENV VITE_API_URL=${VITE_API_URL}

RUN npm run build

# ── Stage 2: Serve via nginx ──
FROM nginx:1.27-alpine AS runtime

# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf

# Add custom nginx config
COPY src/web/fund-manager-web/nginx.conf /etc/nginx/conf.d/default.conf

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

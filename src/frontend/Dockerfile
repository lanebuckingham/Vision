# Vision — frontend (Next.js)
#
# Multi-stage build using Next.js "standalone" output so the runtime image
# carries only the built app + the node_modules it actually needs, not the
# full dev toolchain or dependency tree.
#
# Build context is this directory (src/frontend), not the repository root:
#   docker build -f src/frontend/Dockerfile -t vision-frontend src/frontend
#
# IMPORTANT: NEXT_PUBLIC_* values are inlined into the browser bundle at BUILD
# time, not read at container start time. Pass them as build args pointing at
# host-reachable URLs (e.g. http://localhost:5163), not Docker-internal
# service DNS names — the browser, not another container, makes these calls.
# These are public browser configuration values, not secrets.

# ---- Dependencies stage ---------------------------------------------------
FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# ---- Build stage -----------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

ARG NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_WORK_ORDER_API_URL
ARG NEXT_PUBLIC_CREDENTIAL_API_URL
ARG NEXT_PUBLIC_COGNITO_DOMAIN
ARG NEXT_PUBLIC_COGNITO_CLIENT_ID
ARG NEXT_PUBLIC_COGNITO_REDIRECT_URI
ARG NEXT_PUBLIC_COGNITO_LOGOUT_URI

ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}
ENV NEXT_PUBLIC_WORK_ORDER_API_URL=${NEXT_PUBLIC_WORK_ORDER_API_URL}
ENV NEXT_PUBLIC_CREDENTIAL_API_URL=${NEXT_PUBLIC_CREDENTIAL_API_URL}
ENV NEXT_PUBLIC_COGNITO_DOMAIN=${NEXT_PUBLIC_COGNITO_DOMAIN}
ENV NEXT_PUBLIC_COGNITO_CLIENT_ID=${NEXT_PUBLIC_COGNITO_CLIENT_ID}
ENV NEXT_PUBLIC_COGNITO_REDIRECT_URI=${NEXT_PUBLIC_COGNITO_REDIRECT_URI}
ENV NEXT_PUBLIC_COGNITO_LOGOUT_URI=${NEXT_PUBLIC_COGNITO_LOGOUT_URI}

COPY --from=deps /app/node_modules ./node_modules
COPY . .
# Keep an empty public/ so the runtime COPY succeeds when there are no static assets.
RUN mkdir -p public && npm run build

# ---- Runtime stage ----------------------------------------------------------
FROM node:22-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

# node:alpine ships a non-root "node" user by default.
RUN addgroup --system --gid 1001 nodejs \
    && adduser --system --uid 1001 nextjs

COPY --from=build /app/public ./public
COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static

USER nextjs

EXPOSE 3000

ENTRYPOINT ["node", "server.js"]

# syntax=docker/dockerfile:1.6

FROM node:22-alpine AS deps
RUN corepack enable && corepack prepare pnpm@10.0.0 --activate
WORKDIR /repo

COPY pnpm-workspace.yaml package.json .npmrc* ./
COPY apps/ops/package.json ./apps/ops/
COPY packages/infrastructure/ui ./packages/infrastructure/ui
COPY packages/tooling ./packages/tooling

RUN pnpm install --no-frozen-lockfile --prefer-offline

FROM deps AS builder
WORKDIR /repo
ENV NEXT_TELEMETRY_DISABLED=1
ENV NODE_OPTIONS=--max-old-space-size=4096

COPY . .

RUN pnpm --filter @game-guild/ops build

FROM node:22-alpine AS runner

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

WORKDIR /app

RUN addgroup --system --gid 1001 nodejs \
    && adduser --system --uid 1001 nextjs

COPY --from=builder --chown=nextjs:nodejs /repo/apps/ops/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /repo/apps/ops/.next/static ./apps/ops/.next/static

USER nextjs
EXPOSE 3000

CMD ["node", "apps/ops/server.js"]

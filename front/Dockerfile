FROM node:16-bullseye-slim

WORKDIR /app

COPY . /app

RUN npm install -g pnpm

RUN pnpm install
RUN pnpm run build

ARG PORT=3000

EXPOSE $PORT

CMD [ "pnpm", "run", "start" ]
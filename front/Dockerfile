FROM node:16-bullseye-slim

# I am not proud of this work. todo: fix this to use multi-stage builds and remove multiple tsconfigs for build

WORKDIR /app

COPY . /app

ARG NODE_ENV=production

RUN npm install -g pnpm
RUN NODE_ENV=production pnpm install --production
RUN NODE_ENV=production pnpm run build

ARG PORT=3000

EXPOSE $PORT

CMD [ "pnpm", "run", "start" ]
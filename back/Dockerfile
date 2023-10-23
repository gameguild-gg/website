FROM node:lts

## add cmake and build-essential
RUN apt update -y
RUN apt install -y cmake build-essential

WORKDIR /app

COPY . /app

RUN npm install -g pnpm

ENV NODE_ENV=development

RUN pnpm install
RUN pnpm run build

ENV NODE_ENV=production

ARG PORT=3000

EXPOSE $PORT

CMD [ "pnpm", "run", "start:prod" ]
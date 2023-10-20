FROM node:lts

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
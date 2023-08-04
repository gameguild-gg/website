FROM node:lts

WORKDIR /app

COPY . /app

RUN npm install
RUN npm run build

ARG PORT=8080

EXPOSE $PORT

CMD [ "npm", "run", "start" ]
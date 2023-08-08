FROM node:16-bullseye-slim

WORKDIR /app

COPY . /app

RUN npm install
# RUN npm run build

ARG PORT=3000

EXPOSE $PORT

#CMD [ "npm", "run", "start" ]
CMD [ "npm", "run", "dev" ]
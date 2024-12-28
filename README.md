# Game Guild Platform

Game Guild is a game dev community. We aim to revolutionize the way game developers collaborate, learn, and monetize their creations. Our platform will provide a space for developers to showcase their games, connect with other creators, and access resources for skill development. Users can discover new games, playtest them, and support their favorite developers through in-game purchases or donations.

## How to install setup

### Setup Windows/MacOS:

1. Install Docker;
2. install Node.js. I am using version 18, probably other versions will work as well;
3. Start the database with the command line on the root of the repo `docker-compose up -d adminer`;
4. Ask a teammate the `.env` files;
5. run `npm install` on the root of the repo to install the dependencies;
6. run `npm run start:both` on the root of the repo to start both front and back-end;

### Setup GNU/Linux:

1. Install Docker, view: https://docs.docker.com/engine/install/

2. Install Node.js, version 20. Recommend use NVM to version a correct nodejs version for this project:

```bash
# installs nvm (Node Version Manager)
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.0/install.sh | bash
# download and install Node.js (you may need to restart the terminal)
nvm install 20
# verifies the right Node.js version is in the environment
node -v # should print `v20.18.1`
# verifies the right npm version is in the environment
npm -v # should print `10.8.2`
```

3. Start the database with the command line on the root of the repo `sudo docker compose up -d adminer`;
4. Ask a teammate the `.env` files;
5. Run `npm install` on the root of the repo to install the dependencies;

6. Two ways to run:

- Run `npm run start:both` on the root of the repo to start both front and back-end; or

- Run `npm run start:both` to start back-end and in new terminal, run `npm run dev:web` to start front-end;

## Under construction
Here are some current stages of the website.
![screenshot](documentation/Page1.png)

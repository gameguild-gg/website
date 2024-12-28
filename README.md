![GitHub Stars](https://img.shields.io/github/stars/gameguild-gg/website?style=social)
![Version](https://img.shields.io/github/package-json/v/gameguild-gg/website)
![Repo Size](https://img.shields.io/github/repo-size/gameguild-gg/website)
![GitHub Issues](https://img.shields.io/github/issues/gameguild-gg/website)
![Last Commit](https://img.shields.io/github/last-commit/gameguild-gg/website)
![Contributors](https://img.shields.io/github/contributors/gameguild-gg/website)
![Languages](https://img.shields.io/github/languages/top/gameguild-gg/website)
![Website Uptime 30d](https://status.gameguild.gg/api/badge/1/uptime/720?label=Uptime%20Web%20(30d))
![Api Uptime 30d](https://status.gameguild.gg/api/badge/3/uptime/720?label=Uptime%20Api%20(30d))

# Game Guild Platform

We are a **game dev** community.
Our main goal is to revolutionize the way game developers:
- **Collaborate**: with others in workshops and lectures online and in person.
- **Learn**: learn from mentors and other more experienced developers.
- **Monetize**: their creations.

Our platform will provide a space for developers to showcase their games, connect with other creators, and access resources for skill development. Users can discover new games, playtest them, and support their favorite developers through in-game purchases or donations.

![screenshot](documentation/Page1.png)

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

## How You Can Contribute
We’re actively seeking contributors to help us improve and expand the platform. Here’s how you can get involved:
- **Report Issues**: Found a bug or have a suggestion? Open an issue!
- **Contribute Code**: Take on issues labeled good first issue.
- **Share the Project**: Star the repo and spread the word to fellow developers!

![Next.js](https://img.shields.io/badge/Next.js-000000?style=for-the-badge&logo=nextdotjs&logoColor=white)
![NestJS](https://img.shields.io/badge/NestJS-E0234E?style=for-the-badge&logo=nestjs&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)

## Why Star This Project?
By starring this repository, you:
- Show support for the project.
- Help increase visibility, attracting more contributors and collaborators.
- Join a growing community shaping the future of game development.

<div style="text-align: center; margin-top: 20px;">
  <!-- <a href="" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/color/48/000000/youtube-play.png" alt="YouTube" style="vertical-align: middle;"/>
  </a> -->
  <a href="https://chat.whatsapp.com/CAboWKtosP673f9EkzxKNb" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/color/48/000000/whatsapp.png" alt="WhatsApp" style="vertical-align: middle;"/>
  </a>
  <a href="https://discord.com/invite/9CdJeQ2XKB?ref=gameguild.gg" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/color/48/000000/discord-logo.png" alt="Discord" style="vertical-align: middle;"/>
  </a>
</div>

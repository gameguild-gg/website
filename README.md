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

<div class="social-links">
  <style>
    #socialicon img{
      width: 64px;
    }
    .social-links {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      text-align: center;
      margin-top: 20px;
    }
  </style>
  <!-- YouTube -->
  <a id="socialicon" href="https://www.youtube.com/@AwesomeGamedevGuild" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/color/48/000000/youtube-play.png" alt="YouTube" style="vertical-align: middle;"/>
  </a>
  <!-- WhatsApp -->
  <a id="socialicon" href="https://chat.whatsapp.com/CAboWKtosP673f9EkzxKNb" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/color/48/000000/whatsapp.png" alt="WhatsApp" style="vertical-align: middle;"/>
  </a>
  <!-- Instagram -->
  <a id="socialicon" href="" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=zezJrErrmcwx&format=png&color=000000" alt="Instagram" style="vertical-align: middle;"/>
  </a>
  <!-- Facebook -->
  <!-- <a id="socialicon" href="https://x.com/GameGuildDev" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=13912&format=png&color=000000" alt="Facebook" style="vertical-align: middle;"/>
  </a> -->
  <!-- LinkedIn -->
  <!-- <a id="socialicon" href="https://x.com/GameGuildDev" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=8808&format=png&color=000000" alt="LinkedIn" style="vertical-align: middle;"/>
  </a> -->
  <!-- X -->
  <a id="socialicon" href="https://x.com/GameGuildDev" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=phOKFKYpe00C&format=png&color=000000" alt="X" style="vertical-align: middle;"/>
  </a>
  <!-- Threads -->
  <!-- <a id="socialicon" href="" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=oykyblY20T6o&format=png&color=000000" alt="Threads" style="vertical-align: middle;"/>
  </a> -->
  <!-- Discord -->
  <a id="socialicon" href="https://discord.com/invite/9CdJeQ2XKB?ref=gameguild.gg" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/color/48/000000/discord-logo.png" alt="Discord" style="vertical-align: middle;"/>
  </a>
  <!-- BlueSky -->
  <a id="socialicon" href="https://bsky.app/profile/gameguild.bsky.social" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=3ovMFy5JDSWq&format=png&color=000000" alt="BlueSky" style="vertical-align: middle;"/>
  </a>
  <!-- TikTok -->
  <a id="socialicon" href="https://www.tiktok.com/@awesomegameguild" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=3veRWJpxPPDH&format=png&color=000000" alt="TikTok" style="vertical-align: middle;"/>
  </a>
  <!-- Patreon -->
  <a id="socialicon" href="https://mastodon.social/@gameguild" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=I49RSKuKXYoP&format=png&color=000000" alt="Patreon" style="vertical-align: middle;"/>
  </a>
  <!-- Mastodon -->
  <a id="socialicon" href="https://mastodon.social/@gameguild" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=SjG6BzZwdP2-&format=png&color=000000" alt="Mastodon" style="vertical-align: middle;"/>
  </a>
  <!-- Twitch -->
  <a id="socialicon" href="https://www.twitch.tv/awesomegamedevguild" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=MFZCdvQbJtV1&format=png&color=000000" alt="Twitch" style="vertical-align: middle;"/>
  </a>
  <!-- Itch.io -->
  <a id="socialicon" href="http://gameguild.itch.io/" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=XrWrgAx9pAYM&format=png&color=000000" alt="Itch.io" style="vertical-align: middle;"/>
  </a>
  <!-- GameJolt -->
  <a id="socialicon" href="https://gamejolt.com/@GameGuild" target="_blank" style="text-decoration: none; margin: 0 15px;">
    <img src="https://img.icons8.com/?size=100&id=QxjoLwAXiCXT&format=png&color=000000" alt="GameJolt" style="vertical-align: middle;"/>
  </a>
</div>
Icons by [Icons8](https://icons8.com/)

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=gameguild-gg/website&type=Date)](https://star-history.com/#gameguild-gg/website&Date)

## Gource

[![Gource](https://gameguild-gg.github.io/website/gource.gif)](https://gameguild-gg.github.io/website/gource.mp4)

## License

This project is available under a dual-license model:

1. **Open Source License:** [GNU AGPL v3.0](./LICENSE)
    - For non-commercial, open-source use or commercial with less than 1000 users.
    - Must comply with AGPL terms, including providing source code for modifications.

2. **Commercial License:** [Commercial License](./COMMERCIAL_LICENSE.md)
    - For commercial use exceeding 1000 users.
    - Contact us for terms on discord.
 

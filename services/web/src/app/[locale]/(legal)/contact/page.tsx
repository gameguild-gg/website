import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer-new/markdown-renderer-new';

const markdownContent: string = `
# 🎮 Contact - GameGuild

Get in touch with us and help make **GameGuild** even better! We are here to answer questions, receive suggestions, and collaborate with you.

---

## 🗨️ Community

Join our community to discuss ideas, report issues, and share feedback:

- **GitHub Discussions:** [Join here](https://github.com/gameguild-gg/website/discussions)
- **GitHub Issues:** [Report an issue](https://github.com/gameguild-gg/website/issues)
- **Discord:** [Join the server](https://discord.gg/QtRTgYRm)

---

## 🎮 Our Games

- **Itchio:** [@GameGuild](https://gameguild.itch.io/)
- **GameJolt:** [@GameGuild](https://gamejolt.com/@GameGuild)

---

## 🖇️ Social Media

Follow **GameGuild GG** for the latest updates and stay informed:

- **X (formerly Twitter):** [@GameGuildGG](https://x.com/GameGuildDev)
- **LinkedIn:** [GameGuild GG](https://www.linkedin.com/company/gameguild-gg)
- **Instagram:** [@GameGuild](https://instagram.com/gameguild)
- **Facebook:** [@GameGuildGG](https://facebook.com/gameguildgg)
- **Mastodon:** [@GameGuildGG](https://mastodon.social/@GameGuild)
- **Reddit:** [@GameGuildGG](https://reddit.com/user/GameGuildGG)
- **Twitch:** [@GameGuildGG](https://twitch.tv/GameGuildGG)
- **Tiktok:** [@GameGuildGG](https://tiktok.com/@GameGuildGG)
- **BlueSky:** [@GameGuildGG](https://bsky.app/profile/GameGuild)
- **YouTube:** [@GameGuildGG](https://youtube.com/@GameGuildGG)

---

## 📧 Email

For direct inquiries, feel free to contact us via **Discord**, by user @tolstenko.

`;

export default function Page(): JSX.Element {
  return <MarkdownRenderer content={markdownContent} />;
}

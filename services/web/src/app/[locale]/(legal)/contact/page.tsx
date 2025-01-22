import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer-new/markdown-renderer-new';

const markdownContent: string = `
# 🎮 Contact - GameGuild

Get in touch with us and help make **GameGuild** even better! We are here to answer questions, receive suggestions, and collaborate with you.

---

## 🗨️ Community

Join our community to discuss ideas, report issues, and share feedback:

- [**Discord**](https://discord.com/invite/9CdJeQ2XKB?ref=gameguild.gg)
- [**WhatsApp**](https://chat.whatsapp.com/CAboWKtosP673f9EkzxKNb)

---

## 🎮 Our Games

- [**Itchio**](https://gameguild.itch.io/)
- [**GameJolt**](https://gamejolt.com/@GameGuild)

---

## 🖇️ Social Media

Follow **GameGuild GG** for the latest updates and stay informed:

- [**X (formerly Twitter)**](https://x.com/GameGuildDev)
- [**LinkedIn**](https://www.linkedin.com/company/gameguild-gg)
- [**Instagram**](https://instagram.com/gameguild)
- [**Facebook**](https://facebook.com/gameguildgg)
- [**Mastodon**](https://mastodon.social/@gameguild)
- [**Reddit**](https://reddit.com/user/GameGuildGG)
- [**Tiktok**](https://tiktok.com/@GameGuildGG)
- [**BlueSky**](https://bsky.app/profile/gameguild.bsky.social)
- [**Twitch**](https://www.twitch.tv/awesomegamedevguild)
- [**YouTube**](https://www.youtube.com/@AwesomeGamedevGuild)

---
## Our Project Repository

- **GitHub Discussions:** [Join here](https://github.com/gameguild-gg/website/discussions)
- **GitHub Issues:** [Report an issue](https://github.com/gameguild-gg/website/issues)

---

## 📧 Formal Contact

For direct inquiries, feel free to contact us via [**Discord**](https://discord.com/invite/9CdJeQ2XKB?ref=gameguild.gg), by user @tolstenko.

`;

export default function Page(): JSX.Element {
  return <MarkdownRenderer content={markdownContent} />;
}

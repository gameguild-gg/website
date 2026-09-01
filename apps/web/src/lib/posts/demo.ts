import type { PostCardData } from "./queries";

export interface SocialStoryPreview {
  id: string;
  name: string;
  handle: string;
  accent: string;
}

export interface SocialPlaytestPreview {
  title: string;
  detail: string;
  date: string;
  href: string;
}

export interface SocialCreatorPreview {
  name: string;
  handle: string;
  focus: string;
}

export const demoSocialStories: SocialStoryPreview[] = [
  {
    id: "story-pixel",
    name: "Pixel Pioneer",
    handle: "@pixelpioneer",
    accent: "from-amber-300 via-rose-500 to-violet-600",
  },
  {
    id: "story-starline",
    name: "Starline Devs",
    handle: "@starlinedevs",
    accent: "from-cyan-300 via-blue-500 to-violet-600",
  },
  {
    id: "story-bram",
    name: "Bram S.",
    handle: "@bram_dev",
    accent: "from-emerald-300 via-sky-500 to-blue-700",
  },
  {
    id: "story-mana",
    name: "ManaVoid",
    handle: "@manavoid",
    accent: "from-fuchsia-400 via-violet-500 to-indigo-700",
  },
  {
    id: "story-arcade",
    name: "Indie Arcade",
    handle: "@indiearcade",
    accent: "from-orange-300 via-rose-500 to-cyan-400",
  },
];

export const demoSocialPlaytests: SocialPlaytestPreview[] = [
  {
    title: "Mythwake",
    detail: "Co-op dungeon crawler · 14 going",
    date: "May 24 · 6:00 PM",
    href: "/testing-lab",
  },
  {
    title: "Gardenbound",
    detail: "Cozy farming sim · 9 going",
    date: "May 25 · 3:00 PM",
    href: "/testing-lab",
  },
  {
    title: "Skyline Riders",
    detail: "Arcade racing · 22 going",
    date: "May 26 · 7:00 PM",
    href: "/testing-lab",
  },
];

export const demoSocialCreators: SocialCreatorPreview[] = [
  { name: "Pixel Pioneer", handle: "@pixelpioneer", focus: "Gameplay systems" },
  { name: "ManaVoid", handle: "@manavoid_studios", focus: "Technical art" },
  { name: "Indie Arcade", handle: "@indiearcade", focus: "Indie publishing" },
];

export const demoSocialPosts: PostCardData[] = [
  {
    id: "demo-neon-rift",
    authorId: "demo-marina",
    authorName: "Marina Costa",
    authorHandle: "@marinacodes",
    communityLabel: "Luna Park",
    content:
      "New drift physics are finally live. Looking for feedback on controller feel.\n\n#indiedev #playtesting",
    mediaUrl:
      "https://images.unsplash.com/photo-1511512578047-dfb367046420?w=1600&h=900&fit=crop",
    mediaType: "Image",
    likesCount: 243,
    commentsCount: 38,
    sharesCount: 17,
    isEdited: false,
    isPinned: false,
    createdAt: new Date(Date.now() - 12 * 60_000).toISOString(),
    project: {
      title: "Neon Rift",
      version: "v0.8.2",
      platform: "Windows",
      status: "Ready for testing",
      href: "/projects",
      buildHref: "/projects",
      playtestHref: "/testing-lab",
    },
    commentPreview: [
      {
        id: "comment-bram",
        authorName: "Bram S.",
        authorHandle: "@bram_dev",
        content: "The cornering feels so good now. Nice work!",
        likesCount: 12,
      },
      {
        id: "comment-kayla",
        authorName: "Kayla Jones",
        authorHandle: "@kaylj",
        content: "Tried it with my controller and the weight feels perfect.",
        likesCount: 9,
      },
    ],
  },
];

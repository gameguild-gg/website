type EmbedUrlMatch = { id: string; url: string };

const YOUTUBE_HOSTS = new Set([
  "youtube.com",
  "www.youtube.com",
  "m.youtube.com",
  "music.youtube.com",
  "youtube-nocookie.com",
  "www.youtube-nocookie.com",
  "youtu.be",
]);

const TWITTER_HOSTS = new Set([
  "x.com",
  "www.x.com",
  "twitter.com",
  "www.twitter.com",
  "mobile.twitter.com",
]);

function parseHttpsUrl(text: string): URL | null {
  try {
    const url = new URL(text);
    if (url.protocol !== "https:" || url.username || url.password || url.port) {
      return null;
    }
    return url;
  } catch {
    return null;
  }
}

function pathSegments(pathname: string): string[] {
  const parts = pathname.slice(1).split("/");
  if (parts.at(-1) === "") parts.pop();
  return parts;
}

export function parseYoutubeEmbedUrl(text: string): EmbedUrlMatch | null {
  const url = parseHttpsUrl(text);
  if (url === null || !YOUTUBE_HOSTS.has(url.hostname)) return null;

  const parts = pathSegments(url.pathname);
  let id: string | null = null;
  if (url.hostname === "youtu.be") {
    if (parts.length === 1) id = parts[0] ?? null;
  } else if (parts.length === 1 && parts[0] === "watch") {
    id = url.searchParams.get("v");
  } else if (
    parts.length === 2 &&
    (parts[0] === "embed" || parts[0] === "v")
  ) {
    id = parts[1] ?? null;
  } else if (
    parts.length === 3 &&
    parts[0] === "u" &&
    /^[A-Za-z0-9_]$/.test(parts[1] ?? "")
  ) {
    id = parts[2] ?? null;
  }

  if (id === null || !/^[A-Za-z0-9_-]{11}$/.test(id)) return null;
  return { id, url: url.href };
}

export function parseTwitterEmbedUrl(text: string): EmbedUrlMatch | null {
  const url = parseHttpsUrl(text);
  if (url === null || !TWITTER_HOSTS.has(url.hostname)) return null;

  const parts = url.pathname === "/" && url.hash.startsWith("#!/")
    ? pathSegments(url.hash.slice(2))
    : pathSegments(url.pathname);
  let id: string | null = null;
  if (
    parts.length === 3 &&
    /^[A-Za-z0-9_]+$/.test(parts[0] ?? "") &&
    (parts[1] === "status" || parts[1] === "statuses")
  ) {
    id = parts[2] ?? null;
  } else if (
    parts.length === 4 &&
    parts[0] === "i" &&
    parts[1] === "web" &&
    parts[2] === "status"
  ) {
    id = parts[3] ?? null;
  }

  if (id === null || !/^[0-9]+$/.test(id)) return null;
  return { id, url: url.href };
}

export function parseFigmaEmbedUrl(text: string): EmbedUrlMatch | null {
  const url = parseHttpsUrl(text);
  if (
    url === null ||
    (url.hostname !== "figma.com" && !url.hostname.endsWith(".figma.com"))
  ) {
    return null;
  }

  const parts = pathSegments(url.pathname);
  const id = parts[1];
  if (
    (parts[0] !== "file" && parts[0] !== "proto") ||
    id === undefined ||
    !/^[0-9a-zA-Z]{22,128}$/.test(id)
  ) {
    return null;
  }
  return { id, url: url.href };
}

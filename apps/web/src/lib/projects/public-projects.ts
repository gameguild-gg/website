import { auth, getToken } from "@/auth";
import type { PublicProject } from "@/lib/community/public-community";
import {
  createServerClient,
  GeneratedApi,
  type ProjectsProjectApiOutput,
} from "@game-guild/client";
import { cache } from "react";

const FALLBACK_PROJECT_IMAGE =
  "https://images.unsplash.com/photo-1511512578047-dfb367046420?w=1400&h=900&fit=crop";
const ALLOWED_IMAGE_HOSTS = new Set([
  "cdn.gameguild.gg",
  "i.imgur.com",
  "images.unsplash.com",
  "placehold.co",
  "www.python.org",
]);

function getApiUrl() {
  return (
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:8080"
  ).replace(/\/$/, "");
}

async function createProjectsModule() {
  const session = await auth().catch(() => null);
  const client = createServerClient({
    baseUrl: getApiUrl(),
    auth: { getAccessToken: () => getToken() },
    tenant: { getTenantId: async () => session?.tenantId ?? null },
  });

  return new GeneratedApi.ProjectsModule(client);
}

function parseTags(value?: string | null) {
  if (!value?.trim()) return [];

  try {
    const parsed = JSON.parse(value);
    if (Array.isArray(parsed))
      return parsed.filter(
        (tag): tag is string =>
          typeof tag === "string" && tag.trim().length > 0,
      );
  } catch {
    // Older project records stored comma-separated tags instead of JSON.
  }

  return value
    .split(",")
    .map((tag) => tag.trim())
    .filter(Boolean);
}

function safeProjectImage(value?: string | null) {
  if (!value) return FALLBACK_PROJECT_IMAGE;
  if (value.startsWith("/")) return value;

  try {
    const image = new URL(value);
    return image.protocol === "https:" &&
      ALLOWED_IMAGE_HOSTS.has(image.hostname)
      ? value
      : FALLBACK_PROJECT_IMAGE;
  } catch {
    return FALLBACK_PROJECT_IMAGE;
  }
}

function projectAccent(type?: ProjectsProjectApiOutput["type"]) {
  if (type === "Art" || type === "Music")
    return "from-fuchsia-400/30 via-violet-300/10 to-slate-950";
  if (type === "Tool" || type === "Plugin" || type === "Library")
    return "from-emerald-400/30 via-teal-300/10 to-slate-950";
  if (type === "Educational" || type === "Template")
    return "from-amber-300/30 via-orange-300/10 to-slate-950";
  return "from-sky-400/30 via-cyan-300/10 to-slate-950";
}

function projectCreator(project: ProjectsProjectApiOutput) {
  if (project.creator?.name || project.creator?.username) {
    return project.creator.name || project.creator.username!;
  }

  const owner = project.collaborators?.find(
    (collaborator) =>
      collaborator.isActive !== false &&
      collaborator.role?.toLowerCase() === "owner",
  );
  return owner?.userName || "GameGuild creator";
}

function projectCreatorId(project: ProjectsProjectApiOutput) {
  if (project.creator?.id) return project.creator.id;

  const owner = project.collaborators?.find(
    (collaborator) =>
      collaborator.isActive !== false &&
      collaborator.role?.toLowerCase() === "owner",
  );
  return owner?.userId || project.createdById || undefined;
}

function projectMedia(
  project: ProjectsProjectApiOutput,
): PublicProject["media"] {
  const latestRelease =
    project.releases?.find((release) => release.isLatest) ??
    project.releases?.[0];
  const media = [
    latestRelease?.releaseVersion
      ? { label: "Latest release", detail: latestRelease.releaseVersion }
      : null,
    project.websiteUrl
      ? { label: "Project website", detail: project.websiteUrl }
      : null,
    project.repositoryUrl
      ? { label: "Source repository", detail: project.repositoryUrl }
      : null,
    project.downloadUrl
      ? { label: "Playable build", detail: project.downloadUrl }
      : null,
  ].filter((item): item is { label: string; detail: string } => item !== null);

  return media.length > 0
    ? media
    : [
        {
          label: "Project overview",
          detail:
            project.shortDescription ||
            project.description ||
            "More project details are coming soon.",
        },
      ];
}

function mapProject(project: ProjectsProjectApiOutput): PublicProject {
  const releases = project.releases ?? [];
  const slug = project.slug || project.id || "project";
  return {
    slug,
    title: project.title || slug,
    creatorId: projectCreatorId(project),
    creator: projectCreator(project),
    creatorRole: project.type ? `${project.type} creator` : "Project creator",
    summary:
      project.shortDescription ||
      project.description ||
      "A published GameGuild community project.",
    description:
      project.description ||
      project.shortDescription ||
      "The creator has not added a detailed description yet.",
    status: project.developmentStatus || project.status || "Published",
    tags: parseTags(project.tags),
    coursePath: project.category?.name || "Independent project",
    accent: projectAccent(project.type),
    previewImage: safeProjectImage(
      project.featuredImageUrl || project.imageUrl,
    ),
    buildType: project.type || "Project",
    feedbackGoal:
      project.shortDescription ||
      "Review the current build and share actionable feedback with its creator.",
    feedbackCount: project.feedbackCount ?? 0,
    metrics: [
      { label: "Releases", value: String(releases.length) },
      { label: "Followers", value: String(project.followerCount ?? 0) },
      { label: "Feedback", value: String(project.feedbackCount ?? 0) },
    ],
    media: projectMedia(project),
  };
}

export const getPublishedProjects = cache(
  async (): Promise<PublicProject[]> => {
    const projects = await createProjectsModule();
    const result = await projects.getProjectsForGetProjects({
      status: "Published",
      visibility: "Public",
      skip: 0,
      take: 24,
      sortBy: "UpdatedAt",
      sortDirection: "DESC",
    });

    return result.ok ? result.data.map(mapProject) : [];
  },
);

export const getVisibleProject = cache(
  async (slug: string): Promise<PublicProject | null> => {
    if (!slug.trim()) return null;

    const projects = await createProjectsModule();
    const result = await projects.getProjectsSlug(slug, {
      includeTeam: true,
      includeReleases: true,
      includeCollaborators: true,
    });

    return result.ok ? mapProject(result.data) : null;
  },
);

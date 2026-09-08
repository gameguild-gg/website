import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  createServerClient: vi.fn(),
  getProjectsForGetProjects: vi.fn(),
  getProjectsSlug: vi.fn(),
  getToken: vi.fn(),
}));

vi.mock("@/auth", () => ({
  auth: mocks.auth,
  getToken: mocks.getToken,
}));

vi.mock("@game-guild/client", () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    ProjectsModule: class {
      getProjectsForGetProjects = mocks.getProjectsForGetProjects;
      getProjectsSlug = mocks.getProjectsSlug;
    },
  },
}));

import { getPublishedProjects, getVisibleProject } from "./public-projects";

const apiProject = {
  id: "project-1",
  title: "API Project",
  slug: "api-project",
  shortDescription: "Published through the real API.",
  description: "A public project description.",
  type: "Game",
  developmentStatus: "Beta",
  status: "Published",
  visibility: "Public",
  tags: '["Testing Lab","Public"]',
  imageUrl: "https://cdn.gameguild.gg/projects/api-project/cover.jpg",
  featuredImageUrl: null,
  followerCount: 2,
  feedbackCount: 3,
  createdAt: "2026-08-13T00:00:00.000Z",
  updatedAt: "2026-08-13T00:00:00.000Z",
  releases: [],
  collaborators: [],
  creator: { id: "creator-1", name: "API Creator" },
};

describe("public Projects API queries", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.mockResolvedValue({ tenantId: "tenant-1" });
    mocks.getToken.mockResolvedValue("access-token");
    mocks.createServerClient.mockReturnValue({ kind: "server-client" });
  });

  it("loads only published public projects and maps their API fields", async () => {
    mocks.getProjectsForGetProjects.mockResolvedValue({ ok: true, data: [apiProject] });

    const result = await getPublishedProjects();

    expect(mocks.getProjectsForGetProjects).toHaveBeenCalledWith({
      status: "Published",
      visibility: "Public",
      skip: 0,
      take: 24,
      sortBy: "UpdatedAt",
      sortDirection: "DESC",
    });
    expect(result).toEqual([
      expect.objectContaining({
        slug: "api-project",
        title: "API Project",
        creatorId: "creator-1",
        creator: "API Creator",
        status: "Beta",
        tags: ["Testing Lab", "Public"],
        feedbackCount: 3,
        previewImage: "https://cdn.gameguild.gg/projects/api-project/cover.jpg",
      }),
    ]);
  });

  it("delegates slug visibility to the API and hides inaccessible projects", async () => {
    mocks.getProjectsSlug.mockResolvedValue({
      ok: false,
      error: { name: "ApiError", message: "Not found", status: 404 },
    });

    await expect(getVisibleProject("private-project")).resolves.toBeNull();
    expect(mocks.getProjectsSlug).toHaveBeenCalledWith("private-project", {
      includeTeam: true,
      includeReleases: true,
      includeCollaborators: true,
    });
  });
});

import type { ProjectExportInput, ProjectExportMetadata } from "../../lib/interopAdapter/interop-types"

type ProjectIdentity = Pick<ProjectExportMetadata, "id" | "name" | "tags" | "createdAt" | "updatedAt">

export type GoogleDriveProjectMetadata = ProjectIdentity & Pick<ProjectExportMetadata, "hash" | "size">
export type GoogleDriveProjectContent = ProjectIdentity & Pick<ProjectExportInput, "data">

function isProjectIdentity(value: unknown): value is ProjectIdentity {
  if (typeof value !== "object" || value === null) return false

  const candidate = value as Record<string, unknown>
  return (
    typeof candidate.id === "string" &&
    typeof candidate.name === "string" &&
    Array.isArray(candidate.tags) &&
    candidate.tags.every((tag: unknown) => typeof tag === "string") &&
    typeof candidate.createdAt === "string" &&
    typeof candidate.updatedAt === "string"
  )
}

export function isGoogleDriveProjectMetadata(value: unknown): value is GoogleDriveProjectMetadata {
  return (
    isProjectIdentity(value) &&
    "hash" in value &&
    typeof value.hash === "string" &&
    "size" in value &&
    typeof value.size === "number"
  )
}

export function isGoogleDriveProjectContent(value: unknown): value is GoogleDriveProjectContent {
  return isProjectIdentity(value) && "data" in value && typeof value.data === "string"
}

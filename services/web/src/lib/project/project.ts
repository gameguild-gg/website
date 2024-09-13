export type ProjectIdentifier = string;

export type Project = {
  id: ProjectIdentifier;
  slug: string;
  name: string;
  description: string;
  thumbnailUrl: string;
}
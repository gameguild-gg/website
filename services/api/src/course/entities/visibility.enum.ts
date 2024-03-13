export enum VisibilityEnum {
  DRAFT = 'DRAFT', // not visible to the public
  PUBLISHED = 'PUBLISHED', // published and visible
  FUTURE = 'FUTURE', // scheduled for future publication
  PENDING = 'PENDING', // pending approval
  PRIVATE = 'PRIVATE', // only visible to the author
  TRASH = 'TRASH', // marked for deletion
}
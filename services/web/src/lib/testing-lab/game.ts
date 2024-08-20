export type GameIdentifier = string;

export type Game = {
  id: GameIdentifier;
  slug: string;
  name: string;
  description: string;
  thumbnailUrl: string;
}
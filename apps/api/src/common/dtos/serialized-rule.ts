export interface SerializedRule {
  action: string; // Ex: "update"
  subject: string; // Ex: "Course"
  conditions: any; // Ex: { "creatorId": 1 }
  inverted: boolean; // Se true, é uma negação (cannot)
  reason: string; // Uma mensagem opcional
}

interface RawRule {
  action: string | string[];
  subject?: string | string[];
  /** an array of fields to which user has (or not) access */
  fields?: string[];
  /** an object of conditions which restricts the rule scope */
  conditions?: any;
  /** indicates whether rule allows or forbids something */
  inverted?: boolean;
  /** message which explains why rule is forbidden */
  reason?: string;
}

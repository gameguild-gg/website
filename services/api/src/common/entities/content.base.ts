import { EntityBase } from './entity.base';

export abstract class ContentBase extends EntityBase {
  title: string;
  slug: string;
}

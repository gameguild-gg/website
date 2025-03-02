import { ModelBase } from '@/common/models/model.base';
import { ContentDto } from '@/common/dtos/content.dto';

export abstract class ContentModel extends ModelBase implements ContentDto {
  public readonly slug: string;
}

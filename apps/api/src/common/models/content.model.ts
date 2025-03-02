import { ContentDto } from '@/common/dtos/content.dto';
import { LocalizableResourceModel } from '@/common/models/localizable-resource.model';

export abstract class ContentModel extends LocalizableResourceModel implements ContentDto {
  public readonly slug: string;
}

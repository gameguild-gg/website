import { LocalizableResourceBase } from '@/common/entities/localizable-resource.base';

import { LocalizableResourceDto } from '../dtos/localizable-resource.dto';

export abstract class LocalizableResourceModel extends LocalizableResourceBase implements LocalizableResourceDto {}

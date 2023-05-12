import type { Provider } from '@nestjs/common';
import { Global, Module } from '@nestjs/common';

import { ApiConfigService } from './config.service';
// import { AwsS3Service } from './services/aws-s3.service';
// import { GeneratorService } from './services/generator.service';
// import { TranslationService } from './services/translation.service';
// import { ValidatorService } from './services/validator.service';

// const providers: Provider[] = [
    // ConfigService,
    // ValidatorService,
    // AwsS3Service,
    // GeneratorService,
    // TranslationService,
// ];

@Global()
@Module({
    providers: [ApiConfigService],
    exports: [ApiConfigService],
})
export class SharedModule {}
import { Injectable } from '@nestjs/common';

@Injectable()
export abstract class UploadService {
    abstract upload(): any;
}

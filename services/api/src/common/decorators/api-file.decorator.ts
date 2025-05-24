import { applyDecorators, UnprocessableEntityException, UseInterceptors } from '@nestjs/common';
import { ApiBody, ApiConsumes } from '@nestjs/swagger';
import { FileInterceptor, FilesInterceptor } from '@nestjs/platform-express';
import { MulterOptions } from '@nestjs/platform-express/multer/interfaces/multer-options.interface';
import { SchemaObject } from '@nestjs/swagger/dist/interfaces/open-api-spec.interface';

export class ApiFileOptions {
  maxFileSize?: number = 1024 * 1024 * 2; // 2MB by default
  minFileSize?: number = 1; // 1 byte by default
  destination?: string = '/tmp/uploads'; // '/tmp/uploads' by default
  acceptedMimeTypes?: string[] = []; // empty array by default accepts all
  acceptedFileExtensions?: string[] = []; // empty array by default accepts all
  fieldOptions?: ApiFileFieldOptions | ApiFileFieldOptions[] = {
    maxCount: 1,
    fieldName: 'file',
  };
}

export class ApiFileFieldOptions {
  maxCount?: number = 1; // 1 file by default
  fieldName?: string = 'file'; // 'file' by default
}

export function ApiFile(options: ApiFileOptions) {
  const isMultipleFields = Array.isArray(options.fieldOptions);
  const fieldOptions = isMultipleFields ? (options.fieldOptions as ApiFileFieldOptions[]) : [options.fieldOptions as ApiFileFieldOptions];

  options.destination = options.destination || '/tmp/uploads';
  options.maxFileSize = options.maxFileSize || 1024 * 1024 * 2;
  options.minFileSize = options.minFileSize || 1;
  options.acceptedMimeTypes = options.acceptedMimeTypes || [];
  options.acceptedFileExtensions = options.acceptedFileExtensions || [];

  const multerOptions: MulterOptions = {
    dest: options.destination,
    limits: {
      fileSize: options.maxFileSize,
    },
    fileFilter: (req, file, cb) => {
      const fileExtension = file.originalname.split('.').pop();
      if (options.acceptedFileExtensions.length > 0 && !options.acceptedFileExtensions.includes(fileExtension)) {
        return cb(new UnprocessableEntityException(`File extension not allowed. Accepted extensions: ${options.acceptedFileExtensions.join(', ')}`), false);
      }

      if (options.acceptedMimeTypes.length > 0 && !options.acceptedMimeTypes.includes(file.mimetype)) {
        return cb(new UnprocessableEntityException(`File type not allowed. Accepted types: ${options.acceptedMimeTypes.join(', ')}`), false);
      }

      cb(null, true);
    },
  };

  const decorators = [
    ApiConsumes('multipart/form-data'),
    ApiBody({
      schema: {
        type: 'object',
        properties: fieldOptions.reduce<Record<string, SchemaObject>>((acc, field) => {
          const isArray = (field.maxCount || 1) > 1;
          acc[field.fieldName || 'file'] = isArray
            ? {
                type: 'array',
                items: {
                  type: 'string',
                  format: 'binary',
                },
              }
            : {
                type: 'string',
                format: 'binary',
              };
          return acc;
        }, {}),
      },
    }),
  ];

  if (isMultipleFields) {
    decorators.push(UseInterceptors(...fieldOptions.map((field) => FilesInterceptor(field.fieldName || 'file', field.maxCount, multerOptions))));
  } else {
    const singleField = fieldOptions[0];
    decorators.push(UseInterceptors(FileInterceptor(singleField.fieldName || 'file', multerOptions)));
  }

  return applyDecorators(...decorators);
}

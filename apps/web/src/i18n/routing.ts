import { defineRouting } from 'next-intl/routing';

export const routing = defineRouting({
  locales: ['en-US', 'pt-BR'],
  // The default language is canonical without a URL prefix. The proxy owns
  // the internal rewrite so Next 16 never reprocesses next-intl's rewrite.
  localePrefix: 'as-needed',
  defaultLocale: 'en-US',
});

import {notFound} from 'next/navigation';
import {getRequestConfig} from 'next-intl/server';
import {locales} from '@/data/locales';

export default getRequestConfig(async ({locale}) => {
  // Validate that the incoming `locale` parameter is valid.
  if (!locales.includes(locale as string)) notFound();

  return {
    messages: (await import(`../messages/${locale}.json`)).default,
  };
});

export type ParamsWithLocale<P = unknown> = P & { locale: string };

export type ParamsWithSlug<P = unknown> = P & { slug: string };

export type PropsWithSlugParams<P = unknown> = P & { params: ParamsWithSlug };

export type PropsWithLocaleParams<P = unknown> = P & {
  params: ParamsWithLocale;
};

export type PropsWithLocaleSlugParams<P = unknown> = P & {
  params: P & { locale: string; slug: string };
};

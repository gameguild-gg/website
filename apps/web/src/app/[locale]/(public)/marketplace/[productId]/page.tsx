import { cn } from "@game-guild/ui/lib/utils";
import { AddToCartForm } from '@/components/marketplace/add-to-cart-form';
import { Link } from '@/i18n/navigation';
import { getMarketplaceProduct } from '@/lib/marketplace/queries';
import { Badge } from '@game-guild/ui/components/badge';
import { buttonVariants } from '@game-guild/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';

// Dynamic metadata for the storefront product page; shares the request-scoped product
// lookup with the page body (getMarketplaceProduct is React-cache'd).
export async function generateMetadata({ params }: { params: Promise<{ productId: string }> }): Promise<Metadata> {
  const { productId } = await params;
  const product = await getMarketplaceProduct(productId);

  if (!product || product.type === 'Physical' || product.type === 'Subscription') {
    return {
      title: 'Product Not Found',
      description: 'The requested marketplace product could not be found.',
      robots: { index: false, follow: false },
    };
  }

  const name = product.name ?? 'Marketplace product';
  const description = product.shortDescription ?? product.description ?? `View ${name} on the GameGuild Marketplace.`;

  return {
    title: name,
    description,
    openGraph: {
      title: name,
      description,
      ...(product.imageUrl ? { images: [product.imageUrl] } : {}),
    },
  };
}

export default async function MarketplaceProductPage({ params }: { params: Promise<{ productId: string }> }) {
  const [{ productId }, t] = await Promise.all([params, getTranslations('marketplace')]);
  const product = await getMarketplaceProduct(productId);
  if (!product || product.type === 'Physical' || product.type === 'Subscription') notFound();
  const price = product.pricing?.find((item) => item.isDefault) ?? product.pricing?.[0];

  return (
    <main className="mx-auto grid w-full max-w-6xl gap-8 px-4 py-10 sm:px-6 lg:grid-cols-[1fr_22rem] lg:px-8">
      <section className="space-y-5">
        <Badge variant="outline">{product.type}</Badge>
        <h1 className="text-4xl font-semibold tracking-tight">{product.name}</h1>
        <p className="max-w-3xl leading-7 text-muted-foreground">{product.description ?? product.shortDescription}</p>
      </section>
      <Card>
        <CardHeader><CardTitle>{price ? `${price.currentPrice} ${price.currency}` : t('priceUnavailable')}</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <AddToCartForm product={product} labels={{ add: t('addToCart'), quantity: t('quantity'), unavailable: t('priceUnavailable') }} />
          <Link
            href="/marketplace/cart"
            className={cn(buttonVariants({ variant: "outline", className: "w-full" }))}
          >
            {t("viewCart")}
          </Link>
        </CardContent>
      </Card>
    </main>
  );
}

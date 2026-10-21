import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getMarketplaceProductMock } = vi.hoisted(() => ({
  getMarketplaceProductMock: vi.fn(),
}));

vi.mock('@/lib/marketplace/queries', () => ({
  getMarketplaceProduct: getMarketplaceProductMock,
}));
vi.mock('@/components/marketplace/add-to-cart-form', () => ({
  AddToCartForm: () => null,
}));

import { generateMetadata } from './page';

describe('marketplace product page metadata', () => {
  beforeEach(() => {
    getMarketplaceProductMock.mockReset();
  });

  it('derives title and description from the fetched product', async () => {
    getMarketplaceProductMock.mockResolvedValue({
      type: 'Digital',
      name: 'Pixel Art Pack',
      shortDescription: 'A bundle of pixel art assets.',
      description: 'The long description.',
      imageUrl: 'https://example.test/pack.png',
    });

    const metadata = await generateMetadata({ params: Promise.resolve({ productId: 'pack-1' }) });

    expect(getMarketplaceProductMock).toHaveBeenCalledWith('pack-1');
    expect(metadata).toMatchObject({
      title: 'Pixel Art Pack',
      description: 'A bundle of pixel art assets.',
      openGraph: {
        title: 'Pixel Art Pack',
        description: 'A bundle of pixel art assets.',
        images: ['https://example.test/pack.png'],
      },
    });
  });

  it('falls back to noindex metadata for missing or non-digital products', async () => {
    getMarketplaceProductMock.mockResolvedValue(null);

    const metadata = await generateMetadata({ params: Promise.resolve({ productId: 'missing' }) });

    expect(metadata).toMatchObject({
      title: 'Product Not Found',
      robots: { index: false, follow: false },
    });
  });
});

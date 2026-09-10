import { SocialShell } from '@/components/feed/social-shell';
import { isSocialFeedTab } from '@/components/feed/social-feed-tabs';

export default async function Page({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}): Promise<React.JSX.Element> {
  const query = await searchParams;
  const rawTab = typeof query?.tab === 'string' ? query.tab : undefined;
  const tag = typeof query?.tag === 'string' ? query.tag : null;
  return <SocialShell tab={isSocialFeedTab(rawTab) ? rawTab : 'foryou'} tag={tag} />;
}

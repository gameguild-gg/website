'use client';

import { Link, usePathname } from '@/i18n/navigation';
import { cn } from '@/lib/utils';
import { Accessibility, Globe2, Laptop, Palette, ShieldCheck, User, UserCog } from 'lucide-react';
import { useTranslations } from 'next-intl';

const NAV_ITEMS = [
  { href: '/workspace/settings/profile', icon: User, labelKey: 'nav.profile' },
  { href: '/workspace/settings/account', icon: UserCog, labelKey: 'nav.account' },
  { href: '/workspace/settings/security', icon: Laptop, labelKey: 'nav.security' },
  { href: '/workspace/settings/appearance', icon: Palette, labelKey: 'nav.appearance' },
  { href: '/workspace/settings/localization', icon: Globe2, labelKey: 'nav.localization' },
  { href: '/workspace/settings/privacy', icon: ShieldCheck, labelKey: 'nav.privacy' },
  { href: '/workspace/settings/accessibility', icon: Accessibility, labelKey: 'nav.accessibility' },
] as const;

export function SettingsNav() {
  const pathname = usePathname();
  const t = useTranslations('settings');

  return (
    <nav aria-label={t('navLabel')}>
      <ul className="flex gap-1 overflow-x-auto pb-1 lg:flex-col lg:overflow-visible lg:pb-0">
        {NAV_ITEMS.map(({ href, icon: Icon, labelKey }) => {
          const isActive = pathname === href || pathname?.startsWith(`${href}/`);

          return (
            <li key={href} className="shrink-0">
              <Link
                href={href}
                aria-current={isActive ? 'page' : undefined}
                className={cn(
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors',
                  isActive
                    ? 'bg-muted font-medium text-foreground'
                    : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground',
                )}
              >
                <Icon className="size-4 shrink-0" />
                {t(labelKey)}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

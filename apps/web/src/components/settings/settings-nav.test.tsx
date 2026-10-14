import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/i18n/navigation', () => ({
  usePathname: () => '/workspace/settings/accessibility',
  Link: ({ children, href, ...props }: { readonly children: ReactNode; readonly href: string }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) =>
    ({
      navLabel: 'Settings sections',
      'nav.profile': 'Profile',
      'nav.account': 'Account',
      'nav.security': 'Security',
      'nav.appearance': 'Appearance',
      'nav.localization': 'Language & region',
      'nav.privacy': 'Privacy',
      'nav.accessibility': 'Accessibility',
    })[key] ?? key,
}));

import { SettingsNav } from './settings-nav';

describe('SettingsNav', () => {
  it('exposes each hub section and marks the active section', () => {
    render(<SettingsNav />);

    expect(screen.getByRole('navigation', { name: 'Settings sections' })).toBeInTheDocument();
    expect(screen.getAllByRole('link')).toHaveLength(7);
    expect(screen.getByRole('link', { name: 'Accessibility' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('link', { name: 'Profile' })).toHaveAttribute(
      'href',
      '/workspace/settings/profile',
    );
  });

  it('links to the security section', () => {
    render(<SettingsNav />);

    expect(screen.getByRole('link', { name: 'Security' })).toHaveAttribute(
      'href',
      '/workspace/settings/security',
    );
  });
});

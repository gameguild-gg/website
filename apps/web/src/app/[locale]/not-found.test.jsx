import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href }) => <a href={href}>{children}</a>,
}));

import NotFound from './not-found';

describe('NotFound', () => {
  it('explains the missing page with one accessible page heading', () => {
    render(<NotFound />);

    expect(screen.getByRole('heading', { level: 1, name: "This page isn't on the map." })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Suggested destinations' })).toBeInTheDocument();
  });

  it('offers working home and course destinations', () => {
    render(<NotFound />);

    expect(screen.getByRole('link', { name: 'Go to home' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('link', { name: 'Explore courses' })).toHaveAttribute('href', '/courses');
  });
});

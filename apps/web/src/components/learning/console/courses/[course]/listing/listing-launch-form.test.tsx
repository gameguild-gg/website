import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { ListingLaunchForm } from './listing-launch-form';
import { updateCourse } from '@/lib/learning/actions';

const refreshMock = vi.fn();

Object.defineProperties(HTMLElement.prototype, {
  hasPointerCapture: { value: vi.fn(() => false) },
  setPointerCapture: { value: vi.fn() },
  releasePointerCapture: { value: vi.fn() },
  scrollIntoView: { value: vi.fn() },
});

beforeAll(() => {
  global.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});

vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning',
  useRouter: () => ({
    refresh: refreshMock,
  }),
}));

vi.mock('@/lib/learning/actions', () => ({
  updateCourse: vi.fn(),
}));

const course = {
  id: 'course-1',
  title: 'Advanced AI',
  description: 'Advanced AI course',
  slug: 'advanced-ai',
  status: 'Published',
  visibility: 'private',
  enrollmentStatus: 'Closed',
  enrollmentDeadline: '2026-08-15T13:30:00.000Z',
  maxEnrollments: 25,
} as never;

describe('ListingLaunchForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(updateCourse).mockResolvedValue({ success: true, data: null });
  });

  it('updates catalog visibility, enrollment status, deadline, and unlimited seats', async () => {
    const user = userEvent.setup();

    render(<ListingLaunchForm course={course} />);

    await user.click(screen.getByRole('combobox', { name: /catalog visibility/i }));
    await user.click(await screen.findByRole('option', { name: /public/i }));

    await user.click(screen.getByRole('combobox', { name: /enrollment status/i }));
    await user.click(await screen.findByRole('option', { name: /open/i }));

    fireEvent.change(screen.getByLabelText(/enrollment deadline/i), { target: { value: '2026-09-01T09:00' } });
    fireEvent.change(screen.getByLabelText(/enrollment cap/i), { target: { value: '0' } });

    await user.click(screen.getByRole('button', { name: /save launch controls/i }));

    await waitFor(() => {
      expect(updateCourse).toHaveBeenCalledWith({
        courseId: 'course-1',
        visibility: 'Public',
        enrollmentStatus: 'Open',
        enrollmentDeadline: new Date('2026-09-01T09:00').toISOString(),
        maxEnrollments: null,
      });
    });
    expect(screen.getByText('Listing controls updated successfully.')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /save launch controls/i })).toBeEnabled();
    });
    expect(refreshMock).not.toHaveBeenCalled();
  }, 15_000);

  it('renders API errors without clearing the launch form', async () => {
    const user = userEvent.setup();
    vi.mocked(updateCourse).mockResolvedValueOnce({ success: false, error: 'Enrollment deadline is invalid.' });

    render(<ListingLaunchForm course={course} />);

    await user.click(screen.getByRole('button', { name: /save launch controls/i }));

    expect(await screen.findByText('Enrollment deadline is invalid.')).toBeInTheDocument();
    expect(refreshMock).not.toHaveBeenCalled();
  });
});

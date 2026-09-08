import '@testing-library/jest-dom/vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { PricingEditorForm } from './pricing-editor-form';

const updateCoursePricingMock = vi.fn();

vi.mock('@/lib/learning/actions', () => ({
  updateCoursePricing: (...args: unknown[]) => updateCoursePricingMock(...args),
}));

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

describe('PricingEditorForm', () => {
  beforeEach(() => {
    updateCoursePricingMock.mockReset();
    updateCoursePricingMock.mockResolvedValue({ success: true, data: null });
  });

  it('submits enabled pricing to the course pricing action', async () => {
    const user = userEvent.setup();

    render(
      <PricingEditorForm
        courseId="course-1"
        pricing={{
          tiers: [],
          discounts: [],
          refundPolicy: 'Free courses do not collect payment.',
          hasFreeTrial: false,
        }}
      />,
    );

    await user.click(screen.getByRole('switch', { name: /enable monetization/i }));
    await user.clear(screen.getByLabelText(/price/i));
    await user.type(screen.getByLabelText(/price/i), '149.99');
    await user.clear(screen.getByLabelText(/currency/i));
    await user.type(screen.getByLabelText(/currency/i), 'eur');

    await user.click(screen.getByRole('button', { name: /save pricing/i }));

    await waitFor(() => {
      expect(updateCoursePricingMock).toHaveBeenCalledWith({
        courseId: 'course-1',
        isMonetizationEnabled: true,
        price: 149.99,
        currency: 'EUR',
        isSubscription: false,
        subscriptionDurationDays: null,
      });
    });
    expect(screen.getByText('Pricing updated successfully.')).toBeInTheDocument();
  });

  it('submits existing monthly pricing, normalizes bad price input, and falls back to USD', async () => {
    const user = userEvent.setup();

    render(
      <PricingEditorForm
        courseId="course-1"
        pricing={{
          tiers: [
            {
              id: 'tier-1',
              courseId: 'course-1',
              name: 'Studio pass',
              price: 49,
              currency: 'brl',
              interval: 'monthly',
              active: true,
              order: 1,
              createdAt: '2026-01-01T00:00:00.000Z',
              updatedAt: '2026-01-02T00:00:00.000Z',
            },
          ],
          discounts: [],
          refundPolicy: 'Monthly refunds reviewed manually.',
          hasFreeTrial: true,
        }}
      />,
    );

    await user.clear(screen.getByLabelText(/price/i));
    await user.type(screen.getByLabelText(/price/i), 'not-a-price');
    await user.clear(screen.getByLabelText(/currency/i));

    await user.click(screen.getByRole('button', { name: /save pricing/i }));

    await waitFor(() => {
      expect(updateCoursePricingMock).toHaveBeenCalledWith({
        courseId: 'course-1',
        isMonetizationEnabled: true,
        price: 0,
        currency: 'USD',
        isSubscription: true,
        subscriptionDurationDays: 30,
      });
    });
  });

  it('supports yearly subscriptions and surfaces pricing API errors', async () => {
    const user = userEvent.setup();
    updateCoursePricingMock.mockResolvedValueOnce({ success: false, error: 'Pricing provider rejected the tier.' });

    render(
      <PricingEditorForm
        courseId="course-1"
        pricing={{
          tiers: [],
          discounts: [],
          refundPolicy: 'Paid access.',
          hasFreeTrial: false,
        }}
      />,
    );

    await user.click(screen.getByRole('switch', { name: /enable monetization/i }));
    await user.click(screen.getByRole('combobox'));
    await user.click(await screen.findByRole('option', { name: /yearly/i }));
    await user.click(screen.getByRole('button', { name: /save pricing/i }));

    await waitFor(() => {
      expect(updateCoursePricingMock).toHaveBeenCalledWith(expect.objectContaining({
        isSubscription: true,
        subscriptionDurationDays: 365,
      }));
    });
    expect(screen.getByText('Pricing provider rejected the tier.')).toBeInTheDocument();
  });
});

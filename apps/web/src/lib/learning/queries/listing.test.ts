import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CourseViewModel } from '@/lib/learning/view-models';
import { getCourse } from './course';
import {
  getCourseFaq,
  getCourseLandingProjects,
  getCoursePricing,
  getCourseTestimonials,
} from './listing';

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  getToken: vi.fn(),
  resolveCourseId: vi.fn(),
  getApiSocialCoursesReviews: vi.fn(),
  getCoursesPricing: vi.fn(),
}));

vi.mock('@/auth', () => ({ getToken: mocks.getToken }));

vi.mock('@game-guild/client', () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningCoursesProgramModule: class {
      getCoursesPricing = mocks.getCoursesPricing;
    },
    LearningExperienceSocialReviewsModule: class {
      getApiSocialCoursesReviews = mocks.getApiSocialCoursesReviews;
    },
  },
}));

vi.mock('./course', () => ({
  getCourse: vi.fn(),
  resolveCourseId: mocks.resolveCourseId,
}));

const baseCourse: CourseViewModel = {
  id: 'course-1',
  creatorId: 'creator-1',
  creatorHandle: 'instructor-one',
  title: 'Dashboard Editable Course',
  description: 'Course description',
  metadata: null,
  slug: 'dashboard-editable-course',
  status: 'draft',
  visibility: 'private',
  thumbnail: null,
  videoShowcaseUrl: null,
  estimatedHours: 12,
  category: 'GameDevelopment',
  difficulty: 'Intermediate',
  skillsRequired: null,
  skillsProvided: null,
  enrollmentStatus: 'Open',
  maxEnrollments: null,
  enrollmentDeadline: null,
  currentEnrollments: 0,
  averageRating: 0,
  totalRatings: 0,
  isEnrollmentOpen: true,
  deliveryMode: 'on-demand',
  pricingModel: 'free',
  features: {
    hasClasses: true,
    hasRecordings: true,
    hasSchedule: true,
    hasOnDemandContent: true,
    hasPricing: false,
    hasCertificate: true,
    hasAssessments: true,
    hasDiscussions: true,
  },
  createdAt: '2026-01-01T00:00:00.000Z',
  updatedAt: '2026-01-02T00:00:00.000Z',
};

describe('course listing queries', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.createServerClient.mockReturnValue({});
    mocks.getToken.mockResolvedValue('token');
    mocks.resolveCourseId.mockResolvedValue('course-1');
    vi.mocked(getCourse).mockReset();
  });

  it('uses dashboard-edited FAQ stored in course metadata', async () => {
    vi.mocked(getCourse).mockResolvedValue({
      ...baseCourse,
      metadata: JSON.stringify({
        landingFaq: [
          { question: 'Can I edit this from the dashboard?', answer: 'Yes, this FAQ is metadata-backed.' },
          { question: 'Will the storefront use it?', answer: 'Yes, edited metadata wins over generated defaults.' },
        ],
      }),
    } as CourseViewModel & { metadata: string });

    const faq = await getCourseFaq('course-1');

    expect(faq.total).toBe(2);
    expect(faq.items).toEqual([
      expect.objectContaining({
        id: 'course-1-faq-1',
        question: 'Can I edit this from the dashboard?',
        answer: 'Yes, this FAQ is metadata-backed.',
        order: 1,
      }),
      expect.objectContaining({
        id: 'course-1-faq-2',
        question: 'Will the storefront use it?',
        answer: 'Yes, edited metadata wins over generated defaults.',
        order: 2,
      }),
    ]);
  });

  it('uses dashboard-edited project carousel items stored in course metadata', async () => {
    vi.mocked(getCourse).mockResolvedValue({
      ...baseCourse,
      metadata: JSON.stringify({
        landingProjects: [
          {
            title: 'Boss behavior sandbox',
            summary: 'Students build a readable boss encounter with inspectable AI states.',
            image: 'https://example.com/boss-sandbox.jpg',
            skills: ['State debugging', 'Combat pacing'],
            deliverable: 'A playable boss encounter with annotated decision logic.',
            moduleLabel: 'Project A',
          },
        ],
      }),
    } as CourseViewModel & { metadata: string });

    const projects = await getCourseLandingProjects('course-1');

    expect(projects.total).toBe(1);
    expect(projects.items).toEqual([
      expect.objectContaining({
        id: 'course-1-project-1',
        title: 'Boss behavior sandbox',
        summary: 'Students build a readable boss encounter with inspectable AI states.',
        image: 'https://example.com/boss-sandbox.jpg',
        skills: ['State debugging', 'Combat pacing'],
        deliverable: 'A playable boss encounter with annotated decision logic.',
        moduleLabel: 'Project A',
        order: 1,
      }),
    ]);
  });

  it('does not invent project carousel items when metadata has not been configured', async () => {
    vi.mocked(getCourse).mockResolvedValue(baseCourse);

    const projects = await getCourseLandingProjects('course-1');

    expect(projects).toEqual({ items: [], total: 0 });
  });
  it('loads testimonials through the generated reviews module', async () => {
    mocks.getApiSocialCoursesReviews.mockResolvedValue({
      ok: true,
      data: [{
        id: 'review-1', courseId: 'course-1', userId: 'user-1', rating: 5,
        title: 'Clear and practical', content: 'Great course.', isApproved: true,
        isVerifiedPurchase: true, helpfulCount: 3, createdAt: '2026-01-02T00:00:00.000Z',
      }],
    });

    const result = await getCourseTestimonials('course-slug');

    expect(mocks.resolveCourseId).toHaveBeenCalledWith('course-slug');
    expect(mocks.getApiSocialCoursesReviews).toHaveBeenCalledWith('course-1', {
      skip: 0, take: 100, approvedOnly: false,
    });
    expect(result).toMatchObject({
      total: 1, averageRating: 5,
      testimonials: [expect.objectContaining({ id: 'review-1', studentId: 'user-1' })],
    });
  });

  it('loads monetized pricing through the generated course module', async () => {
    mocks.getCoursesPricing.mockResolvedValue({
      ok: true,
      data: {
        isMonetizationEnabled: true, isSubscription: true,
        subscriptionDurationDays: 365, price: 129, currency: 'USD',
      },
    });

    const pricing = await getCoursePricing('course-slug');

    expect(mocks.getCoursesPricing).toHaveBeenCalledWith('course-1');
    expect(pricing.tiers).toEqual([
      expect.objectContaining({ price: 129, currency: 'USD', interval: 'yearly' }),
    ]);
  });

});

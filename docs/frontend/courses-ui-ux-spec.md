# Courses UI/UX specification (public discovery and learner entry)

Status: accepted — this is the design contract requested in #24 ("define which data and
functionality the UI must serve") and referenced by #22 ("Implementation of the Courses
pages"). It describes the learner-facing course surfaces that exist today and the behavior
they must keep satisfying; changes to these pages must preserve this contract.

## 1. Scope and page inventory

| Surface | Route | Purpose |
|---|---|---|
| Course catalog | `/courses` | Public discovery of published courses: hero stats, highlight carousel, filterable catalog. |
| Course detail (landing) | `/courses/[course]` | Full landing page for one course: hero, curriculum journey, outcomes, enrollment/checkout actions. |
| Course activities (public summary) | `/courses/[course]/activities` | Public list of the course's activities. |
| Course grades (public summary) | `/courses/[course]/grades` | Public grading shape for the course. |
| My courses | `/learn/courses` | Learner's enrolled courses with resume links. |
| Learn course home | `/learn/courses/[slug]` | Enrolled learner experience: content, lessons, activities, grades, community, groups. |

Authoring/console surfaces (`/console/learning/courses/...`, `/workspace/learning/courses/...`)
are out of scope for this specification.

## 2. Data contract

### 2.1 Catalog and cards (`/courses`, `/courses/[course]`)

Source: `Learning.Courses` public projections via
`apps/web/src/lib/courses/services/course.service.ts` (`getPublicCourseCatalog`,
`getCourseBySlug`). The UI consumes exactly these fields of the mapped `Program`:

| Field | Type | Used for | Required |
|---|---|---|---|
| `id` | string | Stable card key, access checks | yes |
| `slug` | string | Detail route `/courses/[course]` | yes |
| `title` | string | Card/hero heading (fallback: "Untitled course") | yes |
| `description` | string | Card outcome copy | no |
| `thumbnail` | string (URL) | Card/hero image; gradient placeholder when absent | no |
| `category` | string/number | Discipline badge + discipline filter (`getCourseCategoryName`) | no |
| `difficulty` | string/number | Level badge (`getCourseLevelConfig`) | no |
| `estimatedHours` | number | Duration badge, hero "Study time" stat | no |
| `isEnrollmentOpen` | boolean | Hero "Open enrollment" stat | no |

Static enrichment (no API dependency): program packages (`PUBLIC_PROGRAM_PACKAGES`:
slug, shortTitle, image) and per-course showcase copy (`SHOWCASE_BY_SLUG`) resolve by
course slug; unmatched courses fall back to category/description copy.

### 2.2 Course detail additionally

- Viewer access state from `getCourseViewerAccess` (`signed-out` | viewer with access |
  viewer without access) drives enroll vs. continue actions.
- Purchasable products containing the course (`getProductsContainingCourse`) drive the
  checkout button.
- Failure reasons are distinct: `not-found` renders the 404 page; `unavailable` renders
  the explicit "temporarily unavailable" state.

### 2.3 My courses (`/learn/courses`)

`CourseAttendanceData[]` from `getMyLearningCourses()`: enrolled course summaries with
per-course resume targets. Signed-out visitors see the standard empty state, not an error.

## 3. Functionality

1. **Search**: free text over title, description, slug, discipline, program title and
   showcase headline. Debounce-free client filter; matches are live.
2. **Package filter**: one of `PUBLIC_PROGRAM_PACKAGES` slugs or `all`. Deep-linkable via
   `?program=<slug>`.
3. **Discipline filter**: derived from the loaded catalog's categories or `all`.
   Deep-linkable via `?category=<name>`.
4. **Combined filtering**: search + package + discipline intersect (AND).
5. **Reset**: whenever any filter or search is active, a "Reset filters" action restores
   `all/all/""`; the no-match empty state repeats this action inline.
6. **Counts**: result count is announced (`aria-live="polite"`), singular/plural aware.
7. **Navigation loop**: catalog card → course detail → enroll/self-enroll/checkout →
   `/learn/courses/...`. The catalog links back to `/learn/courses` ("My enrolled
   courses") and the hero CTA "Continue learning" points there for returning learners.

## 4. State matrix (catalog + detail)

| State | Trigger | Presentation |
|---|---|---|
| Success, courses present | API ok, N > 0 | Stats, carousel, filterable catalog. |
| Success, empty catalog | API ok, N = 0 | "No courses are published yet." — distinct from no-match. |
| Filtered to zero | filters/search exclude all | "No courses matched this view." + inline Clear filters action. |
| Catalog API failure | `success: false` | Full-page "course catalog is temporarily unavailable" state with error detail, Try again, Go to my courses. Never rendered as an empty catalog. |
| Course not found | `reason: 'not-found'` | 404 page. |
| Course API failure | `reason: 'unavailable'` | "This course is temporarily unavailable" state with error detail. |
| Loading (detail) | Suspense | Spinner + "Loading course...". |

Stats honesty: hero stats render only on catalog success. "Open enrollment" counts
courses with `isEnrollmentOpen`, never seats.

## 5. Accessibility requirements

- Filter chips expose `aria-pressed`; each filter row is a `role="group"` with
  `aria-label` ("Filter courses by package" / "Filter courses by discipline").
- Search input has an explicit `aria-label` (icon is `aria-hidden`).
- Result count changes are announced via `aria-live="polite"`.
- Stat cards use description lists with `sr-only` terms.
- Empty and error states are text-first (no icon-only communication).

## 6. Visual language

Dark storefront theme shared with the rest of the public site: background `#070a12`,
surfaces `bg-white/[0.04]` with `border-white/10`, rounded-2xl cards (rounded-3xl stat
tiles), slate-300/400 body ramp, white primary CTAs, lucide icons at `size-4` in
inline actions. Cards use a 160px media column on md+ with hover lift
(`hover:-translate-y-0.5`, `hover:border-white/20`).

## 7. Acceptance checklist

- [x] Catalog renders published courses with search, package filter, discipline filter.
- [x] Catalog distinguishes empty-published, no-match, and API-failure states.
- [x] Catalog API failure renders an explicit unavailable state, never an empty catalog.
- [x] Course detail handles found / not-found / unavailable with distinct outcomes.
- [x] Filters are keyboard operable and announce state (aria-pressed) and results (aria-live).
- [x] Reset/clear actions restore the unfiltered catalog.
- [x] Learner loop links (`Continue learning`, `My enrolled courses`) resolve to `/learn/courses`.
- [x] Vitest coverage for the catalog page states and catalog component interactions.

import type { ContentItem } from '@/lib/learning/types';

interface CourseContentIdentity {
  title: string;
  description: string;
  createdAt: string;
  updatedAt: string;
}

export interface ContentTreeModel {
  hasModules: boolean;
  modules: ContentItem[];
  treeItems: ContentItem[];
  virtualModuleIds: string[];
}

export function buildContentTreeModel(
  courseId: string,
  items: ContentItem[],
  course: CourseContentIdentity,
): ContentTreeModel {
  const topLevelItems = items.filter((item) => !item.parentId).sort((left, right) => left.order - right.order);
  const hasModules = items.some((item) => item.type === 'Module' || Boolean(item.parentId));

  if (!hasModules) {
    const legacyFlatModuleId = `${courseId}-content`;
    const compatibilityModule: ContentItem = {
      id: legacyFlatModuleId,
      version: 0,
      slug: legacyFlatModuleId,
      parentId: null,
      order: 0,
      type: 'Module',
      title: 'Course Content',
      description: course.description || 'Imported course content',
      status: 'published',
      visibility: 'Public',
      duration: null,
      estimatedMinutesSource: null,
      metadata: {},
      gradingConfig: null,
      createdAt: course.createdAt,
      updatedAt: course.updatedAt,
    };

    return {
      hasModules: false,
      modules: [compatibilityModule],
      treeItems: topLevelItems.map((item) => ({ ...item, parentId: legacyFlatModuleId })),
      virtualModuleIds: [legacyFlatModuleId],
    };
  }

  const realTopModules = topLevelItems.filter((item) => item.type === 'Module');
  const orphanTopLevel = topLevelItems.filter((item) => item.type !== 'Module');

  // Pure-module course, or legacy top-level lesson containers with children but
  // no coexisting real modules: keep rendering top-level items as modules.
  if (orphanTopLevel.length === 0 || realTopModules.length === 0) {
    return {
      hasModules: true,
      modules: orphanTopLevel.length === 0 ? realTopModules : topLevelItems,
      treeItems: items,
      virtualModuleIds: [],
    };
  }

  // Real modules coexist with top-level non-module orphans: reparent the orphans
  // under a virtual "Unassigned" module so they render as lessons, not empty
  // module cards.
  const virtualModuleId = `${courseId}-unassigned`;
  const virtualModule: ContentItem = {
    id: virtualModuleId,
    version: 0,
    slug: virtualModuleId,
    parentId: null,
    order: realTopModules.length,
    type: 'Module',
    title: 'Unassigned',
    description: 'Content not yet organized into a module.',
    status: 'published',
    visibility: 'Public',
    duration: null,
    estimatedMinutesSource: null,
    metadata: {},
    gradingConfig: null,
    createdAt: course.createdAt,
    updatedAt: course.updatedAt,
  };
  const orphanIds = new Set(orphanTopLevel.map((orphan) => orphan.id));

  return {
    hasModules: true,
    modules: [...realTopModules, virtualModule],
    treeItems: items.map((item) =>
      orphanIds.has(item.id) ? { ...item, parentId: virtualModuleId } : item,
    ),
    virtualModuleIds: [virtualModuleId],
  };
}

/**
 * Course Editor Module - Index
 *
 * Central export file for the Course Editor system providing:
 * - Type definitions for course editing
 * - React context provider and hooks
 * - State management with reducer
 * - Server actions for data operations
 * - Utility functions and helpers
 */

// Internal imports for utility functions
import type { Course as CourseType } from '@/lib/courses';
import { autoSaveCourse, createCourse, getCourseBySlug, publishCourse, saveCourse } from './actions';
import { CourseEditorProvider, useCourseEditor } from './context/course-editor-provider';
import { courseEditorReducer, createEmptyCourse, createInitialCourseEditorState, validateCourse } from './context/course-editor-reducer';
import type { CourseChapter as CourseChapterType, CourseEditorAction, CourseEditorConfig, CourseEditorContextValue, CourseEditorState, CourseLesson as CourseLessonType, CourseValidationError as CourseValidationErrorType } from './types';

// Types
export type {
  CourseChapter, CourseEditorAction,
  CourseEditorConfig, CourseEditorContextValue, CourseEditorHistoryEntry, CourseEditorMode, CourseEditorState, CourseEditorTab, CourseLesson,
  CoursePreviewMode,
  CourseValidationError,
  EnhancedCourseContent
} from './types';

export { CourseEditorActionType, courseEditorValidationRules, defaultCourseEditorState } from './types';

// Context Provider and Hooks
export { CourseEditorProvider, useCourseEditor } from './context/course-editor-provider';

// Reducer and Utilities
export { courseEditorReducer, createEmptyCourse, createInitialCourseEditorState, validateCourse } from './context/course-editor-reducer';

// Server Actions
export { autoSaveCourse, createCourse, getCourseBySlug, publishCourse, saveCourse } from './actions';

// Enhanced Course Types (re-export for convenience)
export type { Course } from '@/lib/courses';

/**
 * Course Editor Module Configuration
 */
export const COURSE_EDITOR_CONFIG = {
  // Default configuration values
  DEFAULT_AUTO_SAVE_INTERVAL: 30000, // 30 seconds
  DEFAULT_MAX_HISTORY_STEPS: 50,

  // Validation limits
  MIN_TITLE_LENGTH: 3,
  MAX_TITLE_LENGTH: 100,
  MIN_DESCRIPTION_LENGTH: 10,
  MAX_DESCRIPTION_LENGTH: 500,

  // Editor tabs
  EDITOR_TABS: ['overview', 'content', 'chapters', 'settings', 'preview'] as const,

  // Preview modes
  PREVIEW_MODES: ['mobile', 'tablet', 'desktop'] as const,

  // Editor modes
  EDITOR_MODES: ['create', 'edit', 'preview'] as const,
} as const;

/**
 * Course Editor Utility Functions
 */
export const CourseEditorUtils = {
  /**
   * Calculate total course duration from chapters
   */
  calculateTotalDuration: (chapters: CourseChapterType[]): number => {
    return chapters.reduce((total, chapter) => {
      return (
        total +
        chapter.lessons.reduce((chapterTotal, lesson) => {
          return chapterTotal + (lesson.duration || 0);
        }, 0)
      );
    }, 0);
  },

  /**
   * Calculate total number of lessons
   */
  calculateTotalLessons: (chapters: CourseChapterType[]): number => {
    return chapters.reduce((total, chapter) => total + chapter.lessons.length, 0);
  },

  /**
   * Format duration in minutes to human-readable string
   */
  formatDuration: (minutes: number): string => {
    if (minutes < 60) {
      return `${minutes}m`;
    }
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    return remainingMinutes > 0 ? `${hours}h ${remainingMinutes}m` : `${hours}h`;
  },

  /**
   * Validate course data and return detailed errors
   */
  validateCourseDetailed: (course: CourseType): CourseValidationErrorType[] => {
    // This function is exported from the reducer, but we provide it here for convenience
    return validateCourse(course);
  },

  /**
   * Check if course is ready for publishing
   */
  isReadyForPublishing: (course: CourseType): boolean => {
    const errors = validateCourse(course);
    // Simplified check - just validate there are no errors
    // The content.chapters check is removed as Course type doesn't have this structure
    return errors.length === 0;
  },

  /**
   * Generate course progress percentage
   */
  calculateProgress: (chapters: CourseChapterType[]): number => {
    if (!chapters.length) return 0;

    const totalLessons = chapters.reduce((total, chapter) => total + chapter.lessons.length, 0);
    const completedLessons = chapters.reduce((total, chapter) => {
      // Learner completion is optional on editor lessons.
      return total + chapter.lessons.filter((lesson: CourseLessonType & { isCompleted?: boolean }) => lesson.isCompleted).length;
    }, 0);

    return totalLessons > 0 ? Math.round((completedLessons / totalLessons) * 100) : 0;
  },
} as const;

/**
 * Course Editor Error Messages
 */
export const COURSE_EDITOR_ERRORS = {
  VALIDATION: {
    TITLE_REQUIRED: 'Course title is required',
    TITLE_TOO_SHORT: `Title must be at least ${COURSE_EDITOR_CONFIG.MIN_TITLE_LENGTH} characters`,
    TITLE_TOO_LONG: `Title must be no more than ${COURSE_EDITOR_CONFIG.MAX_TITLE_LENGTH} characters`,
    DESCRIPTION_REQUIRED: 'Course description is required',
    DESCRIPTION_TOO_SHORT: `Description must be at least ${COURSE_EDITOR_CONFIG.MIN_DESCRIPTION_LENGTH} characters`,
    DESCRIPTION_TOO_LONG: `Description must be no more than ${COURSE_EDITOR_CONFIG.MAX_DESCRIPTION_LENGTH} characters`,
    AREA_REQUIRED: 'Course area is required',
    LEVEL_REQUIRED: 'Course level is required',
    TOOLS_REQUIRED: 'At least one tool is required',
  },
  OPERATIONS: {
    SAVE_FAILED: 'Failed to save course',
    LOAD_FAILED: 'Failed to load course',
    AUTO_SAVE_FAILED: 'Auto-save failed',
    PUBLISH_FAILED: 'Failed to publish course',
    CREATE_FAILED: 'Failed to create course',
  },
  CONTEXT: {
    PROVIDER_MISSING: 'useCourseEditor must be used within a CourseEditorProvider',
  },
} as const;

/**
 * Default export for the entire Course Editor module
 */
const CourseEditor = {
  // Core exports
  Provider: CourseEditorProvider,
  useCourseEditor,

  // Configuration and utilities
  Config: COURSE_EDITOR_CONFIG,
  Utils: CourseEditorUtils,
  Errors: COURSE_EDITOR_ERRORS,

  // Actions
  Actions: {
    getCourseBySlug,
    saveCourse,
    autoSaveCourse,
    createCourse,
    publishCourse,
  },

  // Types (for TypeScript users)
  Types: {} as {
    State: CourseEditorState;
    Action: CourseEditorAction;
    ContextValue: CourseEditorContextValue;
    Config: CourseEditorConfig;
    ValidationError: CourseValidationErrorType;
    Chapter: CourseChapterType;
    Lesson: CourseLessonType;
    Course: CourseType;
  },
};

export default CourseEditor;

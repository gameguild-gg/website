/**
 * Course Editor Types and Interfaces
 *
 * Comprehensive type definitions for the Course Editor Provider system
 */

import { Course } from '@/lib/courses';
import { ReactNode } from 'react';

// Stub type for CourseContent which doesn't exist in @/lib/courses
export interface CourseContent {
  chapters?: CourseChapter[];
  syllabus?: string;
  prerequisites?: string[];
  objectives?: string[];
  totalDuration?: number;
  totalLessons?: number;
}

/**
 * Course Editor Action Types
 */
export const CourseEditorActionType = {
  // Loading states
  SET_LOADING: 'SET_LOADING',
  SET_ERROR: 'SET_ERROR',
  CLEAR_ERROR: 'CLEAR_ERROR',

  // Course loading and management
  SET_COURSE: 'SET_COURSE',
  CLEAR_COURSE: 'CLEAR_COURSE',

  // Course editing operations
  UPDATE_COURSE_FIELD: 'UPDATE_COURSE_FIELD',
  UPDATE_COURSE_CONTENT: 'UPDATE_COURSE_CONTENT',
  UPDATE_COURSE_METADATA: 'UPDATE_COURSE_METADATA',

  // Content operations
  ADD_LESSON: 'ADD_LESSON',
  UPDATE_LESSON: 'UPDATE_LESSON',
  DELETE_LESSON: 'DELETE_LESSON',
  REORDER_LESSONS: 'REORDER_LESSONS',

  ADD_CHAPTER: 'ADD_CHAPTER',
  UPDATE_CHAPTER: 'UPDATE_CHAPTER',
  DELETE_CHAPTER: 'DELETE_CHAPTER',
  REORDER_CHAPTERS: 'REORDER_CHAPTERS',

  // Save states
  SET_SAVING: 'SET_SAVING',
  SET_SAVE_STATUS: 'SET_SAVE_STATUS',
  SET_LAST_SAVED: 'SET_LAST_SAVED',

  // Validation
  SET_VALIDATION_ERRORS: 'SET_VALIDATION_ERRORS',
  CLEAR_VALIDATION_ERRORS: 'CLEAR_VALIDATION_ERRORS',

  // Editor state
  SET_EDITOR_MODE: 'SET_EDITOR_MODE',
  SET_ACTIVE_TAB: 'SET_ACTIVE_TAB',
  SET_PREVIEW_MODE: 'SET_PREVIEW_MODE',

  // Undo/Redo
  ADD_TO_HISTORY: 'ADD_TO_HISTORY',
  UNDO: 'UNDO',
  REDO: 'REDO',
  CLEAR_HISTORY: 'CLEAR_HISTORY',

  // Auto-save
  ENABLE_AUTO_SAVE: 'ENABLE_AUTO_SAVE',
  DISABLE_AUTO_SAVE: 'DISABLE_AUTO_SAVE',
  SET_AUTO_SAVE_INTERVAL: 'SET_AUTO_SAVE_INTERVAL',

  // Reset
  RESET_EDITOR: 'RESET_EDITOR',
} as const;

/**
 * Course Editor Modes
 */
export type CourseEditorMode = 'create' | 'edit' | 'preview' | 'review';

/**
 * Course Editor Tabs
 */
export type CourseEditorTab = 'general' | 'content' | 'lessons' | 'settings' | 'preview' | 'publish';

/**
 * Save Status Types
 */
export type CourseSaveStatus = 'idle' | 'saving' | 'saved' | 'error' | 'conflict';

/**
 * Preview Mode Types
 */
export type CoursePreviewMode = 'student' | 'instructor' | 'admin';

/**
 * Course Lesson Interface
 */
export interface CourseLesson {
  id: string;
  title: string;
  description: string;
  content: string;
  duration?: number; // in minutes
  order: number;
  isPublished: boolean;
  videoUrl?: string;
  resources?: CourseLessonResource[];
  quiz?: CourseLessonQuiz;
  createdAt?: string;
  updatedAt?: string;
}

/**
 * Course Lesson Resource
 */
export interface CourseLessonResource {
  id: string;
  title: string;
  type: 'pdf' | 'link' | 'download' | 'video' | 'image';
  url: string;
  description?: string;
  size?: number; // in bytes
}

/**
 * Course Lesson Quiz
 */
export interface CourseLessonQuiz {
  id: string;
  title: string;
  questions: CourseLessonQuestion[];
  passingScore: number; // percentage
  allowRetries: boolean;
  timeLimit?: number; // in minutes
}

/**
 * Course Lesson Question
 */
export interface CourseLessonQuestion {
  id: string;
  question: string;
  type: 'multiple-choice' | 'true-false' | 'short-answer' | 'essay';
  options?: string[];
  correctAnswer: string | string[];
  explanation?: string;
  points: number;
}

/**
 * Course Chapter Interface
 */
export interface CourseChapter {
  id: string;
  title: string;
  description: string;
  order: number;
  isPublished: boolean;
  lessons: CourseLesson[];
  createdAt?: string;
  updatedAt?: string;
}

/**
 * Enhanced Course Content for Editor
 */
export interface EnhancedCourseContent extends CourseContent {
  chapters: CourseChapter[];
  totalDuration?: number; // calculated from lessons
  totalLessons?: number; // calculated from chapters
  completionRate?: number; // for editing existing courses
}

/**
 * Course Validation Error
 */
export interface CourseValidationError {
  field: string;
  message: string;
  type: 'required' | 'invalid' | 'duplicate' | 'length';
}

/**
 * Course Editor Configuration
 */
export interface CourseEditorConfig {
  autoSave: boolean;
  autoSaveInterval: number; // in milliseconds
  enableHistory: boolean;
  maxHistorySteps: number;
  enablePreview: boolean;
  enableDrafts: boolean;
  enableCollaboration: boolean;
  maxFileSize: number; // in bytes
  allowedFileTypes: string[];
  enableRichTextEditor: boolean;
  enableMarkdown: boolean;
}

/**
 * Course Editor History Entry
 */
export interface CourseEditorHistoryEntry {
  id: string;
  timestamp: Date;
  action: string;
  course: Course;
  description: string;
}

/**
 * Course Editor State
 */
export interface CourseEditorState {
  // Core state
  course: Course | null;
  originalCourse: Course | null; // for comparison and reset
  isLoading: boolean;
  error: string | null;

  // Save state
  isSaving: boolean;
  saveStatus: CourseSaveStatus;
  lastSaved: Date | null;
  hasUnsavedChanges: boolean;

  // Validation
  validationErrors: CourseValidationError[];
  isValid: boolean;

  // Editor state
  mode: CourseEditorMode;
  activeTab: CourseEditorTab;
  previewMode: CoursePreviewMode;

  // History (undo/redo)
  history: CourseEditorHistoryEntry[];
  historyIndex: number;
  canUndo: boolean;
  canRedo: boolean;

  // Configuration
  config: CourseEditorConfig;

  // UI state
  sidebarCollapsed: boolean;
  previewPanelOpen: boolean;
  fullScreenMode: boolean;

  // Meta
  lastUpdated: Date;
}

/**
 * Course Editor Actions
 */
export type CourseEditorAction =
  // Loading states
  | { type: typeof CourseEditorActionType.SET_LOADING; payload: boolean }
  | { type: typeof CourseEditorActionType.SET_ERROR; payload: string | null }
  | { type: typeof CourseEditorActionType.CLEAR_ERROR }

  // Course management
  | { type: typeof CourseEditorActionType.SET_COURSE; payload: Course }
  | { type: typeof CourseEditorActionType.CLEAR_COURSE }

  // Course editing
  | {
    type: typeof CourseEditorActionType.UPDATE_COURSE_FIELD;
    payload: { field: keyof Course; value: unknown };
  }
  | { type: typeof CourseEditorActionType.UPDATE_COURSE_CONTENT; payload: Partial<EnhancedCourseContent> }
  | { type: typeof CourseEditorActionType.UPDATE_COURSE_METADATA; payload: Partial<Course> }

  // Content operations
  | { type: typeof CourseEditorActionType.ADD_LESSON; payload: { chapterId: string; lesson: CourseLesson } }
  | {
    type: typeof CourseEditorActionType.UPDATE_LESSON;
    payload: { chapterId: string; lessonId: string; lesson: Partial<CourseLesson> };
  }
  | { type: typeof CourseEditorActionType.DELETE_LESSON; payload: { chapterId: string; lessonId: string } }
  | { type: typeof CourseEditorActionType.REORDER_LESSONS; payload: { chapterId: string; lessonIds: string[] } }
  | { type: typeof CourseEditorActionType.ADD_CHAPTER; payload: CourseChapter }
  | {
    type: typeof CourseEditorActionType.UPDATE_CHAPTER;
    payload: { chapterId: string; chapter: Partial<CourseChapter> };
  }
  | { type: typeof CourseEditorActionType.DELETE_CHAPTER; payload: string }
  | { type: typeof CourseEditorActionType.REORDER_CHAPTERS; payload: string[] }

  // Save states
  | { type: typeof CourseEditorActionType.SET_SAVING; payload: boolean }
  | { type: typeof CourseEditorActionType.SET_SAVE_STATUS; payload: CourseSaveStatus }
  | { type: typeof CourseEditorActionType.SET_LAST_SAVED; payload: Date }

  // Validation
  | { type: typeof CourseEditorActionType.SET_VALIDATION_ERRORS; payload: CourseValidationError[] }
  | { type: typeof CourseEditorActionType.CLEAR_VALIDATION_ERRORS }

  // Editor state
  | { type: typeof CourseEditorActionType.SET_EDITOR_MODE; payload: CourseEditorMode }
  | { type: typeof CourseEditorActionType.SET_ACTIVE_TAB; payload: CourseEditorTab }
  | { type: typeof CourseEditorActionType.SET_PREVIEW_MODE; payload: CoursePreviewMode }

  // History
  | { type: typeof CourseEditorActionType.ADD_TO_HISTORY; payload: CourseEditorHistoryEntry }
  | { type: typeof CourseEditorActionType.UNDO }
  | { type: typeof CourseEditorActionType.REDO }
  | { type: typeof CourseEditorActionType.CLEAR_HISTORY }

  // Auto-save
  | { type: typeof CourseEditorActionType.ENABLE_AUTO_SAVE }
  | { type: typeof CourseEditorActionType.DISABLE_AUTO_SAVE }
  | { type: typeof CourseEditorActionType.SET_AUTO_SAVE_INTERVAL; payload: number }

  // Reset
  | { type: typeof CourseEditorActionType.RESET_EDITOR };

/**
 * Course Editor Reducer Type
 */
export type CourseEditorReducer = (state: CourseEditorState, action: CourseEditorAction) => CourseEditorState;

/**
 * Course Editor Context Value
 */
export interface CourseEditorContextValue {
  // State
  state: CourseEditorState;

  // Course operations
  loadCourse: (courseId: string) => Promise<void>;
  loadCourseBySlug: (slug: string) => Promise<void>;
  createNewCourse: () => void;
  saveCourse: () => Promise<boolean>;
  publishCourse: () => Promise<boolean>;
  resetCourse: () => void;

  // Course editing
  updateCourseField: (field: keyof Course, value: unknown) => void;
  updateCourseContent: (content: Partial<EnhancedCourseContent>) => void;
  updateCourseMetadata: (metadata: Partial<Course>) => void;

  // Content management
  addChapter: (chapter: Omit<CourseChapter, 'id' | 'order'>) => void;
  updateChapter: (chapterId: string, chapter: Partial<CourseChapter>) => void;
  deleteChapter: (chapterId: string) => void;
  reorderChapters: (chapterIds: string[]) => void;

  addLesson: (chapterId: string, lesson: Omit<CourseLesson, 'id' | 'order'>) => void;
  updateLesson: (chapterId: string, lessonId: string, lesson: Partial<CourseLesson>) => void;
  deleteLesson: (chapterId: string, lessonId: string) => void;
  reorderLessons: (chapterId: string, lessonIds: string[]) => void;

  // Validation
  validateCourse: () => boolean;
  clearValidationErrors: () => void;

  // Editor state
  setEditorMode: (mode: CourseEditorMode) => void;
  setActiveTab: (tab: CourseEditorTab) => void;
  setPreviewMode: (mode: CoursePreviewMode) => void;

  // History
  undo: () => void;
  redo: () => void;
  clearHistory: () => void;

  // Auto-save
  enableAutoSave: () => void;
  disableAutoSave: () => void;
  setAutoSaveInterval: (interval: number) => void;

  // Loading & errors
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  clearError: () => void;

  // Utilities
  hasUnsavedChanges: () => boolean;
  canSave: () => boolean;
  canPublish: () => boolean;
  getDuration: () => number;
  getLessonCount: () => number;
  getProgress: () => number; // completion percentage
}

/**
 * Course Editor Provider Props
 */
export interface CourseEditorProviderProps {
  children: ReactNode;
  courseId?: string;
  slug?: string;
  mode?: CourseEditorMode;
  config?: Partial<CourseEditorConfig>;
  onSave?: (course: Course) => Promise<boolean>;
  onPublish?: (course: Course) => Promise<boolean>;
  onError?: (error: string) => void;
}

/**
 * Default Course Editor Configuration
 */
export const defaultCourseEditorConfig: CourseEditorConfig = {
  autoSave: true,
  autoSaveInterval: 30000, // 30 seconds
  enableHistory: true,
  maxHistorySteps: 50,
  enablePreview: true,
  enableDrafts: true,
  enableCollaboration: false,
  maxFileSize: 10 * 1024 * 1024, // 10MB
  allowedFileTypes: ['pdf', 'doc', 'docx', 'txt', 'mp4', 'mp3', 'jpg', 'jpeg', 'png', 'gif'],
  enableRichTextEditor: true,
  enableMarkdown: true,
};

/**
 * Default Course Editor State
 */
export const defaultCourseEditorState: CourseEditorState = {
  // Core state
  course: null,
  originalCourse: null,
  isLoading: false,
  error: null,

  // Save state
  isSaving: false,
  saveStatus: 'idle',
  lastSaved: null,
  hasUnsavedChanges: false,

  // Validation
  validationErrors: [],
  isValid: true,

  // Editor state
  mode: 'create',
  activeTab: 'general',
  previewMode: 'student',

  // History
  history: [],
  historyIndex: -1,
  canUndo: false,
  canRedo: false,

  // Configuration
  config: defaultCourseEditorConfig,

  // UI state
  sidebarCollapsed: false,
  previewPanelOpen: false,
  fullScreenMode: false,

  // Meta
  lastUpdated: new Date(),
};

/**
 * Course Editor Constants
 */
export const COURSE_EDITOR_STORAGE_KEY = 'course-editor-state';
export const COURSE_EDITOR_AUTO_SAVE_KEY = 'course-editor-auto-save';

/**
 * Course Editor Validation Rules
 */
export const courseEditorValidationRules = {
  title: {
    required: true,
    minLength: 3,
    maxLength: 100,
  },
  description: {
    required: true,
    minLength: 10,
    maxLength: 500,
  },
  area: {
    required: true,
  },
  level: {
    required: true,
  },
  status: {
    required: true,
  },
  tools: {
    required: true,
    minItems: 1,
  },
} as const;

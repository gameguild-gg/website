import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface Lesson {
  id: string;
  order: number;
  title: string;
  description: string;
  thumbnail: File | null;
  content: File | null;
  additionalText: string;
  extraContent: File[];
}

interface Pricing {
  fullPrice: number;
  installments: { number: number; price: number }[];
  maxDiscount: number;
  maxUnits: number;
  publishDate: string;
  unpublishDate: string;
}

interface CourseState {
  id: string;
  title: string;
  description: string;
  uniqueUrl: string;
  thumbnail: File | null;
  demoContent: File[];
  lessons: Lesson[];
  pricing: Pricing;
  setId: (id: string) => void;
  setTitle: (title: string) => void;
  setDescription: (description: string) => void;
  setUniqueUrl: (url: string) => void;
  setThumbnail: (thumbnail: File | null) => void;
  setDemoContent: (demoContent: File[]) => void;
  addLesson: (lesson: Omit<Lesson, 'order'>) => void;
  updateLesson: (id: string, updatedLesson: Partial<Lesson>) => void;
  reorderLessons: (startIndex: number, endIndex: number) => void;
  setPricing: (pricing: Pricing) => void;
  resetStore: () => void;
}

const initialState = {
  id: '',
  title: '',
  description: '',
  uniqueUrl: '',
  thumbnail: null,
  demoContent: [],
  lessons: [],
  pricing: {
    fullPrice: 0,
    installments: [],
    maxDiscount: 0,
    maxUnits: 0,
    publishDate: '',
    unpublishDate: '',
  },
}

export const useCourseStore = create<CourseState>()(
  persist(
    (set, get) => ({
      ...initialState,
      setId: (id) => set({ id }),
      setTitle: (title) => set({ title }),
      setDescription: (description) => set({ description }),
      setUniqueUrl: (uniqueUrl) => set({ uniqueUrl }),
      setThumbnail: (thumbnail) => set({ thumbnail }),
      setDemoContent: (demoContent) => set({ demoContent }),
      addLesson: (lesson) => set((state) => {
        const newOrder = state.lessons.length + 1;
        return { lessons: [...state.lessons, { ...lesson, order: newOrder }] };
      }),
      updateLesson: (id, updatedLesson) => set((state) => ({
        lessons: state.lessons.map((lesson) => 
          lesson.id === id ? { ...lesson, ...updatedLesson } : lesson
        ),
      })),
      reorderLessons: (startIndex, endIndex) => set((state) => {
        const newLessons = Array.from(state.lessons);
        const [reorderedItem] = newLessons.splice(startIndex, 1);
        newLessons.splice(endIndex, 0, reorderedItem);
        return { 
          lessons: newLessons.map((lesson, index) => ({ ...lesson, order: index + 1 }))
        };
      }),
      setPricing: (pricing) => set({ pricing }),
      resetStore: () => set(initialState),
    }),
    {
      name: 'course-storage',
      getStorage: () => localStorage,
      serialize: (state) => {
        const { thumbnail, demoContent, lessons, ...rest } = state;
        return JSON.stringify({
          ...rest,
          lessons: lessons.map(lesson => ({
            ...lesson,
            thumbnail: null,
            content: null,
            extraContent: [],
          })),
        });
      },
      deserialize: (str) => {
        const state = JSON.parse(str);
        return {
          ...state,
          thumbnail: null,
          demoContent: [],
          lessons: state.lessons.map(lesson => ({
            ...lesson,
            thumbnail: null,
            content: null,
            extraContent: [],
          })),
        };
      },
    }
  )
)


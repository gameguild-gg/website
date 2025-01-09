import React, { createContext, useContext, useState, useEffect } from 'react';

export interface Lesson {
  id: string;
  group: string;
  title: string;
  description: string;
  previewContent: string;
  mainContent: string;
  downloadFiles: File[] | null;
  complementaryText: string;
}

export interface CourseData {
  id: string;
  title: string;
  briefDescription: string;
  thumbnail: File | null;
  lessons: Lesson[];
  pricing: {
    price: number;
    installmentPrice: { [key: number]: number };
    maxDiscount: number;
    maxSales: number;
    publishDate: string;
    unpublishDate: string;
  };
}

const initialData: CourseData = {
  id: '', // será preenchido pelo sistema
  title: '',
  briefDescription: '',
  thumbnail: null,
  lessons: [],
  pricing: {
    price: 0,
    installmentPrice: { 2: 0, 3: 0, 6: 0, 12: 0 },
    maxDiscount: 0,
    maxSales: 0,
    publishDate: '',
    unpublishDate: '',
  },
};

const CourseContext = createContext<{
  courseData: CourseData;
  updateCourseData: (data: Partial<CourseData>) => void;
}>({
  courseData: initialData,
  updateCourseData: () => {},
});

export const CourseProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [courseData, setCourseData] = useState<CourseData>(initialData);

  useEffect(() => {
    const savedData = localStorage.getItem('courseData');
    if (savedData) {
      setCourseData(JSON.parse(savedData));
    }
  }, []);

  useEffect(() => {
    localStorage.setItem('courseData', JSON.stringify(courseData));
  }, [courseData]);

  const updateCourseData = (data: Partial<CourseData>) => {
    setCourseData((prev) => ({ ...prev, ...data }));
  };

  return (
    <CourseContext.Provider value={{ courseData, updateCourseData }}>
      {children}
    </CourseContext.Provider>
  );
};

export const useCourseContext = () => useContext(CourseContext);

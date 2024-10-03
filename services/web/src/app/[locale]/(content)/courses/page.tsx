/* eslint-disable react/no-unescaped-entities */
import React from 'react';
import { ParamsWithLocale } from '@/types';
import { fetchCourses } from '@/lib/course/fetch-courses.action';
import Link from 'next/link';
import Image from 'next/image';

export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export default async function Page() {
  const courses = await fetchCourses();

  if (courses.length === 0) {
    return (
      <div>
        <p>It's looks like that we don't have a course yet.</p>
      </div>
    );
  }

  return (
    // TODO: Refactor this page layout.
    <div className="flex flex-col w-full h-full bg-neutral-100 p-2">
      <div className="w-full mx-auto min-w-[320px] max-w-[1370px] bg-white p-4">
        <h1 className="text-2xl font-semibold">Courses</h1>
        <div className="flex flex-wrap justify-around gap-4">
          {courses.map((course) => (
            <div
              className="group bg-white border-2 shadow-lg rounded-lg w-full lg:w-1/4 md:w-1/3 h-72 m-2 transform transition-all duration-300 hover:scale-105 hover:shadow-2xl overflow-hidden cursor-pointer"
              key={course.id}
            >
              <Link href={`/courses/${course.slug}`}>
                <Image
                  src="/assets/images/placeholder.svg"
                  className="w-full h-1/2 object-cover"
                  alt={course.name}
                  width={800}
                  height={500}
                />
                <div className="font-bold text-lg p-3">{course.name}</div>
                <div className="p-2 mb-8 text-neutral-700 text-sm">
                  {course.description}
                </div>
              </Link>
            </div>
          ))}
        </div>
      </div>
      <div className="bg-neutral-100 pb-12"></div>
    </div>
  );
}

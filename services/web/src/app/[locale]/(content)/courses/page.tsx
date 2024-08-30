import React from 'react';
import {ParamsWithLocale} from "@/types";
import {fetchCourses} from "@/lib/course/fetch-courses.action";
import Link from "next/link";

export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

export default async function Page() {
  const courses = await fetchCourses();

  if (courses.length === 0) {
    return (
      <div>
        <p>It's looks like that we don't have a course yet.</p>
      </div>
    )
  }

  return (
    // TODO: Refactor this page layout.
    <div className="w-full h-full bg-neutral-100">
      <div className="w-[1440px] max-w-full min-h-screen mx-auto bg-white p-2 ">
        <h1>Courses</h1>
        {/*<div*/}
        {/*  className={`bg-neutral-500 text-white p-2 my-2 rounded-lg ${searchSettingsVisible ? '' : 'hidden'} `}*/}
        {/*>*/}
        {/*  [ Search Settings Here ]*/}
        {/*</div>*/}
        <div className="h-full grid grid-cols-1 md:grid-cols-4 justify-around md:mx-[0px]">
          {courses.map((course) => (
            <div
              className="group bg-white border-2 shadow rounded-lg w-full md:w-[345px] h-[285px] m-2 hover:scale-105 duration-300 overflow-hidden cursor-pointer"
              key={course.id}
            >
              <Link href={`/courses/${course.slug}`}>
                <img src="/assets/images/placeholder.svg" className="w-fullobject-none h-[170px]"
                     alt={course.name}/>
                <div className="font-bold p-2">{course.name}</div>
                <div className="p-2 text-neutral-700">{course.description}</div>
              </Link>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

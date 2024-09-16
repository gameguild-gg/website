'use client';
import React, {useEffect, useState} from 'react';
import {Search, Settings2} from 'lucide-react';
import {CoursePagination} from '@/components/courses/course-pagination';
import {fetchCourses} from '@/lib/old/courses/actions';

const placeholder_courses = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

export default async function Page() {
  const [searchSettingsVisible, setSearchSettingsVisible] = useState(true);
  const [courses, setCourses] = useState<any[]>([]);
  const [pages, setPages] = useState(1);

  useEffect(() => {
    const fetch = async () => {
      const courseData = await fetchCourses();
      console.log('useEffect() courseData:\n', courseData);
      setCourses(courseData.courses);
      console.log('useEffect() courses:\n', courses);
      setPages(courseData.pages);
    };
    fetch();
  }, []);

  const handleSearchButton = () => {
    console.log('Search Button');
  };

  const handleSearchSettingsButton = () => {
    console.log('Search Settings Button');
    setSearchSettingsVisible(!searchSettingsVisible);
  };

  return (
    <div className="w-full h-full bg-neutral-100">
      <div className="w-[1440px] max-w-full min-h-screen mx-auto bg-white p-2 ">
        {/*Search Bar Row*/}
        <div className="flex h-[45px] text-center items-center">
          <input
            type="text"
            placeholder="Search..."
            className="h-[40px] border-2 border-black rounded-full p-2 w-full"
          />

          <button onClick={handleSearchButton} className="px-2">
            <Search/>
          </button>
          <button onClick={handleSearchSettingsButton}>
            <Settings2/>
          </button>
        </div>
        {/*Search Settings*/}
        <div
          className={`bg-neutral-500 text-white p-2 my-2 rounded-lg ${searchSettingsVisible ? '' : 'hidden'} `}
        >
          [ Search Settings Here ]
        </div>
        {/*Courses List*/}
        <div className="h-full grid grid-cols-1 md:grid-cols-4 justify-around md:mx-[0px]">
          {courses.map((c: any, i) => (
            <div
              className="group bg-white border-2 shadow rounded-lg w-full md:w-[345px] h-[285px] m-2 hover:scale-105 duration-300 overflow-hidden cursor-pointer"
              key={i}
            >
              <img
                src="/assets/images/placeholder.svg"
                className="w-full object-none h-[170px]"
              />
              <div className="font-bold p-2">{c.name}</div>
              <div className="p-2 text-neutral-700">{c.description}</div>
            </div>
          ))}
        </div>
        <div>
          <CoursePagination page={1} pages={pages}/>
        </div>
      </div>
    </div>
  );
}

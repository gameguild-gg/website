'use client'
import React, { useState } from 'react';
import { Search, TextSearch } from 'lucide-react';

const placeholder_courses = [1,2,3,4,5,6,7,8,9,10,11,12]

export default async function Page() {
  const [searchSettingsVisible, setSearchSettingsVisible] = useState(true)

  const handleSearchButton = () => {
    console.log("Search Button")
  }

  const handleSearchSettingsButton = () => {
    console.log("Search Settings Button")
    setSearchSettingsVisible(!searchSettingsVisible)
  }

  return (
    <div className='w-screen h-full bg-neutral-100'>
      <div className='w-[1440px] max-w-full min-h-screen mx-auto bg-white p-2 '>
          {/*Search Bar Row*/}
          <div className='flex h-[45px] text-center items-center'>
            <input type='text' placeholder="Search..." className='h-[40px] border-2 border-black rounded-full p-2 w-full' />
            
            <button onClick={handleSearchButton} className='px-2'>
              <Search />
            </button>
            <button  onClick={handleSearchSettingsButton}>
              <TextSearch />
            </button>
          </div>
          {/*Search Settings*/}
          <div className={`bg-neutral-500 text-white p-2 my-2 rounded-lg ${searchSettingsVisible ? '' : 'hidden'} `}>
            [ Search Settings Here ]
          </div>
          {/*Courses List*/}
          <div className='h-full grid grid-cols-1 md:grid-cols-4 justify-around md:-mx-[10px]'>
            {
              placeholder_courses.map(el=>

                  <div className='group bg-white border-2 shadow rounded-lg w-full md:w-[335px] h-[250px] mx-auto m-3 hover:scale-125 duration-300 ' key={el}>
                    <img
                      src='assets/images/placeholder.svg'
                      className='w-full object-none h-[200px] group-hover:h-[150px]'
                    />
                    <div className='font-bold p-2'>My Course {el}</div>
                    <div className='p-2 hidden group-hover:block'>Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor.</div>
                  </div>
              )
            }

          </div>
          <div>
            [ Pages List Here ]
          </div>
      </div>
    </div>
  );
}

import { getProjects } from '@/components/projects/actions';
import { ProjectList } from '@/components/projects/project-list';

export default async function ProjectsPage() {
  const projects = await getProjects();

  return (
    <div className="flex flex-col flex-1 items-center justify-center">
      <div className="flex flex-col flex-1 container">
        <ProjectList initialProjects={[]} />
      </div>
    </div>
  );
}

// 'use client'; // todo: I dont think all things from dashboard should be client side
//
// import React, { useEffect, useState } from 'react';
// import Link from 'next/link';
// import { Button } from '@/components/ui/button';
// import { GameCard } from '@/components/testing-lab/game-card';
// import { Sheet, SheetContent, SheetTrigger } from '@/components/ui/sheet';
// import ProjectForm from '@/components/projects/project-form';
// import { getSession } from 'next-auth/react';
// import { useRouter } from 'next/navigation';
// import { Api, ProjectApi } from '@game-guild/apiclient';
// import ProjectEntity = Api.ProjectEntity;
//
// export default async function Page() {
//   const [projects, setProjects] = useState<ProjectEntity[] | null>(null);
//   const router = useRouter();
//
//   const fetchProjects = async () => {
//     if (projects === null) {
//       const session = await getSession();
//       const api = new ProjectApi({ basePath: process.env.NEXT_PUBLIC_API_URL });
//       const projects = await api.getManyBaseProjectControllerProjectEntity({}, { headers: { Authorization: `Bearer ${session?.user?.accessToken}` } });
//       if (projects.status == 401) {
//         router.push('/disconnect');
//       } else if (projects.status >= 400) {
//         console.error(projects.body);
//       }
//       setProjects(projects.body as ProjectEntity[]);
//     }
//   };
//
//   useEffect(() => {
//     fetchProjects();
//   });
//
//   return (
//     <div className="flex flex-col flex-1">
//       <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
//         <div className="bg-white shadow rounded-lg overflow-hidden">
//           <div className="p-6">
//             <h1 className="text-2xl font-semibold text-gray-900">Creator Dashboard</h1>
//             <div className="mt-4 flex justify-end space-x-4">
//               <div className="text-center">
//                 <div className="text-2xl font-bold">0</div>
//                 <div className="text-sm text-gray-500">Views</div>
//               </div>
//               <div className="text-center">
//                 <div className="text-2xl font-bold">0</div>
//                 <div className="text-sm text-gray-500">Downloads</div>
//               </div>
//               <div className="text-center">
//                 <div className="text-2xl font-bold">0</div>
//                 <div className="text-sm text-gray-500">Followers</div>
//               </div>
//             </div>
//           </div>
//           <div className="border-t border-gray-200">
//             <nav className="flex">
//               <Link href="#" className="px-6 py-3 text-sm font-medium text-pink-500 border-b-2 border-pink-500">
//                 Projects
//               </Link>
//               <Link href="#" className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700">
//                 Analytics
//               </Link>
//               <Link href="#" className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700">
//                 Earnings
//               </Link>
//               <Link href="#" className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700">
//                 Promotions
//               </Link>
//               <Link href="#" className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700">
//                 Posts
//               </Link>
//               <Link href="#" className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700">
//                 Game jams
//               </Link>
//               <Link href="#" className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700">
//                 More
//               </Link>
//             </nav>
//           </div>
//           <div className="p-6">
//             {/*<div className="bg-pink-50 border border-pink-100 rounded-md p-4 mb-6">*/}
//             {/*  <p className="text-pink-800">*/}
//             {/*    <span className="font-semibold">itch.io tips:</span> Tell us how*/}
//             {/*    you build your projects • Try the Engines, Tools, & Devices*/}
//             {/*    classification{' '}*/}
//             {/*    <Link href="#" className="text-pink-600 hover:underline">*/}
//             {/*      learn more →*/}
//             {/*    </Link>*/}
//             {/*  </p>*/}
//             {/*</div>*/}
//             {projects?.length === 0 && (
//               <div className="text-center py-12">
//                 <h2 className="text-2xl font-semibold text-gray-700 mb-6">Are you a developer? Upload your first game</h2>
//                 <Sheet>
//                   <SheetTrigger asChild>
//                     <Button className="bg-pink-500 text-white hover:bg-pink-600">Create new project</Button>
//                   </SheetTrigger>
//                   <SheetContent className="min-w-full">
//                     <ProjectForm action={'update'} />
//                   </SheetContent>
//                 </Sheet>
//                 <div className="mt-4">
//                   <Link href="#" className="text-sm text-gray-500 hover:underline">
//                     Nah, take me to the games feed
//                   </Link>
//                 </div>
//               </div>
//             )}
//             {projects?.map((project) => (
//               <Link key={project.slug} href={`/project/${project.slug}`}>
//                 <GameCard game={project} />
//               </Link>
//             ))}
//           </div>
//         </div>
//       </main>
//     </div>
//   );
// }

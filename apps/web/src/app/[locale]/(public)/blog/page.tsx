'use client';
import { useRouter } from 'next/navigation';

export default function Page() {
  // const {posts, pagination} = await fetchPosts();
  //
  // return (
  //   <div>
  //     <div
  //       style={{
  //         textAlign: 'center',
  //         color: '#fff',
  //         backgroundColor: '#101014',
  //         alignItems: 'center',
  //         alignContent: 'center',
  //         overflow: 'hidden',
  //         width: '100%',
  //       }}
  //     >
  //       <div
  //         style={{
  //           maxWidth: '1200px',
  //           width: '100%',
  //           background: '#18181c', //#18181c
  //         }}
  //         className="grid grid-cols-1 md:grid-cols-3 mx-auto"
  //       >
  //         {posts &&
  //           posts.map((post: any) => <PostCard post={post} key={post.id}/>)}
  //       </div>
  //       <div>
  //         <BlogPagination page={1} pages={pagination.pages}/>
  //       </div>
  //     </div>
  //   </div>
  // );

  const router = useRouter();
  // redirect to gameguild.gg/blog/
  router.push('https://blog.gameguild.gg/');
}

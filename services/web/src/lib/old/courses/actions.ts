'use server';

const coursesPage1 = [
  {name: "My Course 1", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 2", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 3", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 4", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 5", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 6", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 7", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 8", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 9", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 10", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 11", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 12", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
]

const coursesPage2 = [
  {name: "My Course 13", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 14", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 15", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 16", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 17", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
  {name: "My Course 18", description: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor."},
]

export const fetchCourses = async (page: number = 1, limit: string = "12") => {
  // const admin_domain = process.env.GHOST_ADMIN_DOMAIN;
  // const content_api_key = process.env.GHOST_CONTENT_API_KEY;

  let courses:any[] = []

  if (page == 1){
    courses = coursesPage1
  } else {
    courses = coursesPage2
  }
  
  const pages = 2

  // if (!response.ok) {
  //   throw new Error("Failed to fetch posts");
  // }

  //const { posts, meta: { pagination } } = await response.json();

  return {courses, pages}
};

export const fetchPost = async (slug: string) => {
  const admin_domain = process.env.GHOST_ADMIN_DOMAIN;
  const content_api_key = process.env.GHOST_CONTENT_API_KEY;

  const response = await fetch(
    `https://${admin_domain}/ghost/api/content/posts/slug/${slug}?key=${content_api_key}`,
    {
      headers: {
        "Content-Type": "application/json",
        "Accept-Version": "v5.0"
      }
    }
  );

  if (!response.ok) {
    throw new Error("Failed to fetch post");
  }

  const { posts } = await response.json();

  return posts[0];
};

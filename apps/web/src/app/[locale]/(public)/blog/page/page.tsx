import { redirect } from 'next/navigation';

async function Page() {
  redirect('/blog');
}

export default Page;

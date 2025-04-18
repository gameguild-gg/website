import { redirect } from 'next/navigation';

export default async function Page(): Promise<void> {
  redirect('/dashboard/overview');
}

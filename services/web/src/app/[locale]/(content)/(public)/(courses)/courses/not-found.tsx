import Link from 'next/link';

export default function CourseNotFound() {
  return (
    <div className="container mx-auto py-8 text-center">
      <h1 className="text-4xl font-bold mb-4">Course Not Found</h1>
      <p className="mb-4">
        Sorry, we couldn't find the course you're looking for.
      </p>
      <Link href="/courses" className="text-blue-600 hover:underline">
        Return to Courses
      </Link>
    </div>
  );
}

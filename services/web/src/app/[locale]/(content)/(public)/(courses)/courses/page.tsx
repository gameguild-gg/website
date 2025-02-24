import Link from 'next/link';
import { courses } from '@/data/courses';
import { CourseCard } from '@/components/courses/course-card';
import { Button } from '@/components/ui/button';
import Image from 'next/image';

export async function generateMetadata({ params }) {
  return {};
}

export default function Page() {
  if (!courses || courses.length === 0) {
    return (
      <div className="container mx-auto py-8">
        <h1 className="text-3xl font-bold mb-8">Available Courses</h1>
        <p>No courses available at the moment.</p>
      </div>
    );
  }

  return (
    <div className="container mx-auto">
      <section className="relative mb-16">
        {/* Background Image */}
        <Image
          src="/assets/images/placeholder.svg"
          alt="Game Development Background"
          width={1920}
          height={1080}
          className="absolute inset-0 h-[500px] w-full object-cover brightness-80"
          priority
        />

        {/* Content */}
        <div className="relative mx-auto max-w-7xl px-4 py-16 sm:px-6 lg:px-8">
          <div className="grid gap-8 lg:grid-cols-2 items-center">
            {/* Left Column - Text Content */}
            <div className="space-y-6">
              <h1 className="text-4xl font-bold text-black md:text-5xl lg:text-6xl">Game Development Courses</h1>
              <p className="max-w-2xl text-lg text-black/90">
                Join Game Guild, a community where individuals help each other break into game development. Our courses cover key aspects of development,
                including programming, game design, and animation. Collaborate with fellow aspiring developers, share knowledge, and work towards landing a job
                at top gaming studios like WildLife Studios.
              </p>
            </div>

            {/* Right Column - Call to Action */}
            <div className="flex items-center w-full lg:w-auto">
              <div className="w-full max-w-none lg:max-w-md rounded-lg bg-white p-6 shadow-lg text-center">
                <h2 className="mb-4 text-2xl font-bold">Ready to Level Up?</h2>
                <p className="mb-6 text-gray-600">
                  Book a meeting with our game development expert to discuss your career goals and how our courses can help you achieve them.
                </p>
                <Button className="w-full bg-[#00A651] font-semibold text-white hover:bg-[#008c44]" asChild>
                  <a href="https://calendar.app.google/EU42UnUSyTwyhryL9" target="_blank" rel="noopener noreferrer">
                    Schedule a Meeting
                  </a>
                </Button>
              </div>
            </div>
          </div>
        </div>
      </section>
      
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {courses.map((course) => (
          <Link href={`/course/${course.slug}`} key={course.id} className="block">
            <CourseCard course={course} />
          </Link>
        ))}
      </div>
    </div>
  );
}

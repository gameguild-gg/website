import Link from 'next/link';
import Image from 'next/image';
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Api } from '@game-guild/apiclient';
import CourseEntity = Api.CourseEntity;

interface CourseCardProps {
  course: CourseEntity;
}

export function CourseCard({ course }: CourseCardProps) {
  const imageUrl = course.thumbnail
    ? `${course.thumbnail.path}/${course.thumbnail.filename}`
    : '/placeholder.svg?height=200&width=300';

  return (
    <Card className="overflow-hidden">
      <Image
        src={imageUrl}
        alt={course.thumbnail?.description || course.title}
        width={course.thumbnail?.width || 300}
        height={course.thumbnail?.height || 200}
        className="w-full object-cover h-48"
      />
      <CardHeader>
        <CardTitle>{course.title}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-gray-600">{course.summary}</p>
        <p className="mt-2 font-semibold">
          {course.price === 0 || course.price === undefined ? (
            <span className="text-green-600">Free Course</span>
          ) : (
            <span>${course.price.toFixed(2)}</span>
          )}
        </p>
        {course.subscriptionAccess && (
          <p className="text-sm text-blue-600">Requires Subscription</p>
        )}
      </CardContent>
      <CardFooter>
        <Link href={`/course/${course.slug}`} passHref>
          <Button>View Course</Button>
        </Link>
      </CardFooter>
    </Card>
  );
}

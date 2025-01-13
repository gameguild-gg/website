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
  console.log(`Rendering CourseCard for course: ${course.id}`);

  try {
    const imageUrl = course.thumbnail
      ? `${course.thumbnail.path}/${course.thumbnail.filename}`
      : '/placeholder.svg?height=200&width=300';

    return (
      <Card className="overflow-hidden h-full flex flex-col">
        <div className="relative h-48">
          <Image
            src={imageUrl}
            alt={course.thumbnail?.description || course.title}
            fill
            className="object-cover"
          />
        </div>
        <CardHeader>
          <CardTitle>{course.title}</CardTitle>
        </CardHeader>
        <CardContent className="flex-grow">
          <p className="text-sm text-gray-600">{course.summary}</p>
          <p className="mt-2 font-semibold">
            {course.price ? (
              <span>${course.price.toFixed(2)}</span>
            ) : (
              <span className="text-green-600">Free Course</span>
            )}
          </p>
          {course.subscriptionAccess && (
            <p className="text-sm text-blue-600">Requires Subscription</p>
          )}
        </CardContent>
        <CardFooter>
          <Button className="w-full">View Course</Button>
        </CardFooter>
      </Card>
    );
  } catch (error) {
    console.error(`Error rendering CourseCard for course ${course.id}:`, error);
    return <div>Error rendering course card</div>;
  }
}

import { Game } from '@/lib/testing-lab/game';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Api } from '@game-guild/apiclient';
import ProjectEntity = Api.ProjectEntity;

type Props = {
  game: ProjectEntity;
};

export function GameCard({ game }: Readonly<Props>) {
  const { title, summary } = game;

  return (
    <Card className="rounded-lg overflow-hidden shadow-lg max-w-[320px] mx-auto hover:shadow-xl transition-all duration-200">
      <div className="relative">
        <img
          alt="Profile picture"
          className="object-cover w-full"
          height="320"
          src="/assets/images/placeholder.svg"
          style={{ aspectRatio: '320/320', objectFit: 'cover' }}
          width="320"
        />
        <div className="flex justify-end">
          <Badge>{'draft'}</Badge>
        </div>
      </div>
      <CardContent className="p-4">
        <CardHeader>
          <CardTitle>{title}</CardTitle>
          <CardDescription>{summary}</CardDescription>
        </CardHeader>
      </CardContent>
    </Card>
  );
}

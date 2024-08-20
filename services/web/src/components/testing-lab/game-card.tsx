import {Game} from "@/lib/testing-lab/game";
import {Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle} from "@/components/ui/card";
import {Badge} from "@/components/ui/badge";

type Props = {
  game: Game
};

export function GameCard({game}: Readonly<Props>) {

  const {name, description} = game;

  return (
    <Card className="max-w-md">
      <CardHeader>
        <CardTitle>{name}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="flex-1">

      </CardContent>
      <CardFooter>
        <div className="flex justify-end">
          <Badge>{"draft"}</Badge>
        </div>
      </CardFooter>
    </Card>
  );
}
import React from "react";
import CardRoot from "./card-root";
import CardHeader from "./card-header";
import CardTitle from "./card-title";
import CardDescription from "./card-description";
import CardContent from "./card-content";
import CardFooter from "./card-footer";

type CardProps = React.HTMLAttributes<HTMLDivElement> & {
  children: React.ReactNode;
};

const Card: React.FunctionComponent<CardProps> & {
  Header: typeof CardHeader;
  Title: typeof CardTitle;
  Description: typeof CardDescription;
  Content: typeof CardContent;
  Footer: typeof CardFooter;
} = ({ children }: Readonly<CardProps>) => {
  return (
    <CardRoot>
      {children}
    </CardRoot>
  );
};

Card.Header = CardHeader;
Card.Title = CardTitle;
Card.Description = CardDescription;
Card.Content = CardContent;
Card.Footer = CardFooter;

export default Card;

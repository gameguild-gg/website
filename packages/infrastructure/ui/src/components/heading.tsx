interface HeadingProps {
  heading: string;
  description?: string;
}

export function Heading({ heading, description }: HeadingProps) {
  return (
    <div className="grid gap-1">
      <h1 className="text-3xl font-bold tracking-tight">{heading}</h1>
      {description && <p className="text-muted-foreground">{description}</p>}
    </div>
  );
}

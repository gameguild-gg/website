type Props = {
  params: {
    slug: string;
  };
};

export default function Component({ params: { slug } }: Readonly<Props>) {
  return (
    <div>
      <h1>{slug}</h1>
    </div>
  );
}

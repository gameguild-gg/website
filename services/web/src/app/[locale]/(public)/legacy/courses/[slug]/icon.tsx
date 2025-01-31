type Props = {
  params: {
    slug: string;
  };
};

export const size = { width: 32, height: 32 };

export const contentType = 'image/png';

export default async function Icon({ params: { slug } }: Readonly<Props>) {
}

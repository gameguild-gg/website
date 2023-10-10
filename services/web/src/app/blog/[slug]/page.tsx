type IBlogUrl = {
  slug: string;
};

export async function generateStaticParams() {
  return Array.from(
    Array(10).keys())
    .map((elt) => ({ params: { slug: `blog-${ elt }` } }));
}

function Post({ params }: { params: { slug: string } }) {
  return (
    <main>
      <h1 className="capitalize">{ params.slug }</h1>
      <p>
        Lorem ipsum dolor sit amet consectetur adipisicing elit. Inventore eos
        earum doloribus, quibusdam magni accusamus vitae! Nisi, sunt! Aliquam
        iste expedita cupiditate a quidem culpa eligendi, aperiam saepe dolores
        ipsum!
      </p>
    </main>
  )
}

export default Post;

import Head from 'next/head'
import styles from '@/styles/Home.module.css'

export default function Home() {
  return (
    <>
      <Head>
        <title>Create Next App</title>
        <meta name="description" content="An awesome site for our community" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <link rel="icon" href="/favicon.ico" />
      </Head>
      <main className={styles.main}>
        <div className={styles.mainArticle}>
          <h2>Autentication</h2>
          <br />
          <p>
            Nulla ut sagittis sapien, molestie mollis nibh. Suspendisse placerat massa id massa iaculis molestie. Nunc porttitor quam ac lectus tempor, at vestibulum sapien vulputate. Etiam non quam est. Duis id consequat mauris. Maecenas vitae odio libero. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Aliquam enim turpis, dignissim non ante dignissim, facilisis accumsan lorem
          </p>
          <br />
          <p>
              Aenean ut massa id tellus pulvinar suscipit. Curabitur sodales, libero eget finibus feugiat, felis lorem luctus erat, pellentesque hendrerit turpis diam et odio. Nam bibendum a nisl ut mattis. Maecenas venenatis, elit fermentum luctus mollis, arcu mauris tincidunt arcu, nec lacinia nibh eros non velit. Vestibulum luctus dapibus tortor vitae tristique. Pellentesque finibus sem sed venenatis aliquam. Sed quam lorem, luctus eu justo a, fringilla venenatis mauris. Integer bibendum blandit tellus, et accumsan nulla laoreet a. Cras tempor id elit lobortis efficitur. Vivamus molestie dolor nec egestas tempus. Fusce non lobortis lorem. Mauris quis magna justo. Suspendisse placerat fringilla blandit. Aliquam erat volutpat. Etiam congue massa sed sollicitudin porta. Suspendisse ut porta dolor.
          </p>
        </div>
      </main>
    </>
  )
}

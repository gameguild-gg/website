import "../styles/globals.css"
import type { AppProps } from 'next/app'
import DefaultLayout from './../components/defaultlayout'

export default function App({ Component, pageProps }: AppProps) {
  return (
    <DefaultLayout { ...pageProps }>
      <Component {...pageProps} />
    </DefaultLayout>
  );
}

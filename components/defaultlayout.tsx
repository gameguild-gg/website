import styles from "../styles/DefaultLayout.module.css";
import Navbar from "./navbar";
import Footer from "./footer";
import Grid from '@mui/material/Unstable_Grid2';

interface DefaultLayoutProps {
    children: JSX.Element;
}

const DefaultLayout = ({ children }: DefaultLayoutProps) => {
    return (
        <>
          <Grid container className={styles.container}>
            <Grid xs={3}>
             <Navbar />
            </Grid>
            <Grid xs={8} xsOffset={1}>
              <div>{children}</div>
            </Grid>
            <Grid xs={12}>
              <Footer />
            </Grid>
          </Grid>
        </>
    );
};

export default DefaultLayout;

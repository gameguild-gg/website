import styles from "../styles/DefaultLayout.module.css";
import Navbar from "./navbar";
import Footer from "./footer";

interface DefaultLayoutProps {
    children: JSX.Element;
}

const DefaultLayout = ({ children }: DefaultLayoutProps) => {
    return (
        <>
            <div className={styles.container}>
                <Navbar />
                <div className={styles.content}>
                    { children }
                </div>
                <Footer />
            </div>
        </>
    );
};

export default DefaultLayout;

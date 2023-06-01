import styles from "../styles/DefaultLayout.module.css";
import SideMenu from "./sidemenu";

interface DefaultLayoutProps {
    children: JSX.Element;
}

const DefaultLayout = ({ children }: DefaultLayoutProps) => {
    return (
        <>
            <div className={styles.container}>
                <SideMenu />
                <div className={styles.content}>
                    { children }
                </div>
            </div>
        </>
    );
};

export default DefaultLayout;

import Link from "next/link";
import styles from "@/styles/Navbar.module.css";

const Navbar = (children: any) => {
    return (
      <div className={ styles.navbar }>
        <div className={ styles.logo }>
            <Link href="/">
                <img src="./Logo.png"/>
            </Link>
        </div>
        <div className={ styles.links }>
            <Link href="/courses">
                Cursos
            </Link>
            <Link href="/projects">
                Projetos
            </Link>
            <Link href="/events">
                Evento da semana
            </Link>
            <div className={ styles.loginBtn }>
                <Link href="#">
                    Login
                </Link>
            </div>
        </div>
      </div>
    );
};

export default Navbar;
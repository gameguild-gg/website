import Link from "next/link";
import styles from "../styles/SideMenu.module.css";

const SideMenu = (children: any) => {
    return (
      <div className={ styles.sidemenu }>
        <div className={ styles.sidemenuHeader }>
          <div className={ styles.sidemenuLetter }>AGG</div>
          <div className={ styles.sidemenuTitle }>Awesome <br/>GameDev <br/>Guild</div>
        </div>
        <div className={ styles.menu }>
          <div className={ styles.menulinks }><Link href="/courses">Courses</Link></div>
          <div className={ styles.menulinks }><Link href="/projects">Projects</Link></div>
          <div className={ styles.menulinks }><Link href="/events">Week Event</Link></div>
        </div>

        <div className={ styles.supportus }>
          <div className={ styles.supportusTitle }>Help us grown</div>
          <div className={ styles.supportusDetail }>Wanna know more?</div>
          <div className={ styles.supportusBtn }>
            <a>Contact Us</a>
          </div>
        </div>

        <div className={ styles.medias }>
          <div className={ styles.mediaIcons }>
            <div><img src="/Icons/WhatsApp.png" alt="WhatsApp Icon"/></div>
            <div><img src="/Icons/Discord.png" alt="Discord Icon"/></div>
            <div><img src="/Icons/Github.png" alt="Github Icon"/></div>
            <div><img src="/Icons/YouTube.png" alt="YouTube Icon"/></div>
            <div><img src="/Icons/Twitch.png" alt="Twitch Icon"/></div>
          </div>
          <div className={ styles.mediaRef }>
            <a href='https://www.flaticon.com/br/autores/ruslan-babkin/flat?author_id=492&type=standard'>Icons by Ruslan Babkin</a>
          </div>
        </div>
      </div>
    );
};

export default SideMenu;

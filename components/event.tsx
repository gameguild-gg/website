import styles from "../styles/EventCard.module.css"

interface EventProps {
    type: string;
    title: string;
    author: string;
    date: string;
    votes: number;
}

const EventCard = (eventProps: EventProps) => {
    return (
        <div className={ styles.event }>
            <div className={ styles.type }>[{ eventProps.type }]</div>
            <div className={ styles.title }>{ eventProps.title }</div>
            <div className={ styles.author }>{ eventProps.author }</div>
            <div className={ styles.counter }>
                <div><img onClick={ () => { console.log("Add vote.") } } src="/Icons/Add.png" /></div>
                <div className={ styles.votes }>{ eventProps.votes }</div>
                <div><img onClick={ () => { console.log("Minus vote.") } } src="/Icons/Minus.png" /></div>
            </div>
        </div>
    );
}

export default EventCard;
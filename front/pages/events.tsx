import EventCard from "../components/event";
import styles from "../styles/Events.module.css"

const EventsPage = () => {
    const events = [
        { type: "Art Dojo", title: "Modelagem 3D para jogos", author: "Germano Gomes", date: "19h00-12/05/2023", votes: 0, voters: [] },
        { type: "Art Dojo", title: "Modelagem 3D para jogos", author: "Germano Gomes", date: "19h00-12/05/2023", votes: 0, voters: [] },
        { type: "Art Dojo", title: "Modelagem 3D para jogos", author: "Germano Gomes", date: "19h00-12/05/2023", votes: 0, voters: [] },
        { type: "Art Dojo", title: "Modelagem 3D para jogos", author: "Germano Gomes", date: "19h00-12/05/2023", votes: 0, voters: [] },
    ];

    function Vote(index: number, voter: string) {
        events[index].votes += 1;
        // events[index].voters.push(voter);
    }

    return (<>
        <div className={ styles.container }>
            <div className={ styles.title }>Week events Page</div>
            <div className={ styles.events }>
                { events.map((event, index) => {
                    return <EventCard
                        key={`key-${index}`}
                        type={ event.type }
                        title={ event.title }
                        author={ event.author }
                        date={ event.date }
                        votes={ event.votes }
                    />
                })}
            </div>
        </div>
    </>);
}

export default EventsPage;

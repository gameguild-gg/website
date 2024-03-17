import React from 'react';

import { Notification } from '../notification';

type NotificationsProps = {
  notifications: any[];
};

const Notifications: React.FunctionComponent<Readonly<NotificationsProps>> & {
  // TODO: Expose sub-components here.
} = ({ notifications }: Readonly<NotificationsProps>) => {
  return (
    <div>
      {notifications && notifications.length > 0 && (
        <div>
          {notifications.map((notification, index) => <Notification key={index} />)}
        </div>
      )}
    </div>
  );
}; 

export { Notifications };
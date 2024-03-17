import React from "react";
import { NotificationRoot } from "./notification-root";
import { NotificationIcon } from "./notification-icon";
import { NotificationContent } from "./notification-content";
import { NotificationActions } from "./notification-actions";
import { NotificationAction } from "./notification-action";

type NotificationProps = {
  actions?: any[];
};

const Notification: React.FunctionComponent<Readonly<NotificationProps>> & {
  Icon: typeof NotificationIcon;
  Action: typeof NotificationAction;
} = ({ actions }: Readonly<NotificationProps>) => {
  return <NotificationRoot>
    <NotificationIcon />
    <NotificationContent>
      a
    </NotificationContent>
    {actions && actions.length > 0 && (
      <NotificationActions>
        {actions.map((action, index) => <NotificationAction key={index} />)}
      </NotificationActions>
    )}
  </NotificationRoot>;
};

Notification.Icon = NotificationIcon;
Notification.Action = NotificationAction;

export { Notification };

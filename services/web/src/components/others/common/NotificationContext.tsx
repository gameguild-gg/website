import React, { createContext, useContext, ReactNode } from 'react';

type NotificationContextProps = {
  name: string;
  setName: (name: string) => void;
};

const NotificationContext = createContext<NotificationContextProps | undefined>(
  undefined,
);

type NotificationProviderProps = {
  children: ReactNode;
};

export const NotificationProvider: React.FC<NotificationProviderProps> = ({
  children,
}) => {
  const [name, setName] = React.useState('Default');

  const value: NotificationContextProps = {
    name,
    setName,
  };

  return (
    <NotificationContext.Provider value={value}>
      {children}
    </NotificationContext.Provider>
  );
};

export const useNotificationContext = () => {
  const context = useContext(NotificationContext);

  if (!context) {
    throw new Error(
      'useNotificationContext must be used within a NotificationProvider',
    );
  }

  return context;
};

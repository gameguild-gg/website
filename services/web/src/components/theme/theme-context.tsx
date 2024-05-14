"use client";

import React from "react";

type Action =
  | { type: "" }
  | { type: "" };

type Context = { state: State; dispatch: Dispatch } | undefined;

type Dispatch = (action: Action) => void;

type Props = { children: React.ReactNode };

type State = {};

const ThemeContext = React.createContext<Context>(undefined);

const InitialState: State = {};

function themeReducer(state: State, action: Action) {
  switch (action.type) {
    case "":
      return {
        ...state
      };
    default:
      // throw new Error(`Unhandled action type: ${action.type}`);
      return state;
  }
}

export function ThemeProvider({ children }: Readonly<Props>) {
  const [state, dispatch] = React.useReducer(themeReducer, InitialState);

  const value = { state, dispatch };

  return (
    <ThemeContext.Provider value={value}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const context = React.useContext(ThemeContext);

  if (context === undefined) {
    throw new Error("useThemeContext must be used within a ThemeProvider");
  }

  return context;
}

import React from "react";

type SwitchProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {}

const Switch: React.FunctionComponent<Readonly<SwitchProps>> & {
  //
} = ({ ...props }: Readonly<SwitchProps>) => {
  return (
    <button
      type="button"
      role="switch"
      {...props}
    >
    </button>
  );
};

export default Switch;

import React, {PropsWithChildren} from "react";
import {cva, type VariantProps} from "class-variance-authority";


const headerVariants = cva('', {
  variants: {},
  defaultVariants: {},
});

type Props = PropsWithChildren<React.HTMLAttributes<HTMLDivElement>> & VariantProps<typeof headerVariants>;

const Header: React.FunctionComponent<Readonly<Props>> & {
  //
} = ({className, children, ...props}: Readonly<Props>) => {
  return (
    <div>
      {children}
    </div>
  );
}

export default Header;
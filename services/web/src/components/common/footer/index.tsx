import React, {PropsWithChildren} from "react";
import {cva, type VariantProps} from "class-variance-authority";
import Header from "@/components/common/header";


const footerVariants = cva('', {
  variants: {},
  defaultVariants: {},
});

type Props = PropsWithChildren<React.HTMLAttributes<HTMLDivElement>> & VariantProps<typeof footerVariants>;

const Footer: React.FunctionComponent<Readonly<Props>> & {
  //
} = ({className, children, ...props}: Readonly<Props>) => {
  return (
    <div>
      <Header>

      </Header>
      {children}
      <Footer>

      </Footer>
    </div>
  );
}

export default Footer;
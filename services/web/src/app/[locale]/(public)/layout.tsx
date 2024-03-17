"use client";

import React from "react";
import Header from "@/components/common/header";
import Footer from "@/components/common/footer";
import { Flex, Layout } from "antd";

type PublicLayoutProps = {
  children: React.ReactNode;
};

function PublicLayout({ children }: Readonly<PublicLayoutProps>) {
  return (
    <Flex gap="middle" wrap="wrap">
      <Layout style={{ overflow: "hidden", width: "100%", maxWidth: "100%" }}>
        <Header />
        <Layout.Content
          style={{
            textAlign: "center",
            lineHeight: "120px",
            color: "#fff",
            backgroundColor: "#101014",
            alignItems: "center",
            alignContent: "center",
            overflow: "hidden",
            width: "100%"
          }}
        >
          {children}
        </Layout.Content>
        <Footer />
      </Layout>
    </Flex>
  );
}

export default PublicLayout;

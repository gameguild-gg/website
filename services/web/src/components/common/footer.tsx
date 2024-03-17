"use client";
import { Col, Flex, Layout, Row } from "antd";

function Footer() {
  return (
    <Layout.Footer
      style={{
        textAlign: "center",
        color: "#fff",
        backgroundColor: "#2a2a2a"
      }}
    >
      <Flex justify="center" style={{ width: "100%" }} align="middle">
        <Row
          justify="space-between"
          style={{
            maxWidth: "1440px",
            width: "100%",
            display: "inline-flex",
            alignItems: "center",
            alignContent: "center"
          }}
        >
          <Col style={{ display: "inline-flex" }}>
            <a href="#">
              <img
                style={{ width: 30, height: 30, margin: 3 }}
                src="assets/images/whatsapp-icon.svg"
              />
            </a>
            <a href="#">
              <img
                style={{ width: 30, height: 30, margin: 3 }}
                src="assets/images/discord-icon.svg"
              />
            </a>
          </Col>
          <Col>Game Guild © 2024 All Rights Reserved</Col>
          <Col><a href="#" style={{ color: "white" }}>Privacy Policy</a> | <a href="#" style={{ color: "white" }}>Terms
            of Service</a></Col>
        </Row>

      </Flex>
    </Layout.Footer>
  );
}

export default Footer;

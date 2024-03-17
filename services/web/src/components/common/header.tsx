"use client";
import { Col, Layout, Row } from "antd";
import { UserOutlined } from "@ant-design/icons";

function Header() {
  return (
    <Layout.Header
      style={{
        textAlign: "center",
        color: "#fff",
        height: 64,
        paddingInline: 48,
        lineHeight: "60px",
        padding: "2px",
        backgroundColor: "#18181c"
      }}
    >
      <Row
        align="middle"
        justify="space-between"
        style={{
          textAlign: "center",
          width: "100%",
          display: "inline-flex",
          alignItems: "center"
        }}
      >
        <Col
          style={{
            display: "flex",
            minWidth: "150px",
            alignContent: "center"
          }}
        >
          <img
            style={{ width: 135, height: 46, margin: "7px" }}
            src="assets/images/logo-text.png"
          />
          &nbsp;
          {/*<a href="/">*/}
          {/*  <span*/}
          {/*    style={{*/}
          {/*      paddingLeft: '15px',*/}
          {/*      paddingRight: '15px',*/}
          {/*      color: '#ffffff',*/}
          {/*    }}*/}
          {/*  >*/}
          {/*    About*/}
          {/*  </span>*/}
          {/*</a>*/}
          {/*<a href="/">*/}
          {/*  <span*/}
          {/*    style={{*/}
          {/*      paddingLeft: '15px',*/}
          {/*      paddingRight: '15px',*/}
          {/*      color: '#ffffff',*/}
          {/*    }}*/}
          {/*  >*/}
          {/*    Courses*/}
          {/*  </span>*/}
          {/*</a>*/}
          {/*<a href="/">*/}
          {/*  <span*/}
          {/*    style={{*/}
          {/*      paddingLeft: '15px',*/}
          {/*      paddingRight: '15px',*/}
          {/*      color: '#ffffff',*/}
          {/*    }}*/}
          {/*  >*/}
          {/*    Jobs*/}
          {/*  </span>*/}
          {/*</a>*/}
          {/*<a href="/">*/}
          {/*  <span*/}
          {/*    style={{*/}
          {/*      paddingLeft: '15px',*/}
          {/*      paddingRight: '15px',*/}
          {/*      color: '#ffffff',*/}
          {/*    }}*/}
          {/*  >*/}
          {/*    Blog*/}
          {/*  </span>*/}
          {/*</a>*/}
          <a href="/competition">
                  <span
                    style={{
                      paddingLeft: "15px",
                      paddingRight: "15px",
                      color: "#ffffff"
                    }}
                  >
                    Competition
                  </span>
          </a>
        </Col>
        <Col style={{ display: "flex" }}>
          <a href="/login">
                  <span
                    style={{
                      padding: "15px",
                      paddingTop: "10px",
                      paddingBottom: "10px",
                      color: "#000000",
                      borderRadius: "30px",
                      backgroundColor: "#ffffff"
                    }}
                  >
                    <UserOutlined /> Login
                  </span>
          </a>
          <img
            width={25}
            src="assets/images/language.svg"
            style={{ margin: "7px" }}
          />
        </Col>
      </Row>
    </Layout.Header>
  );
}

export default Header;
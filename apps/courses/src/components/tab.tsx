import React from "react";
import { Layout, Model } from "flexlayout-react";
import "flexlayout-react/style/dark.css";

const TabTest: React.FC = () => {
  const model = Model.fromJson({
    global: {},
    borders: [],
    layout: {
      type: "row",
      weight: 100,
      children: [
        {
          type: "tabset",
          weight: 50,
          children: [
            {
              type: "tab",
              name: "Description",
              component: "button",
            },
            {
              type: "tab",
              name: "Editorial",
              component: "button",
            },
            {
              type: "tab",
              name: "Solutions",
              component: "button",
            },
            {
              type: "tab",
              name: "Submissions",
              component: "button",
            },
          ],
        },
        {
          type: "tabset",
          weight: 50,
          children: [
            {
              type: "tab",
              name: "Two",
              component: "button",
            },
          ],
        },
      ],
    },
  });

  const factory = (node) => {
    const component = node.getComponent();

    if (component === "button") {
      return <button>{node.getName()}</button>;
    }
  };

  return (
    <main>
      <Layout model={model} factory={factory} />
    </main>
  );
};

export default TabTest;

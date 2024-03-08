import { AssignmentBasev1_0_0 } from "../assignments/assignment.base.v1.0.0";

import {
  CopyOutlined,
  PlayCircleFilled,
  UndoOutlined,
} from "@ant-design/icons";
import { Editor } from "@monaco-editor/react";
import {
  Breadcrumb,
  Button,
  Col,
  Layout,
  Menu,
  notification,
  Row,
  Space,
  Input,
  Typography,
} from "antd";
const { TextArea } = Input;

import * as Comlink from "comlink";
import { editor } from "monaco-editor";
import React, { useEffect, useRef, useState } from "react";
import { HelloWorldCPP } from "../assignments/intro-cpp/hello-world-cpp";
import Markdown from "markdown-to-jsx";

import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import Emception from "./emception";

const { Header, Content, Footer } = Layout;

interface SyntaxTrainingPageProps {
  assignment?: AssignmentBasev1_0_0 | any; // add other versions here
  theme?: string;
  language?: string;
  height?: string;
}

type EmceptionWrapper = {
  worker: Comlink.Remote<Emception> | null;
};

const SyntaxTrainingPage: React.FC<SyntaxTrainingPageProps> = ({
  assignment = HelloWorldCPP,
  theme = "vs-dark",
  language = "cpp",
  height = "40vh",
}) => {
  const [code, setCode] = useState(assignment.initialCode);

  const [emceptionLoaded, setEmceptionLoaded] = useState(false);

  const [api, contextHolder] = notification.useNotification();
  const [consoleOutput, setConsoleOutput] = useState<string>("");

  const [emception, setEmception] = useState<EmceptionWrapper>({
    worker: null,
  });

  const writeLineToConsole = (str: any) => {
    setConsoleOutput(consoleOutput + str + "\n");
  };

  const clearConsole = () => {
    setConsoleOutput("");
  };

  // todo: fix code styles
  const CodeBlock = ({ children }: { children: React.ReactElement }) => {
    const { className, children: code } = children.props;

    const language = className?.replace("lang-", "");
    return <SyntaxHighlighter language={language}>{code}</SyntaxHighlighter>;
  };

  const prejs = `
if(!window.stdin)
    window.stdin = "";

if(!window.stdout)
    window.stdout = "";

if(!window.stderr)
    window.stderr = "";

var Module = {
    preRun: function() {
        window.prompt = function() {
            return window.stdin;
        };
    },
    stdout: function(c) {
        window.stdout += String.fromCharCode(c);
    },
    stderr: function(c) {
        window.stderr += String.fromCharCode(c);
    },
};
`;

  async function loadEmception(): Promise<any> {
    showNotification("Loading emception...");
    if (emceptionLoaded) return;
    setEmceptionLoaded(true);

    // todo: is it possible to not refer as url?
    const emceptionWorker = new Worker(
      new URL("./emception.worker.ts", import.meta.url),
      { type: "module" },
    );

    emceptionWorker.onerror = (e) => {
      console.error(e);
      showNotification("Emception worker error");
    };

    const emception: Comlink.Remote<Emception> = Comlink.wrap(emceptionWorker);

    setEmception({ worker: emception });

    emception.onstdout.bind(console.log);
    emception.onstderr.bind(console.log);
    emception.onprocessstart.bind(console.log);
    emception.onprocessend.bind(console.log);

    await emception.init();

    console.log("Post init");
    showNotification("Ready");
  }

  useEffect(() => {
    if (emceptionLoaded) return;
    setEmceptionLoaded(true);
    loadEmception();
  }, []);

  const editorRef = useRef<editor.IStandaloneCodeEditor | null>(null);
  const monacoRef = useRef(null);

  // todo: type corretly the event monaco.editor.IModelContentChangedEvent
  function handleEditorChange(value: string | undefined, event: any) {
    // here is the current value
    if (value === undefined) return;
    setCode(value);
    console.log(value);
    console.log("event", event);
  }

  function handleEditorDidMount(
    editor: editor.IStandaloneCodeEditor,
    monaco: any,
  ) {
    editorRef.current = editor;
    monacoRef.current = monaco;
    console.log("onMount: the editor instance:", editor);
    console.log("onMount: the monaco instance:", monaco);
  }

  function handleEditorWillMount(monaco: any) {
    monacoRef.current = monaco;
    console.log("beforeMount: the monaco instance:", monaco);
  }

  function handleEditorValidation(markers: any) {
    // model markers
    markers.forEach((marker) => console.log("onValidate:", marker.message));
  }

  const onprocessstart = (argv) => {
    writeLineToConsole(`\$ ${argv.join(" ")}`);
  };
  const onprocessend = () => {
    writeLineToConsole(`Process finished`);
  };

  const onRunClick = async () => {
    clearConsole();
    if (!emception || !emception.worker) {
      showNotification("Emception not loaded");
      console.log("Emception not loaded");
      return;
    }

    try {
      await emception.worker.fileSystem.writeFile("/working/main.cpp", code);
      await emception.worker.fileSystem.writeFile("/working/pre.js", prejs);
      const cmd = `em++ -O2 -fexceptions --pre-js /working/pre.js -sEXIT_RUNTIME=1 -std=c++23 -sSINGLE_FILE=1 -sUSE_CLOSURE_COMPILER=0 /working/main.cpp -o /working/main.js`;
      onprocessstart(`/emscripten/${cmd}`.split(/\s+/g));
      const result = await emception.worker.run(cmd);
      if (result.returncode == 0) {
        const content = await emception.worker.fileSystem.readFile(
          "/working/main.js",
          { encoding: "utf8" },
        );
        writeLineToConsole("Compilation succeeded");
        // todo: test all test cases
        // todo: create ways to pass custom inputs and custom outputs
        (window as any).stdin = assignment.inputs[0];
        (window as any).stdout = "";

        //wrap eval in a promise and wait it to finish
        await new Promise<void>((resolve) => {
          eval(content);
          resolve();
        });
        clearConsole();
        // todo: fix the requirement to use window.stdout and wait for it to be filled
        await new Promise((resolve) => setTimeout(resolve, 200)); // todo: this shouldnt be needed!!
        console.log((window as any).stdout);
        writeLineToConsole((window as any).stdout);
      } else {
        clearConsole();
        writeLineToConsole(`Emception compilation failed`);
        writeLineToConsole(result.stderr);
      }
    } catch (err) {
      clearConsole();
      writeLineToConsole(`Compilation error`);
      writeLineToConsole(err);
    } finally {
    }
  };

  const showNotification = (message: string) => {
    clearConsole();
    writeLineToConsole(message);
    api.info({
      message: message,
      placement: "topRight",
    });
  };

  const items = new Array(15).fill(null).map((_, index) => ({
    key: index + 1,
    label: `nav ${index + 1}`,
  }));

  const resetCode = () => {
    editorRef.current?.setValue(assignment.initialCode);
    showNotification("Code reset");
  };

  const copyCode = async () => {
    await navigator.clipboard.writeText(editorRef.current?.getValue() || "");
  };

  return (
    <>
      {contextHolder}
      <Layout>
        <Header style={{ display: "flex", alignItems: "center" }}>
          <div className="demo-logo" />
          <Menu
            theme="dark"
            mode="horizontal"
            defaultSelectedKeys={["2"]}
            items={items}
            style={{ flex: 1, minWidth: 0 }}
          />
        </Header>

        <Content style={{ padding: "0 48px" }}>
          <Breadcrumb
            style={{ margin: "16px 0" }}
            items={[{ title: "Home" }, { title: "List" }, { title: "App" }]}
          />
          <div
            style={{
              // background: colorBgContainer,
              minHeight: 280,
              padding: 24,
              // borderRadius: borderRadiusLG,
            }}
          >
            <Space direction="vertical">
              <Markdown
                options={{
                  forceBlock: true,
                  overrides: { pre: { component: CodeBlock } },
                }}
              >
                {assignment.description}
              </Markdown>
              <Editor
                height={height}
                width="100%"
                defaultLanguage={language}
                theme={theme}
                defaultValue={assignment.initialCode}
                onChange={handleEditorChange}
                onValidate={handleEditorValidation}
                onMount={handleEditorDidMount}
                beforeMount={handleEditorWillMount}
                options={{ automaticLayout: true, wordWrap: "on" }}
              />
              <Row justify="space-between">
                <Col>
                  <Space direction="horizontal">
                    <Button
                      type="primary"
                      onClick={onRunClick}
                      icon={<PlayCircleFilled />}
                    >
                      Run
                    </Button>
                  </Space>
                </Col>
                <Col>
                  <Button
                    type="default"
                    icon={<UndoOutlined />}
                    onClick={resetCode}
                  >
                    Reset code
                  </Button>
                  <Button
                    type="default"
                    icon={<CopyOutlined />}
                    onClick={copyCode}
                  >
                    Copy code
                  </Button>
                </Col>
              </Row>
              <TextArea
                disabled={true}
                value={consoleOutput}
                autoSize={{ minRows: 5 }}
              />
              {assignment.inputs.map((input, index) => {
                return (
                  <Row key={index}>
                    <Col>
                      <Typography.Text strong>Test {index + 1}</Typography.Text>
                    </Col>
                    <Col>
                      <Input value={assignment.inputs[index]} disabled={true} />
                    </Col>
                    <Col>
                      <Input
                        value={assignment.outputs[index]}
                        disabled={true}
                      />
                    </Col>
                  </Row>
                );
              })}
            </Space>
          </div>
        </Content>
        <Footer style={{ textAlign: "center" }}>
          GameGuild ©2023. Created by Alexandre Tolstenko.
        </Footer>
      </Layout>
    </>
  );
};

export default SyntaxTrainingPage;

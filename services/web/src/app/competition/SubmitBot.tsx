import React from 'react';

import { InboxOutlined, UploadOutlined, EyeInvisibleOutlined, EyeTwoTone, UserOutlined, KeyOutlined } from "@ant-design/icons";
import { Input, Tooltip } from "antd";

import {Button, Space, UploadProps} from 'antd';
import { message, Upload } from 'antd';
import JSZip from "jszip";
import { TerminalDto } from "@/dtos/competition/terminal.dto";

const { Dragger } = Upload;

// todo: convert to typescript style
const canUpload = function (file: any, fileList: any[] = []) {
  const isZip = file.type === 'application/zip' || file.name.endsWith('.zip');
  const isCppOrH = file.name.endsWith('.cpp') || file.name.endsWith('.h') || file.name.endsWith('.hpp');
  const isCppOrHList = fileList.every((file) => {
    return file.name.endsWith('.cpp') || file.name.endsWith('.h') || file.name.endsWith('.hpp');
  });
  
  return isCppOrH || isZip || isCppOrHList;
}

const SubmitBot: React.FC = () => {
  // store all files in state
  const [files, setFiles] = React.useState<File[]>([]);
  // state for username
  const [username, setUsername] = React.useState<string>('');
  // state for password
  const [password, setPassword] = React.useState<string>('');
  
  // upload functionality
  const doUpload = async () => {
    const isListOfCppOrH = files.length > 1 && files.every((file) => { return file.name.endsWith('.cpp') || file.name.endsWith('.h') || file.name.endsWith('.hpp'); });
    const isZip = files.length === 1 && (files[0].type === 'application/zip' || files[0].name.endsWith('.zip'));
    
    let zipData: ArrayBuffer | undefined = undefined;
    if(isZip) {
      message.info("validating zip file");
      // check if zip contains only cpp and h files at the root and does not contain any subfolders
      const zip = new JSZip();
      await zip.loadAsync(files[0]);
      const zipFiles = Object.values(zip.files);
      const zipFilesAtRoot = zipFiles.every((file) => { return !file.dir && file.name.indexOf('/') === -1; });
      const zipFilesAreCppOrH = zipFiles.every((file) => { return file.name.endsWith('.cpp') || file.name.endsWith('.h') || file.name.endsWith('.hpp'); });
      if(!zipFilesAtRoot || !zipFilesAreCppOrH) {
        message.error('zip file must contain only cpp and h files at the root');
        return;
      }
      else
        zipData = await files[0].arrayBuffer();
    }
    else if(isListOfCppOrH) {
      message.info("zipping files");
      // create a zip file for all files and upload it
      const zip = new JSZip();
      files.forEach((file) => {
        zip.file(file.name, file);
      });
      zipData = await zip.generateAsync({type: "arraybuffer"});
    }
    
    // check if zipData size is less than 10MB
    if(zipData && zipData.byteLength > 10 * 1024 * 1024) {
      message.error('zip file size must be less than 10MB');
      return;
    }
    
    message.info("uploading files");
    
    // upload the zip file
    const formData = new FormData();
    formData.append('username', username);
    formData.append('password', password);
    formData.append('file', new Blob([zipData as ArrayBuffer], {type: 'application/zip'}), 'bot.zip');
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const response = await fetch(baseUrl + '/Competitions/Chess/submit', {
      method: 'POST',
      body: formData
    });
    const data = await response.text();
    message.info(data);
    setFiles([]);
    setUsername('');
    setPassword('');
    message.info('done');
  };
  
  const uploadProps: UploadProps = {
    name: 'file',
    multiple: true,
    showUploadList: false,
    action: (file) => {
      // reject all because we are going to handle it in the button
      return Promise.reject();
    },
    onDrop(e) {
      console.log('Dropped files', e.dataTransfer.files);
    },
    beforeUpload(file, fileList) {
      const allowed = canUpload(file, fileList);
      if (!allowed) {
        message.error('You can only upload a single .zip file or a list of .cpp and .h files!');
      }
      else
        setFiles(fileList);
      return allowed || Upload.LIST_IGNORE;
    }
  };
  
  // todo: improve header and messaging
  return (
    <div>
      <h1>Submit Bot</h1>
      <br/>
      <p>You can either select all .h and .cpp files or zip them all together.</p>
      <p>If you submit via zip, all files should be in the root of the zip file.</p>
      <p>Hit submit button and wait for the submission to be validated.</p>
      <p>ToDo: should be protected and not require password</p>
      <Dragger {...uploadProps}>
        <p className="ant-upload-drag-icon">
          <InboxOutlined/>
        </p>
        <p className="ant-upload-text">Click or drag file(s) to this area to upload</p>
        <p className="ant-upload-hint">
          Support for a single "zip" file or a list of ".cpp" and ".h" files. Strictly prohibit from uploading other
          files. If you exploit this, you will be banned. I am logging everything you do. I am always watching. I am
          always listening. I am always waiting. I might even be watching you from behind. Look behind yourself. NOW!
        </p>
      </Dragger>
      <Input
        value={username}
        defaultValue=""
        placeholder="Enter your username"
        prefix={<UserOutlined className="site-form-item-icon" />}
        onChange={(e) => setUsername(e.target.value)}
      />
      <Input.Password
        value={password}
        defaultValue=""
        placeholder=""
        prefix={<KeyOutlined  className="site-form-item-icon" />}
        iconRender={(visible) => (visible ? <EyeTwoTone /> : <EyeInvisibleOutlined />)}
        onChange={(e) => setPassword(e.target.value)}
      />
      <Button icon={<UploadOutlined/>} type="primary" block danger size="large" onClick={() => doUpload()}>Submit</Button>
      {files.map((file) => (
        <tr key={file.name}>
          <td>{file.name}: </td>
          <td>{file.size} bytes</td>
        </tr>
      ))}
    </div>
)
}

export default SubmitBot;
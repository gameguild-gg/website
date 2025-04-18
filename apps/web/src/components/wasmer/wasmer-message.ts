/**
 * Types for communication between the Wasmer iframe and the parent page
 */

import { CodeLanguage, ProjectData, WasmerStatus } from './use-wasmer';

// Message types for communication
export enum WasmerMessageType {
  RUN_CODE = 'RUN_CODE',
  CODE_RESULT = 'CODE_RESULT',
  STATUS_UPDATE = 'STATUS_UPDATE',
  ERROR = 'ERROR',
}

// Request to run code
export interface RunCodeMessage {
  type: WasmerMessageType.RUN_CODE;
  data: {
    code: string;
    language: CodeLanguage;
    stdin?: string;
  };
  id: string; // Unique ID to match request/response
}

// Response with code execution results
export interface CodeResultMessage {
  type: WasmerMessageType.CODE_RESULT;
  data: {
    stdout?: string;
    stderr?: string;
  };
  id: string; // Same ID as the request
}

// Update on Wasmer status
export interface StatusUpdateMessage {
  type: WasmerMessageType.STATUS_UPDATE;
  data: {
    status: WasmerStatus;
  };
}

// Error message
export interface ErrorMessage {
  type: WasmerMessageType.ERROR;
  data: {
    message: string;
  };
  id?: string; // Optional - may be related to a specific request
}

// Union type for all possible messages
export type WasmerMessage = RunCodeMessage | CodeResultMessage | StatusUpdateMessage | ErrorMessage;

// Helper function to send messages to the parent window
export function sendMessageToParent(message: WasmerMessage): void {
  if (window.parent) {
    window.parent.postMessage(message, '*');
  }
}

// Helper function to send messages to the iframe
export function sendMessageToIframe(iframe: HTMLIFrameElement, message: WasmerMessage): void {
  if (iframe.contentWindow) {
    iframe.contentWindow.postMessage(message, '*');
  }
}

// Generate a unique message ID
export function generateMessageId(): string {
  return Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
}
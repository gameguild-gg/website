"use client";

import { useEffect, useRef, type CSSProperties, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";
import { cn } from "@game-guild/ui/lib/utils";
import { EditorSettingsMenu } from "./editor-settings-menu";
import type { EditorModalSize } from "./editor-preferences";
import type { FeatureEditorSettings } from "./use-feature-editor-settings";

const HTML_SCROLL_LOCK_CLASS = "lexical-surface-editor-scroll-lock";
let scrollLockCount = 0;

function acquireScrollLock() {
  let style = document.getElementById(`${HTML_SCROLL_LOCK_CLASS}-style`);
  if (!style) {
    style = document.createElement("style");
    style.id = `${HTML_SCROLL_LOCK_CLASS}-style`;
    style.textContent = `html.${HTML_SCROLL_LOCK_CLASS}{overflow:hidden!important;}`;
    document.head.appendChild(style);
  }
  scrollLockCount += 1;
  document.documentElement.classList.add(HTML_SCROLL_LOCK_CLASS);
}

function releaseScrollLock() {
  scrollLockCount = Math.max(0, scrollLockCount - 1);
  if (scrollLockCount === 0) {
    document.documentElement.classList.remove(HTML_SCROLL_LOCK_CLASS);
  }
}

function workspaceStyle(size: EditorModalSize): CSSProperties {
  if (size === "compact") {
    return {
      width: "calc(100vw - 32px)",
      maxWidth: 1280,
      height: "calc(100dvh - 32px)",
      maxHeight: 900,
    };
  }

  return {
    width: "100vw",
    maxWidth:
      size === "widescreen" ? 1920 : size === "ultrawide" ? 2560 : undefined,
    height: "100dvh",
  };
}

export function FeatureEditorShell({
  title,
  icon,
  headerMeta,
  headerActions,
  footer,
  children,
  onClose,
  bodyClassName,
  settings,
  disableBackdropClose = false,
}: {
  title: ReactNode;
  icon?: ReactNode;
  headerMeta?: ReactNode;
  headerActions?: ReactNode;
  footer?: ReactNode;
  children: ReactNode;
  onClose: () => void;
  bodyClassName?: string;
  settings: FeatureEditorSettings;
  disableBackdropClose?: boolean;
}) {
  const dialogRef = useRef<HTMLElement>(null);

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    acquireScrollLock();
    dialogRef.current?.focus();
    const closeOnEscape = (event: KeyboardEvent) => {
      if (
        event.key === "Escape" &&
        !event.defaultPrevented &&
        !document.querySelector("[data-radix-popper-content-wrapper]")
      ) {
        onClose();
      }
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      releaseScrollLock();
      document.removeEventListener("keydown", closeOnEscape);
      previouslyFocused?.focus();
    };
  }, [onClose]);

  if (typeof document === "undefined") return null;

  return createPortal(
    <div
      className={cn(
        "fixed inset-0 z-[100] flex items-center justify-center bg-black/60 backdrop-blur-sm",
        settings.modalSize === "compact" && "p-4",
      )}
      role="presentation"
      onMouseDown={(event) => {
        if (!disableBackdropClose && event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <section
        ref={dialogRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-label={typeof title === "string" ? title : "Feature editor"}
        style={workspaceStyle(settings.modalSize)}
        className={cn(
          "flex min-h-0 min-w-0 flex-col overflow-hidden border border-gray-200 bg-white shadow-2xl outline-none dark:border-gray-700 dark:bg-gray-900",
          settings.modalSize === "compact" && "rounded-md",
        )}
        onMouseDown={(event) => { event.stopPropagation(); }}
      >
        <header className="flex min-h-14 items-center justify-between gap-4 border-b border-gray-200 bg-gray-50 px-4 py-3 dark:border-gray-800 dark:bg-gray-900">
          <div className="flex min-w-0 items-center gap-2">
            {icon}
            <h2 className="truncate text-base font-semibold text-gray-900 sm:text-lg dark:text-gray-100">
              {title}
            </h2>
            {headerMeta && (
              <div className="ml-2 hidden min-w-0 items-center gap-3 border-l border-gray-300 pl-3 sm:flex dark:border-gray-700">
                {headerMeta}
              </div>
            )}
          </div>
          <div className="flex shrink-0 items-center gap-2">
            {headerActions}
            <EditorSettingsMenu settings={settings} />
            <button
              type="button"
              onClick={onClose}
              className="inline-flex h-8 w-8 items-center justify-center rounded hover:bg-gray-200 dark:hover:bg-gray-800"
              aria-label="Close editor"
              title="Close"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </header>
        <div className={cn("flex min-h-0 flex-1 flex-col", bodyClassName)}>
          {children}
        </div>
        {footer && (
          <footer className="border-t border-gray-200 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-900">
            {footer}
          </footer>
        )}
      </section>
    </div>,
    document.body,
  );
}

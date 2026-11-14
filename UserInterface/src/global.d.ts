/// <reference types="vite/client" />

declare global {
  interface Window {
    external: {
      sendMessage(message: string): void
      /** Photino macOS/Linux：注册后端消息回调（调用它，而不是给它赋值） */
      receiveMessage?(callback: (message: string) => void): void
    }
  }
  interface External {
    sendMessage(message: string): void
    /** Photino macOS/Linux：注册后端消息回调（调用它，而不是给它赋值） */
    receiveMessage?(callback: (message: string) => void): void
  }
}

export {}

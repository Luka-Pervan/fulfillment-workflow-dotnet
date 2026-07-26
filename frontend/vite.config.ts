import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    port: 5173,
    proxy: {
      "/api": "http://127.0.0.1:5080",
      "/dev": "http://127.0.0.1:5080",
      "/health": "http://127.0.0.1:5080"
    }
  },
  preview: {
    host: "127.0.0.1",
    port: 4173
  },
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: "./src/test/setup.ts",
    css: true,
    restoreMocks: true
  }
});

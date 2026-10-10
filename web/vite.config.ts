import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      // Same origin in the browser: cookie auth works without CORS.
      "/api": "http://localhost:5080",
    },
  },
  test: {
    include: ["src/**/*.test.ts"],
    environment: "node",
  },
});

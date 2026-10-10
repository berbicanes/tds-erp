import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// Minimal shape of the proxy error event (avoids pulling in @types/node for the config only).
interface ProxyResponse {
  headersSent?: boolean;
  writeHead?: (status: number, headers: Record<string, string>) => void;
  end: (body: string) => void;
}
interface ProxyErrorEmitter {
  on(event: "error", listener: (error: Error, req: unknown, res: ProxyResponse) => void): void;
}

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // 5174, not the Vite default 5173: other local projects (Vodostaji BiH) run there and their service worker
    // would control this origin.
    port: 5174,
    strictPort: true,
    proxy: {
      // Same origin in the browser: cookie auth works without CORS.
      "/api": {
        target: "http://localhost:5080",
        configure: (proxy) => {
          // API not running: answer with a clear ProblemDetails instead of an empty 500.
          (proxy as unknown as ProxyErrorEmitter).on("error", (_error, _req, res) => {
            if (!res.writeHead || res.headersSent) return;
            res.writeHead(503, { "Content-Type": "application/problem+json" });
            res.end(
              JSON.stringify({
                status: 503,
                title: "API nije pokrenut",
                detail: "API nije pokrenut na portu 5080. Pokreni ./dev.sh iz korijena projekta.",
              }),
            );
          });
        },
      },
    },
  },
  test: {
    // *.test.tsx render components in jsdom (set per file with @vitest-environment jsdom).
    include: ["src/**/*.test.ts", "src/**/*.test.tsx"],
    environment: "node",
    // labels.test.ts reads Tailwind's palette (?raw) to check badge contrast.
    css: { include: [/tailwindcss\/theme\.css/] },
  },
});

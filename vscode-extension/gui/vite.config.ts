import react from "@vitejs/plugin-react-swc";
import tailwindcss from "tailwindcss";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: "dist",
    sourcemap: true,
    rollupOptions: {
      input: "index.html",
      output: {
        format: "iife",
        entryFileNames: "assets/index.js",
        chunkFileNames: "assets/[name].js",
        assetFileNames: "assets/[name].[ext]",
        // IIFE must be inlined — no dynamic import() in webviews
        inlineDynamicImports: true,
      },
    },
  },
  server: {
    port: 5173,
    cors: { origin: "*" },
  },
});

const esbuild = require("esbuild");
const flags = process.argv.slice(2);

const config = {
  entryPoints: ["src/extension.ts"],
  bundle: true,
  outfile: "out/extension.js",
  external: ["vscode"],
  format: "cjs",
  platform: "node",
  sourcemap: true,
  logLevel: "info",
};

(async () => {
  if (flags.includes("--watch")) {
    const ctx = await esbuild.context(config);
    await ctx.watch();
    console.log("Watching extension host...");
  } else {
    await esbuild.build(config);
    console.log("Extension host build complete");
  }
})();

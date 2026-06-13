// Copies gui/dist/assets into out/webview/ after both builds complete.
// This avoids vscode-resource:// path issues with spaces in the project path
// by serving assets from out/ which is already trusted by the extension host.
const fs   = require("fs");
const path = require("path");

const src  = path.join(__dirname, "..", "gui", "dist", "assets");
const dest = path.join(__dirname, "..", "out", "webview");

if (!fs.existsSync(src)) {
  console.error("copy-gui: gui/dist/assets not found — run npm run build:gui first");
  process.exit(1);
}

fs.mkdirSync(dest, { recursive: true });

for (const file of fs.readdirSync(src)) {
  fs.copyFileSync(path.join(src, file), path.join(dest, file));
  console.log(`  copied ${file} → out/webview/${file}`);
}
console.log("copy-gui: done");

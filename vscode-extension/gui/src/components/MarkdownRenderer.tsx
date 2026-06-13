import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeHighlight from "rehype-highlight";
import "highlight.js/styles/github-dark-dimmed.css";
import { useState } from "react";

interface Props {
  content: string;
  streaming?: boolean;
}

export default function MarkdownRenderer({ content, streaming }: Props) {
  return (
    <div className={`prose prose-sm max-w-none text-[var(--vscode-foreground)] ${streaming ? "streaming-cursor" : ""}`}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeHighlight]}
        components={{
          // Custom code block with language header + copy button
          pre({ children, ...props }) {
            return <CodeBlock {...props}>{children}</CodeBlock>;
          },
          // Inline code
          code({ className, children, ...props }) {
            const isBlock = !!(props as { "data-language"?: string })["data-language"];
            if (isBlock) {
              return <code className={className} {...props}>{children}</code>;
            }
            return (
              <code
                style={{
                  background: "var(--vscode-textCodeBlock-background)",
                  borderRadius: 3,
                  padding: "0.1em 0.35em",
                  fontSize: "0.875em",
                }}
                {...props}
              >
                {children}
              </code>
            );
          },
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}

function CodeBlock({ children, ...props }: React.HTMLAttributes<HTMLPreElement>) {
  const [copied, setCopied] = useState(false);

  // Extract language from class name (e.g. "language-typescript")
  const codeEl = (children as React.ReactElement)?.props;
  const className: string = codeEl?.className ?? "";
  const lang = className.replace("language-", "").replace("hljs ", "").trim() || "code";
  const codeText: string = codeEl?.children ?? "";

  const copy = () => {
    const text = typeof codeText === "string" ? codeText : String(codeText);
    navigator.clipboard.writeText(text).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  };

  return (
    <div className="code-block-wrapper">
      <div className="code-block-header">
        <span className="code-block-lang">{lang}</span>
        <button className="copy-btn" onClick={copy}>
          {copied ? "Copied!" : "Copy"}
        </button>
      </div>
      <pre {...props} style={{ margin: 0, padding: "10px 12px", overflowX: "auto" }}>
        {children}
      </pre>
    </div>
  );
}

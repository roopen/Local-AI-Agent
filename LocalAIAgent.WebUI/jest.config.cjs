/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'jsdom',
  testMatch: ["**/__tests__/**/*.ts?(x)", "**/?(*.)+(spec|test).ts?(x)"],
  transform: {
    '^.+\\.(ts|tsx)$': ['ts-jest', { tsconfig: 'tsconfig.test.json' }],
    "^.+\\.(js|jsx)$": ['ts-jest', { tsconfig: { allowJs: true, target: 'ES2020', module: 'CommonJS', esModuleInterop: true }, diagnostics: false }],
  },
  // Transform the ESM Markdown dependency tree so tests exercise the real safe renderer.
  transformIgnorePatterns: [
    "node_modules/(?!(react-markdown|devlop|hast-util-to-jsx-runtime|comma-separated-tokens|estree-util-is-identifier-name|hast-util-whitespace|mdast-util-mdx-expression|mdast-util-from-markdown|decode-named-character-reference|character-entities|mdast-util-to-string|micromark|micromark-core-commonmark|micromark-factory-destination|micromark-util-character|micromark-util-symbol|micromark-util-types|micromark-factory-label|micromark-factory-space|micromark-factory-title|micromark-factory-whitespace|micromark-util-chunked|micromark-util-classify-character|micromark-util-html-tag-name|micromark-util-normalize-identifier|micromark-util-resolve-all|micromark-util-subtokenize|micromark-util-combine-extensions|micromark-util-decode-numeric-character-reference|micromark-util-encode|micromark-util-sanitize-uri|micromark-util-decode-string|unist-util-stringify-position|mdast-util-to-markdown|longest-streak|mdast-util-phrasing|unist-util-is|unist-util-visit|unist-util-visit-parents|zwitch|mdast-util-mdx-jsx|ccount|parse-entities|character-entities-legacy|character-reference-invalid|is-alphanumerical|is-alphabetical|is-decimal|is-hexadecimal|stringify-entities|character-entities-html4|vfile-message|mdast-util-mdxjs-esm|property-information|space-separated-tokens|unist-util-position|html-url-attributes|mdast-util-to-hast|@ungap/structured-clone|trim-lines|vfile|remark-parse|unified|bail|is-plain-obj|trough|remark-rehype|remark-gfm|mdast-util-gfm|mdast-util-gfm-autolink-literal|mdast-util-find-and-replace|mdast-util-gfm-footnote|mdast-util-gfm-strikethrough|mdast-util-gfm-table|markdown-table|mdast-util-gfm-task-list-item|micromark-extension-gfm|micromark-extension-gfm-autolink-literal|micromark-extension-gfm-footnote|micromark-extension-gfm-strikethrough|micromark-extension-gfm-table|micromark-extension-gfm-tagfilter|micromark-extension-gfm-task-list-item|remark-stringify)/)"
  ],
  moduleNameMapper: {
    "\\.(css|less|scss|sass)$": "<rootDir>/src/test/styleMock.cjs"
  }
};

import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import nextCoreWebVitals from "eslint-config-next/core-web-vitals";
import nextTypeScript from "eslint-config-next/typescript";
import prettierConfig from "eslint-config-prettier";
import tseslint from "typescript-eslint";

const projectRoot = dirname(fileURLToPath(import.meta.url));

export default tseslint.config(
  {
    ignores: [".next/**", "out/**", "node_modules/**", "next-env.d.ts"],
  },

  // eslint-config-next 16 doğrudan flat config dizisi verir; FlatCompat
  // köprüsüne gerek yoktur.
  ...nextCoreWebVitals,
  ...nextTypeScript,

  {
    files: ["**/*.ts", "**/*.tsx"],
    languageOptions: {
      parserOptions: {
        // Tip farkındalıklı kurallar için gereklidir.
        projectService: true,
        tsconfigRootDir: projectRoot,
      },
    },
    rules: {
      // `any` tip hatalarını gizler; bilinçli olarak hata seviyesinde.
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/no-unsafe-assignment": "error",
      "@typescript-eslint/no-unsafe-member-access": "error",
      "@typescript-eslint/no-unsafe-call": "error",
      "@typescript-eslint/no-unsafe-return": "error",
      "@typescript-eslint/no-unsafe-argument": "error",

      // Beklenmeyen promise kaçakları sessiz hataya dönüşür.
      "@typescript-eslint/no-floating-promises": "error",
      "@typescript-eslint/await-thenable": "error",
      "@typescript-eslint/no-misused-promises": "error",

      "@typescript-eslint/consistent-type-imports": [
        "error",
        { prefer: "type-imports", fixStyle: "inline-type-imports" },
      ],

      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" },
      ],

      "no-console": ["warn", { allow: ["warn", "error"] }],
      eqeqeq: ["error", "always", { null: "ignore" }],
    },
  },

  // Biçimlendirme Prettier'a bırakılır; çakışan stil kuralları kapatılır.
  prettierConfig,
);
